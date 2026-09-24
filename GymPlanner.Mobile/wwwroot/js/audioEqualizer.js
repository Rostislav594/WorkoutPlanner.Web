const visualizers = new WeakMap();

function clamp(value, minimum = 0, maximum = 1) {
    return Math.min(maximum, Math.max(minimum, value));
}

function gaussian(value, center, width) {
    const distance = (value - center) / width;
    return Math.exp(-distance * distance);
}

function createParticle(index, count) {
    const normalizedX = count <= 1 ? 0.5 : index / (count - 1);
    const centered = normalizedX * 2 - 1;
    const absoluteX = Math.abs(centered);

    return {
        normalizedX,
        centered,
        bassZone: gaussian(absoluteX, 0, 0.16),
        lowMidZone: gaussian(absoluteX, 0.36, 0.09),
        midZone: gaussian(absoluteX, 0.62, 0.078),
        highZone: gaussian(absoluteX, 0.87, 0.062),
        taper: 1 - absoluteX * 0.32,
        side: Math.random() < 0.5 ? -1 : 1,
        entranceSide: normalizedX < 0.5 ? -1 : 1,
        entranceDelay: Math.random() * 120,
        entranceDuration: 580 + Math.random() * 260,
        spread: Math.pow(Math.random(), 1.28),
        depth: Math.random(),
        radius: 0.2 + Math.random() * 0.48,
        peakScale: 0.68 + Math.random() * 0.32,
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
        bass: 0,
        lowMid: 0,
        mid: 0,
        high: 0,
        beat: 0,
        level: 0,
        targetBass: 0,
        targetLowMid: 0,
        targetMid: 0,
        targetHigh: 0,
        targetBeat: 0,
        targetLevel: 0,
        activationStartedAt: 0
    };

    resize(state);
    state.resizeObserver = new ResizeObserver(() => resize(state));
    state.resizeObserver.observe(canvas);
    state.frame = requestAnimationFrame(timestamp => draw(state, timestamp));
    return state;
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
}

function amplitudeAt(state, particle) {
    const availableHeight = state.height * 0.39;
    const levelGate = clamp((state.level - 0.025) / 0.32);

    const bassResponse = state.bass * particle.bassZone * 1.08;
    const lowMidResponse = state.lowMid * particle.lowMidZone * 0.9;
    const midResponse = state.mid * particle.midZone * 0.74;
    const highResponse = state.high * particle.highZone * 0.6;
    const localSignal = Math.max(bassResponse, lowMidResponse, midResponse, highResponse);
    const localBeat = state.beat * particle.bassZone * 0.44;
    const response = clamp(
        localSignal * 2.85 * particle.taper * (0.16 + levelGate * 0.84) + localBeat,
        0,
        0.98);

    return 0.72 + availableHeight * response * particle.peakScale;
}

function followSignal(current, target, attack, release) {
    const response = target > current ? attack : release;
    return current + (target - current) * response;
}

function draw(state, timestamp) {
    if (state.disposed)
        return;

    const context = state.context;
    context.clearRect(0, 0, state.width, state.height);

    if (state.active) {
        const attack = state.reducedMotion ? 0.16 : 0.62;
        const release = state.reducedMotion ? 0.1 : 0.3;
        state.bass = followSignal(state.bass, state.targetBass, attack, release);
        state.lowMid = followSignal(state.lowMid, state.targetLowMid, attack, release);
        state.mid = followSignal(state.mid, state.targetMid, attack, release);
        state.high = followSignal(state.high, state.targetHigh, attack, release);
        state.level = followSignal(state.level, state.targetLevel, attack, release);
        state.beat = Math.max(state.targetBeat, state.beat * 0.76);
        state.targetBeat *= 0.48;

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
            const depthScale = 0.62 + particle.depth * 0.68;
            const targetX = horizontalInset + particle.normalizedX * drawableWidth;
            const targetY = centerY + particle.side * particle.spread * amplitude * depthScale;

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

            const radius = particle.radius * (0.72 + particle.depth * 0.46 + state.beat * 0.14);
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
        state.targetBass = 0;
        state.targetLowMid = 0;
        state.targetMid = 0;
        state.targetHigh = 0;
        state.targetBeat = 0;
        state.targetLevel = 0;
    }
}

export function update(canvas, bass, lowMid, mid, high, beat, level) {
    const state = visualizers.get(canvas);
    if (!state || !state.active)
        return;

    state.targetBass = clamp(bass);
    state.targetLowMid = clamp(lowMid);
    state.targetMid = clamp(mid);
    state.targetHigh = clamp(high);
    state.targetBeat = clamp(beat);
    state.targetLevel = clamp(level);
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
