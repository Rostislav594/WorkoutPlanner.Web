// Проценты внутри столбиков графика прогресса.
//
// Надпись стоит вертикально и подогнана под свой столбик: шрифт ровно такой,
// чтобы текст целиком поместился по высоте и ширине. При масштабировании
// WebView двумя пальцами всё растёт одинаково, поэтому влезает ли надпись —
// от масштаба не зависит; меняется только её размер на экране. Как только
// он становится читаемым, надпись плавно проявляется, при уменьшении гаснет.

// Размер надписи на экране, в пикселях, между которыми она проявляется.
const READABLE_FROM = 8.5;
const READABLE_FULL = 11;
const MAX_FONT = 13;
// Поля от краёв столбика, чтобы надпись не стояла впритык.
const SIDE_PADDING = 3;
const END_PADDING = 8;

const charts = new Map();

// Длина вертикальной строки на 1px шрифта — меряется по самому элементу,
// с его шрифтом и моноширинными цифрами; поворот на offset* не влияет.
function textLengthPerPixel(label) {
    label.style.fontSize = '20px';
    return label.offsetHeight / 20;
}

function update(bars) {
    const scale = window.visualViewport?.scale ?? 1;
    for (const label of bars.querySelectorAll('.chart-bar-label')) {
        const bar = label.parentElement;
        const width = bar.offsetWidth - SIDE_PADDING * 2;
        const height = bar.offsetHeight - END_PADDING * 2;
        label.dataset.lengthPerPixel ??= String(textLengthPerPixel(label));
        const lengthPerPixel = Number(label.dataset.lengthPerPixel) || 1;

        // Строка вертикальная: её толщина — по ширине столбика, длина — по высоте.
        const font = Math.min(MAX_FONT, width / 1.2, height / lengthPerPixel);
        const onScreen = font * scale;
        const opacity = font <= 0
            ? 0
            : Math.min(1, Math.max(0, (onScreen - READABLE_FROM) / (READABLE_FULL - READABLE_FROM)));

        label.style.fontSize = `${Math.max(1, font).toFixed(2)}px`;
        label.style.opacity = opacity.toFixed(3);
    }
}

function schedule(bars) {
    const chart = charts.get(bars);
    if (!chart || chart.frame)
        return;
    chart.frame = requestAnimationFrame(() => {
        chart.frame = 0;
        update(bars);
    });
}

export function attach(bars) {
    if (!bars || charts.has(bars))
        return;

    const onViewport = () => schedule(bars);
    const observer = new ResizeObserver(onViewport);
    observer.observe(bars);
    window.visualViewport?.addEventListener('resize', onViewport);
    window.addEventListener('resize', onViewport);
    charts.set(bars, { observer, onViewport, frame: 0 });
    update(bars);
}

// После перерисовки Blazor тексты могли смениться: длину считаем заново.
export function refresh(bars) {
    if (!bars)
        return;
    for (const label of bars.querySelectorAll('.chart-bar-label'))
        delete label.dataset.lengthPerPixel;
    schedule(bars);
}

export function detach(bars) {
    const chart = charts.get(bars);
    if (!chart)
        return;

    chart.observer.disconnect();
    window.visualViewport?.removeEventListener('resize', chart.onViewport);
    window.removeEventListener('resize', chart.onViewport);
    cancelAnimationFrame(chart.frame);
    charts.delete(bars);
}
