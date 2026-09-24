const visualizers = new WeakMap();

// The header speaker moves with whatever the equalizer currently shows.
let speaker = null;

// Display-frame smoothing of each band: fast rise, slower fall, like a
// hardware analyzer's attack/release ballistics.
const attackRate = 0.6;
const releaseRate = 0.1;
const reducedAttackRate = 0.18;
const reducedReleaseRate = 0.08;

// Each band is drawn as its own peak centred in its slot. The half-width is in
// band slots: below 1, a valley reaches the baseline between neighbouring peaks.
const peakHalfWidth = 0.5;
const peakSharpness = 1.8;

// Share of particles that trace the outline so every peak keeps a crisp edge.
const outlineShare = 0.5;

function clamp(value, minimum = 0, maximum = 1) {
    return Math.min(maximum, Math.max(minimum, value));
}

function createParticle(index, count) {
    const normalizedX = count <= 1 ? 0.5 : index / (count - 1);
    const tracesOutline = Math.random() < outlineShare;

    return {
        normalizedX,
        side: Math.random() < 0.5 ? -1 : 1,
        entranceSide: normalizedX < 0.5 ? -1 : 1,
        entranceDelay: Math.random() * 120,
        entranceDuration: 580 + Math.random() * 260,
        spread: tracesOutline ? 0.95 + Math.random() * 0.05 : Math.random(),
        depth: Math.random(),
        radius: 0.2 + Math.random() * 0.48,
        bandPosition: 0,
        launchX: 0,
        launchY: 0,
        x: 0,
        y: 0
    };
}

function createState(canvas) {
    const context = canvas.getContext("2d", { alpha: true, desynchronized: true });
    const state = {
        canvas,
        context,
        particles: [],
        frame: 0,
        active: false,
        disposed: false,
        reducedMotion: window.matchMedia("(prefers-reduced-motion: reduce)").matches,
        width: 0,
        height: 0,
        pixelRatio: 1,
        bands: new Float32Array(0),
        targetBands: new Float32Array(0),
        releaseOverride: null,
        activationStartedAt: 0
    };

    resize(state);
    state.resizeObserver = new ResizeObserver(() => resize(state));
    state.resizeObserver.observe(canvas);
    state.frame = requestAnimationFrame(timestamp => draw(state, timestamp));
    return state;
}

function assignBands(state) {
    const bandCount = state.bands.length;
    for (const particle of state.particles)
        particle.bandPosition = particle.normalizedX * bandCount - 0.5;
}

function resize(state) {
    const bounds = state.canvas.getBoundingClientRect();
    const width = Math.max(1, Math.round(bounds.width));
    const height = Math.max(1, Math.round(bounds.height));
    const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);

    if (state.width === width && state.height === height && state.pixelRatio === pixelRatio)
        return;

    state.width = width;
    state.height = height;
    state.pixelRatio = pixelRatio;
    state.canvas.width = Math.round(width * pixelRatio);
    state.canvas.height = Math.round(height * pixelRatio);
    state.context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);

    const density = clamp(Math.round(width * 7.2), 760, 1280);
    const count = state.reducedMotion ? Math.min(760, density) : density;
    state.particles = Array.from({ length: count }, (_, index) => createParticle(index, count));
    assignBands(state);
}

function peakKernel(distance) {
    return distance >= peakHalfWidth ? 0 : Math.pow(1 - distance / peakHalfWidth, peakSharpness);
}

function peakLevelAt(bands, position) {
    const last = bands.length - 1;
    if (last < 0)
        return 0;

    const nearest = Math.round(position);
    let level = 0;
    for (let index = Math.max(nearest - 1, 0); index <= Math.min(nearest + 1, last); index++)
        level = Math.max(level, bands[index] * peakKernel(Math.abs(position - index)));

    return level;
}

function amplitudeAt(state, particle) {
    const availableHeight = state.height * 0.48;
    return 0.72 + availableHeight * peakLevelAt(state.bands, particle.bandPosition);
}

function followBands(state) {
    const attack = state.reducedMotion ? reducedAttackRate : attackRate;
    const release = state.reducedMotion ? reducedReleaseRate : state.releaseOverride ?? releaseRate;
    for (let index = 0; index < state.bands.length; index++) {
        const current = state.bands[index];
        const target = state.targetBands[index];
        state.bands[index] = current + (target - current) * (target > current ? attack : release);
    }
}

function draw(state, timestamp) {
    if (state.disposed)
        return;

    const context = state.context;
    context.clearRect(0, 0, state.width, state.height);

    if (state.active) {
        followBands(state);

        const centerY = state.height * 0.5;
        const horizontalInset = Math.min(4, state.width * 0.025);
        const drawableWidth = state.width - horizontalInset * 2;
        const activationElapsed = timestamp - state.activationStartedAt;
        const particleFollow = state.reducedMotion ? 0.1 : 0.42;

        context.globalCompositeOperation = "lighter";
        context.fillStyle = "rgb(255, 255, 255)";
        context.beginPath();

        for (const particle of state.particles) {
            const amplitude = amplitudeAt(state, particle);
            const targetX = horizontalInset + particle.normalizedX * drawableWidth;
            const targetY = centerY + particle.side * particle.spread * amplitude;

            const entranceProgress = state.reducedMotion
                ? 1
                : clamp((activationElapsed - particle.entranceDelay) / particle.entranceDuration);

            if (entranceProgress < 1) {
                const easedEntrance = 1 - Math.pow(1 - entranceProgress, 3);
                particle.x = particle.launchX + (targetX - particle.launchX) * easedEntrance;
                particle.y = particle.launchY + (targetY - particle.launchY) * easedEntrance;
            } else if (particle.x === 0 && particle.y === 0) {
                particle.x = targetX;
                particle.y = targetY;
            } else {
                particle.x += (targetX - particle.x) * particleFollow;
                particle.y += (targetY - particle.y) * particleFollow;
            }

            const radius = particle.radius * (0.72 + particle.depth * 0.46);
            context.moveTo(particle.x + radius, particle.y);
            context.arc(particle.x, particle.y, radius, 0, Math.PI * 2);
        }

        context.fill();
        context.globalCompositeOperation = "source-over";
    }

    state.frame = requestAnimationFrame(nextTimestamp => draw(state, nextTimestamp));
}

export function initialize(canvas) {
    if (!canvas || visualizers.has(canvas))
        return;

    visualizers.set(canvas, createState(canvas));
}

export function setActive(canvas, active) {
    const state = visualizers.get(canvas);
    if (!state)
        return;

    const isStarting = active && !state.active;
    state.active = active;
    state.canvas.classList.toggle("audio-equalizer-canvas--active", state.active);
    if (isStarting) {
        state.activationStartedAt = performance.now();
        const centerY = state.height * 0.5;
        for (const particle of state.particles) {
            const launchDistance = state.width * (0.08 + Math.random() * 0.3);
            particle.launchX = particle.entranceSide < 0
                ? -launchDistance
                : state.width + launchDistance;
            particle.launchY = centerY + (Math.random() - 0.5) * state.height * 0.64;
            particle.x = particle.launchX;
            particle.y = particle.launchY;
        }
    }
    if (!state.active) {
        state.bands.fill(0);
        state.targetBands.fill(0);
        driveSpeaker(null);
    }
}

/**
 * Feeds a frame of band levels (0..1, lows first). The optional speaker levels
 * override how the header speaker moves, for sources whose band layout does
 * not keep the bass on the left.
 */
export function update(canvas, bands, speakerLevels = null) {
    const isBandList = Array.isArray(bands) || ArrayBuffer.isView(bands);
    const state = visualizers.get(canvas);
    if (!state || !state.active || !isBandList)
        return;

    if (bands.length !== state.targetBands.length) {
        state.bands = new Float32Array(bands.length);
        state.targetBands = new Float32Array(bands.length);
        assignBands(state);
    }

    for (let index = 0; index < bands.length; index++)
        state.targetBands[index] = clamp(Number(bands[index]) || 0);
    driveSpeaker(state.targetBands, speakerLevels);
}

export function dispose(canvas) {
    const state = visualizers.get(canvas);
    if (!state)
        return;

    state.disposed = true;
    cancelAnimationFrame(state.frame);
    state.resizeObserver.disconnect();
    state.context.clearRect(0, 0, state.width, state.height);
    visualizers.delete(canvas);
}

/** Overrides how fast peaks fall on this canvas; null restores the default. */
export function setRelease(canvas, release) {
    const state = visualizers.get(canvas);
    if (state)
        state.releaseOverride = release ?? null;
}

export function setSpeaker(element) {
    speaker = element ?? null;
}

// Sets --speaker-bass and --speaker-high (0..1) on the header speaker; CSS
// turns them into the woofer and tweeter movement. Passing null rests it.
function driveSpeaker(bands, speakerLevels = null) {
    if (!speaker)
        return;

    let bass = speakerLevels?.bass ?? 0;
    let high = speakerLevels?.high ?? 0;
    if (!speakerLevels && bands && bands.length > 0) {
        const bassEnd = Math.max(1, Math.round(bands.length * 0.25));
        const highStart = Math.floor(bands.length * 0.65);
        for (let index = 0; index < bassEnd; index++)
            bass = Math.max(bass, bands[index]);
        for (let index = highStart; index < bands.length; index++)
            high = Math.max(high, bands[index]);
    }

    speaker.style.setProperty("--speaker-bass", clamp(bass).toFixed(3));
    speaker.style.setProperty("--speaker-high", clamp(high).toFixed(3));
}
