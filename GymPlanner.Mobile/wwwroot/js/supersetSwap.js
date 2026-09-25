// Перестановка упражнений суперсета: каждый подход, название и фото переезжает
// со старого места на новое (FLIP). Поднимающийся элемент приподнимается и идёт
// по дуге поверх опускающегося, тот приглушается; строки трогаются друг за другом.
//
// Переезд запускается из MutationObserver, как только Blazor перестроил карточку:
// его колбэк выполняется до отрисовки, поэтому кадра, где элементы уже стоят на
// новых местах, а анимация ещё не началась, не бывает — нет мерцания.
const pending = new Map();

const cardOf = groupId => document.querySelector(`[data-superset-id="${groupId}"]`);

const flipItems = card => [...card.querySelectorAll('[data-flip-key]')];

const reducedMotion = () => matchMedia('(prefers-reduced-motion: reduce)').matches;

function stopWaiting(groupId) {
    const entry = pending.get(groupId);
    if (!entry)
        return;

    entry.observer.disconnect();
    clearTimeout(entry.timeout);
    pending.delete(groupId);
}

export function capture(groupId) {
    stopWaiting(groupId);
    const card = cardOf(groupId);
    if (!card || reducedMotion())
        return;

    const rects = new Map();
    for (const element of flipItems(card))
        rects.set(element.dataset.flipKey, element.getBoundingClientRect());

    // Первые перерисовки могут ничего не двигать (например, кнопка становится
    // занятой), поэтому ждём ту, после которой элементы действительно сместились.
    const observer = new MutationObserver(() => {
        if (animate(card, rects) > 0)
            stopWaiting(groupId);
    });
    observer.observe(card, { childList: true, subtree: true, attributes: true, characterData: true });
    pending.set(groupId, { card, rects, observer, timeout: setTimeout(() => stopWaiting(groupId), 2000) });
}

// Запасной путь из OnAfterRenderAsync: если наблюдатель ещё ждёт, проигрываем сейчас.
export function play(groupId) {
    const entry = pending.get(groupId);
    if (entry && animate(entry.card, entry.rects) > 0)
        stopWaiting(groupId);
}

// Плотный фон с оттенком упражнения: 12% его цвета поверх приподнятой поверхности.
function raisedBackground(element) {
    const [r, g, b] = getComputedStyle(element).color.match(/[\d.]+/g).map(Number);
    const mix = (tone, base) => Math.round(base * 0.88 + tone * 0.12);
    return `rgb(${mix(r, 38)}, ${mix(g, 38)}, ${mix(b, 39)})`;
}

function animate(card, rects) {
    let order = 0;
    for (const element of flipItems(card)) {
        const before = rects.get(element.dataset.flipKey);
        if (!before)
            continue;

        const after = element.getBoundingClientRect();
        const dx = before.left - after.left;
        const dy = before.top - after.top;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1)
            continue;

        const rising = dy > 0;
        const swing = (rising ? -1 : 1) * Math.min(22, 8 + Math.abs(dy) * 0.06);
        const scale = rising ? 1.04 : 0.96;
        // Середина перелёта: поднимающаяся карточка плотнеет и отбрасывает тень,
        // опускающаяся приглушается. Начало и конец не заданы — браузер берёт
        // живые значения из CSS, поэтому ни в начале, ни в конце нет скачка.
        const middle = {
            transform: `translate(${dx / 2 + swing}px, ${dy / 2}px) scale(${scale})`,
            opacity: rising ? 1 : 0.35,
            offset: 0.5
        };
        if (rising && !element.dataset.flipKey.startsWith('name-')) {
            middle.backgroundColor = raisedBackground(element);
            middle.boxShadow = '0 12px 28px 0 rgba(0, 0, 0, .5)';
        }

        element.style.zIndex = rising ? '2' : '1';
        const animation = element.animate(
            [
                { transform: `translate(${dx}px, ${dy}px)` },
                middle,
                { transform: 'translate(0, 0)' }
            ],
            {
                duration: 620,
                delay: order++ * 45,
                easing: 'cubic-bezier(.3, .7, .2, 1)',
                fill: 'backwards'
            });
        animation.onfinish = animation.oncancel = () => element.style.removeProperty('z-index');
    }

    return order;
}
