let activeElement = null;
let activeCoach = null;
let dotNetReference = null;
let resizeObserver = null;
let frameId = 0;
let generation = 0;
let activePreparationId = null;
let focusTrapActive = false;
let previousFocus = null;
let allowTargetFocus = false;

export function prepare() {
    generation++;
    clearObservers();
    startFocusTrap();
    document.documentElement.classList.add("app-guide-active");
}

export async function showTarget(
    targetName,
    preferredPlacement,
    allowInteraction,
    reference,
    preparationId) {
    const callGeneration = ++generation;
    clearObservers();
    dotNetReference = reference;

    const element = await waitForTarget(targetName, 18, 80);
    if (!element || callGeneration !== generation) {
        return null;
    }

    const initialRect = element.getBoundingClientRect();
    const outsideViewport = initialRect.top < 12 ||
        initialRect.bottom > window.innerHeight - 12 ||
        initialRect.left < 8 ||
        initialRect.right > window.innerWidth - 8;

    if (outsideViewport) {
        element.scrollIntoView({
            behavior: "smooth",
            block: window.innerWidth <= 700 ? "start" : "center",
            inline: "nearest"
        });
        await delay(260);

        if (callGeneration !== generation) {
            return null;
        }

        if (window.innerWidth <= 700) {
            window.scrollBy({ top: -14, behavior: "auto" });
        }
    }

    activeElement = element;
    allowTargetFocus = allowInteraction;
    activeCoach = document.querySelector(".guide-coach");
    activePreparationId = preparationId;
    document.documentElement.classList.add("app-guide-active");

    window.addEventListener("resize", scheduleUpdate, { passive: true });
    window.addEventListener("scroll", scheduleUpdate, { passive: true, capture: true });

    resizeObserver = new ResizeObserver(scheduleUpdate);
    resizeObserver.observe(element);
    if (activeCoach) {
        resizeObserver.observe(activeCoach);
    }

    return measureTarget(element, preferredPlacement);
}

export function showOverview(reference, preparationId) {
    generation++;
    clearObservers();
    dotNetReference = reference;
    activePreparationId = preparationId;
    activeCoach = document.querySelector(".guide-coach");
    window.scrollTo({ top: 0, left: 0, behavior: "auto" });
    document.documentElement.classList.add("app-guide-active");
}

export function focusFirstControl() {
    document.querySelector(
        ".guide-choice:not(:disabled), .guide-button-primary:not(:disabled), .guide-button-secondary:not(:disabled)")
        ?.focus({ preventScroll: true });
}

export function stop() {
    generation++;
    clearObservers();
    stopFocusTrap();
    document.documentElement.classList.remove("app-guide-active");
}

function clearObservers() {
    window.removeEventListener("resize", scheduleUpdate);
    window.removeEventListener("scroll", scheduleUpdate, true);

    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }

    if (frameId) {
        cancelAnimationFrame(frameId);
        frameId = 0;
    }

    activeElement = null;
    activeCoach = null;
    dotNetReference = null;
    activePreparationId = null;
    allowTargetFocus = false;
}

function startFocusTrap() {
    if (focusTrapActive) {
        return;
    }

    focusTrapActive = true;
    previousFocus = document.activeElement;
    document.addEventListener("focusin", keepFocusInside, true);
    document.addEventListener("keydown", trapTabKey, true);
}

function stopFocusTrap() {
    if (!focusTrapActive) {
        return;
    }

    focusTrapActive = false;
    document.removeEventListener("focusin", keepFocusInside, true);
    document.removeEventListener("keydown", trapTabKey, true);

    if (previousFocus?.isConnected && typeof previousFocus.focus === "function") {
        previousFocus.focus({ preventScroll: true });
    }

    previousFocus = null;
}

function keepFocusInside(event) {
    const guide = document.querySelector(".app-guide");
    if (!guide || guide.contains(event.target) ||
        (allowTargetFocus && activeElement?.contains(event.target))) {
        return;
    }

    getGuideControls()[0]?.focus({ preventScroll: true });
}

function trapTabKey(event) {
    if (event.key !== "Tab") {
        return;
    }

    const controls = getGuideControls();
    if (!controls.length) {
        event.preventDefault();
        return;
    }

    const currentIndex = controls.indexOf(document.activeElement);
    const nextIndex = event.shiftKey
        ? (currentIndex <= 0 ? controls.length - 1 : currentIndex - 1)
        : (currentIndex === controls.length - 1 ? 0 : currentIndex + 1);

    event.preventDefault();
    controls[nextIndex].focus();
}

function getGuideControls() {
    const controls = Array.from(document.querySelectorAll(
        ".app-guide button:not(:disabled), .app-guide [tabindex]:not([tabindex='-1'])"));

    if (allowTargetFocus && activeElement) {
        const selector = "button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex]:not([tabindex='-1'])";
        if (activeElement.matches(selector)) {
            controls.push(activeElement);
        }
        controls.push(...activeElement.querySelectorAll(selector));
    }

    return [...new Set(controls)];
}

function scheduleUpdate() {
    if (!activeElement || !dotNetReference || frameId) {
        return;
    }

    const callbackGeneration = generation;
    const callbackElement = activeElement;
    const callbackReference = dotNetReference;
    const callbackPreparationId = activePreparationId;

    frameId = requestAnimationFrame(async () => {
        frameId = 0;

        if (callbackGeneration !== generation ||
            !callbackElement?.isConnected ||
            callbackReference !== dotNetReference) {
            return;
        }

        try {
            await callbackReference.invokeMethodAsync(
                "OnTargetChanged",
                measureTarget(callbackElement, "auto"),
                callbackPreparationId);
        } catch {
            if (callbackGeneration === generation &&
                callbackReference === dotNetReference) {
                clearObservers();
            }
        }
    });
}

async function waitForTarget(targetName, attempts, interval) {
    for (let attempt = 0; attempt < attempts; attempt++) {
        const escapedTarget = globalThis.CSS?.escape
            ? CSS.escape(targetName)
            : targetName.replace(/["\\]/g, "\\$&");
        const element = document.querySelector(`[data-guide="${escapedTarget}"]`);

        if (element) {
            return element;
        }

        await delay(interval);
    }

    return null;
}

function measureTarget(element, preferredPlacement) {
    const rect = element.getBoundingClientRect();
    const padding = 8;
    const x = clamp(rect.left - padding, 4, window.innerWidth - 8);
    const y = clamp(rect.top - padding, 4, window.innerHeight - 8);
    const width = Math.max(8, Math.min(rect.width + padding * 2, window.innerWidth - x - 4));
    const height = Math.max(8, Math.min(rect.height + padding * 2, window.innerHeight - y - 4));
    const coach = calculateCoachPosition(
        { x, y, width, height },
        preferredPlacement);

    return {
        x,
        y,
        width,
        height,
        coachLeft: coach.left,
        coachTop: coach.top
    };
}

function calculateCoachPosition(target, preferredPlacement) {
    const safeInsets = getSafeInsets();
    const marginLeft = Math.max(12, safeInsets.left + 8);
    const marginRight = Math.max(12, safeInsets.right + 8);
    const marginTop = Math.max(12, safeInsets.top + 8);
    const marginBottom = Math.max(12, safeInsets.bottom + 8);
    const gap = 18;
    const mobile = window.innerWidth <= 700;
    const coachRect = activeCoach?.getBoundingClientRect();
    const coachWidth = Math.min(
        coachRect?.width || (mobile ? 390 : 560),
        window.innerWidth - marginLeft - marginRight);
    const coachHeight = Math.min(
        coachRect?.height || (mobile ? 220 : 270),
        window.innerHeight - marginTop - marginBottom);
    const spaces = {
        left: target.x - marginLeft,
        right: window.innerWidth - marginRight - target.x - target.width,
        above: target.y - marginTop,
        below: window.innerHeight - marginBottom - target.y - target.height
    };

    let placement = preferredPlacement;
    if (placement === "auto" || placement === "center") {
        placement = mobile
            ? (spaces.below >= spaces.above ? "below" : "above")
            : (spaces.right >= spaces.left ? "right" : "left");
    }

    const needed = placement === "left" || placement === "right"
        ? coachWidth + gap
        : coachHeight + gap;
    if (spaces[placement] < needed) {
        placement = mobile
            ? (spaces.below >= spaces.above ? "below" : "above")
            : Object.entries(spaces).sort((a, b) => b[1] - a[1])[0][0];
    }

    let left = target.x + target.width / 2 - coachWidth / 2;
    let top = target.y + target.height + gap;

    if (placement === "above") {
        top = target.y - coachHeight - gap;
    } else if (placement === "left") {
        left = target.x - coachWidth - gap;
        top = target.y + target.height / 2 - coachHeight / 2;
    } else if (placement === "right") {
        left = target.x + target.width + gap;
        top = target.y + target.height / 2 - coachHeight / 2;
    }

    return {
        left: clamp(left, marginLeft, window.innerWidth - coachWidth - marginRight),
        top: clamp(top, marginTop, window.innerHeight - coachHeight - marginBottom)
    };
}

function getSafeInsets() {
    const guide = document.querySelector(".app-guide");
    if (!guide) {
        return { top: 0, right: 0, bottom: 0, left: 0 };
    }

    const styles = getComputedStyle(guide);
    return {
        top: parseFloat(styles.getPropertyValue("--guide-safe-top")) || 0,
        right: parseFloat(styles.getPropertyValue("--guide-safe-right")) || 0,
        bottom: parseFloat(styles.getPropertyValue("--guide-safe-bottom")) || 0,
        left: parseFloat(styles.getPropertyValue("--guide-safe-left")) || 0
    };
}

function clamp(value, minimum, maximum) {
    return Math.min(Math.max(value, minimum), Math.max(minimum, maximum));
}

function delay(milliseconds) {
    return new Promise(resolve => setTimeout(resolve, milliseconds));
}
