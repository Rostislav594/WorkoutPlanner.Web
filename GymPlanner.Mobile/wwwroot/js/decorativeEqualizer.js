import { update as updateVisualizer, setRelease } from "./audioEqualizer.js";

// Decorative mode: no audio at all. A small generative dance "track" drives
// the equalizer's 20 bands. The groove sits in the middle (kick and bass
// centred, snare beside them) while hats, percussion, crashes and risers throw
// big peaks out to the edges too. Tempo, patterns and song sections change at
// random and each band's colour drifts on its own incommensurate sines, so the
// picture never settles into a visible loop.
const bandCount = 20;
const stepsPerBar = 16;
const center = (bandCount - 1) / 2;
const gain = 1.4;

// Faster falling peaks than the microphone mode, for a punchier picture.
const decorativeRelease = 0.22;

// The middle leads, but the edges keep more than half of their height.
const centerWeight = Array.from({ length: bandCount }, (_, band) =>
    0.6 + 0.4 * Math.exp(-(((band - center) / 5) ** 2)));

const sections = {
    groove: { kick: 1, snare: 1, hat: 0.9, bass: 1, lead: 0.8, perc: 0.1, pad: 0.3, bars: [4, 8] },
    peak: { kick: 1, snare: 1, hat: 1, bass: 1, lead: 1, perc: 0.18, pad: 0.45, bars: [4, 8] },
    breakdown: { kick: 0, snare: 0.35, hat: 0.5, bass: 0.6, lead: 1, perc: 0.08, pad: 0.8, bars: [2, 2, 4] },
    build: { kick: 0.7, snare: 1, hat: 1, bass: 0.8, lead: 0.7, perc: 0.12, pad: 0.5, bars: [2, 4] }
};

const nextSections = {
    groove: ["groove", "peak", "peak", "breakdown", "build"],
    peak: ["groove", "peak", "breakdown", "build"],
    breakdown: ["build", "build", "peak"],
    build: ["peak", "peak", "peak", "groove"]
};

const kickTemplates = [
    [0, 4, 8, 12],
    [0, 4, 8, 12],
    [0, 4, 8, 11, 12],
    [0, 3, 6, 8, 12, 14],
    [0, 4, 7, 8, 12, 15],
    [0, 6, 10, 12]
];

let run = null;

function pick(list) {
    return list[Math.floor(Math.random() * list.length)];
}

function between(minimum, maximum) {
    return minimum + Math.random() * (maximum - minimum);
}

function gaussian(position, peak, width) {
    const distance = (position - peak) / width;
    return Math.exp(-distance * distance);
}

function createPatterns(sectionName) {
    const kick = new Array(stepsPerBar).fill(0);
    for (const step of pick(kickTemplates))
        kick[step] = between(0.85, 1);

    const snare = new Array(stepsPerBar).fill(0);
    snare[4] = between(0.85, 1);
    snare[12] = between(0.85, 1);
    for (let step = 0; step < stepsPerBar; step++) {
        if (!snare[step] && Math.random() < 0.14)
            snare[step] = between(0.3, 0.6);
    }

    const hatDensity = sectionName === "breakdown" ? pick([2, 4]) : pick([1, 1, 2]);
    const hat = new Array(stepsPerBar).fill(0);
    for (let step = 0; step < stepsPerBar; step += hatDensity)
        hat[step] = step % 4 === 2 ? between(0.8, 1) : between(0.4, 0.8);

    const root = pick([1, 1.5, 2]);
    const bass = new Array(stepsPerBar).fill(null);
    for (let step = 0; step < stepsPerBar; step++) {
        const offBeat = step % 4 === 2 || (step % 4 === 3 && Math.random() < 0.4);
        if (offBeat || Math.random() < 0.2)
            bass[step] = { band: root + pick([0, 0, 0.8, 1.6, -0.6, 2.4]), velocity: between(0.65, 1) };
    }

    return { kick, snare, hat, bass };
}

function mutate(patterns) {
    for (let change = 0; change < 2; change++) {
        const step = Math.floor(Math.random() * stepsPerBar);
        if (Math.random() < 0.5 && step % 4 !== 0)
            patterns.kick[step] = patterns.kick[step] ? 0 : between(0.6, 0.95);
        if (Math.random() < 0.5)
            patterns.hat[step] = patterns.hat[step] ? 0 : between(0.4, 0.8);
    }
}

function crash(strength) {
    run.crash = Math.max(run.crash, strength);
}

function startSection(name) {
    const section = sections[name];
    run.section = name;
    run.sectionBars = pick(section.bars);
    run.sectionBarsLeft = run.sectionBars;
    run.patterns = createPatterns(name);
    if (name === "peak" || (name === "groove" && Math.random() < 0.6))
        crash(between(0.85, 1));
}

function triggerStep(step) {
    const section = sections[run.section];
    const { kick, snare, hat, bass } = run.patterns;
    const humanize = () => between(0.85, 1.12);
    const barsDone = run.sectionBars - run.sectionBarsLeft;

    if (kick[step] && section.kick > 0)
        run.kick = Math.max(run.kick, kick[step] * section.kick * humanize());

    let snareHit = snare[step] * section.snare;
    // A fill closes every fourth bar; build-ups roll ever faster to the end.
    if (barsDone % 4 === 3 && step >= 12 && Math.random() < 0.7)
        snareHit = Math.max(snareHit, between(0.5, 0.9));
    if (run.section === "build" && run.sectionBarsLeft <= 1 && step % 2 === 0)
        snareHit = Math.max(snareHit, between(0.5, 0.9));
    if (snareHit > 0)
        run.snare = Math.max(run.snare, snareHit * humanize());

    if (hat[step] && section.hat > 0) {
        run.hat = Math.max(run.hat, hat[step] * section.hat * humanize());
        run.hatDecay = Math.random() < 0.15 ? 6 : 24;
        run.hatSide = Math.random() < 0.5 ? -1 : 1;
    }

    if (bass[step] && section.bass > 0) {
        run.bass = bass[step].velocity * section.bass;
        run.bassBand = bass[step].band;
    }

    // Percussion lands anywhere across the width, edges included.
    if (Math.random() < section.perc) {
        run.percBand = between(0, bandCount - 1);
        run.perc = between(0.65, 1);
    }

    if (run.section === "peak" && step === 0 && barsDone % 4 === 0 && Math.random() < 0.5)
        crash(between(0.7, 0.95));

    // The melody roams the whole width and sometimes leaps far.
    if (Math.random() < 0.32 * section.lead + (step % 8 === 0 ? 0.3 : 0)) {
        run.leadBand += Math.random() < 0.2 ? pick([-6, -4, 4, 6]) : pick([-1.5, -0.8, 0.8, 1.5]);
        run.leadBand = Math.min(bandCount - 2, Math.max(1, run.leadBand));
        run.lead = between(0.6, 1) * section.lead;
    }
}

function advance(seconds) {
    // The tempo breathes slowly around the session's base tempo.
    const bpm = run.baseBpm + Math.sin(run.time * 0.05) * 3;
    const stepSeconds = 60 / bpm / 4;
    run.stepClock += seconds;
    while (run.stepClock >= stepSeconds) {
        run.stepClock -= stepSeconds;
        run.step = (run.step + 1) % stepsPerBar;
        if (run.step === 0) {
            run.sectionBarsLeft--;
            if (run.sectionBarsLeft <= 0)
                startSection(pick(nextSections[run.section]));
            else
                mutate(run.patterns);
        }
        triggerStep(run.step);
    }

    run.kick *= Math.exp(-seconds * 12);
    run.snare *= Math.exp(-seconds * 13);
    run.hat *= Math.exp(-seconds * run.hatDecay);
    run.bass *= Math.exp(-seconds * 5);
    run.lead *= Math.exp(-seconds * 3.2);
    run.perc *= Math.exp(-seconds * 9);
    run.crash *= Math.exp(-seconds * 1.8);
    run.pad += (sections[run.section].pad - run.pad) * Math.min(1, seconds * 0.8);

    // Risers sweep from both edges towards the middle across a build-up.
    if (run.section === "build") {
        const barProgress = (run.step + run.stepClock / stepSeconds) / stepsPerBar;
        const progress = Math.min(1, (run.sectionBars - run.sectionBarsLeft + barProgress) / run.sectionBars);
        run.riser = 0.35 + 0.6 * progress;
        run.riserOffset = (center - 0.5) * (1 - progress);
    } else {
        run.riser *= Math.exp(-seconds * 6);
    }
}

function compose() {
    const bands = run.bands;
    // Sidechain feel: the pad ducks under every kick.
    const pad = run.pad * (1 - 0.7 * Math.min(1, run.kick));
    const bassCenter = center + (run.bassBand - 1.5) * 2.2;
    const hatEdge = run.hatSide < 0 ? 1.5 : bandCount - 2.5;

    for (let band = 0; band < bandCount; band++) {
        const timbre = 0.8
            + 0.12 * Math.sin(run.time * run.drift[band].a + run.drift[band].phaseA)
            + 0.08 * Math.sin(run.time * run.drift[band].b + run.drift[band].phaseB);
        const mirror = 2 * center - band;
        const edgeness = Math.abs(band - center) / center;

        const parts = [
            run.kick * gaussian(band, center, 1.8),
            run.bass * 0.9 * gaussian(band, bassCenter, 1.1),
            run.bass * 0.4 * gaussian(mirror, bassCenter, 1.1),
            run.snare * 0.85 * (gaussian(band, center - 3.5, 1.6) + gaussian(band, center + 3.5, 1.6)),
            run.snare * 0.3 * (gaussian(band, 2, 2) + gaussian(band, bandCount - 3, 2)),
            run.hat * (0.95 * gaussian(band, hatEdge, 1.4) + 0.6 * gaussian(mirror, hatEdge, 1.4)),
            run.perc * gaussian(band, run.percBand, 0.9),
            run.lead * 0.85 * gaussian(band, run.leadBand, 0.8),
            run.lead * 0.5 * gaussian(mirror, run.leadBand, 0.8),
            run.crash * (0.35 + 0.55 * edgeness),
            run.riser * (gaussian(band, center - run.riserOffset, 1.2) + gaussian(band, center + run.riserOffset, 1.2)),
            pad * 0.3 * gaussian(band, center, 4.5)
        ];

        // Soft OR: overlapping parts add up without running past 1.
        let quiet = 1;
        for (const part of parts)
            quiet *= 1 - Math.min(1, part * timbre * centerWeight[band] * gain);
        bands[band] = Math.min(1, 1 - quiet + Math.random() * 0.05);
    }

    return bands;
}

function tick(timestamp) {
    if (!run)
        return;

    // A hidden page stops animation frames; do not jump after it resumes.
    const seconds = Math.min(0.05, Math.max(0, (timestamp - run.lastTimestamp) / 1000));
    run.lastTimestamp = timestamp;
    run.time += seconds;
    advance(seconds);
    const bands = compose();
    run.speaker.bass = Math.min(1, Math.max(run.kick, run.bass * 0.8, run.crash * 0.6));
    run.speaker.high = Math.min(1, Math.max(run.hat, run.snare * 0.6, run.perc * 0.7));
    updateVisualizer(run.canvas, bands, run.speaker);
    run.frame = requestAnimationFrame(tick);
}

export function start(canvas) {
    setRelease(canvas, decorativeRelease);
    if (run) {
        run.canvas = canvas;
        return;
    }

    run = {
        canvas,
        frame: 0,
        lastTimestamp: performance.now(),
        time: between(0, 1000),
        baseBpm: between(122, 138),
        stepClock: 0,
        step: -1,
        section: "groove",
        sectionBars: 0,
        sectionBarsLeft: 0,
        patterns: null,
        kick: 0,
        snare: 0,
        hat: 0,
        hatDecay: 24,
        hatSide: 1,
        bass: 0,
        bassBand: 1,
        lead: 0,
        leadBand: center + 2,
        perc: 0,
        percBand: 0,
        crash: 0,
        riser: 0,
        riserOffset: 0,
        pad: 0.3,
        bands: new Float32Array(bandCount),
        speaker: { bass: 0, high: 0 },
        drift: Array.from({ length: bandCount }, () => ({
            a: between(0.07, 0.23),
            b: between(0.29, 0.61),
            phaseA: between(0, Math.PI * 2),
            phaseB: between(0, Math.PI * 2)
        }))
    };
    startSection("groove");
    run.frame = requestAnimationFrame(tick);
}

export function stop() {
    if (!run)
        return;

    cancelAnimationFrame(run.frame);
    setRelease(run.canvas, null);
    run = null;
}
