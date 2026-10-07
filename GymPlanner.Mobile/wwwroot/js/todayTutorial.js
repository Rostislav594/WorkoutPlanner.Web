// Обучение: окно в краевом размытии и рука поверх любой страницы приложения.
//
// Разметку слоя рисует Blazor, сценарий ведёт C#. Здесь только то, чего Blazor
// не умеет: каждый кадр узнать, где сейчас на экране настоящий элемент, и
// подвинуть туда окно в размытии и руку. Элементы ищутся заново в каждом
// кадре, поэтому прожектор следует за ними при прокрутке, открытии окон и
// перестройке карточек без отдельной синхронизации.
//
// Вуаль с размытием дорого перерисовывать, поэтому стили пишутся
// только когда что-то сдвинулось хотя бы на пиксель: пока цель стоит, слой не
// перерисовывается вовсе. Сглаживание считается по времени, а не по кадрам —
// на медленном телефоне движение не растягивается.
let layer = null;
let veil = null;
let finger = null;
let frame = 0;
let lastTime = 0;

let focusSelector = null;
let fingerSelector = null;
let fingerVisible = false;

let hole = null;
let fingerPoint = null;
let applied = { hole: '', finger: '', fingerShown: null };
let pressAnimation = null;

const padding = 8;
// Постоянные времени сглаживания: за это время проходится ~63% пути.
const holeTau = 70;
const fingerTau = 150;

const reducedMotion = () => matchMedia('(prefers-reduced-motion: reduce)').matches;

// Видимый элемент по селектору: в DOM могут одновременно жить исчезающая
// и новая копия, нужна та, у которой есть размер.
function find(selector) {
    if (!selector)
        return null;

    for (const element of document.querySelectorAll(selector)) {
        const rect = element.getBoundingClientRect();
        if (rect.width > 0 && rect.height > 0)
            return element;
    }

    return null;
}

// Шаг к цели с поправкой на прошедшее время; у самой цели — точно в неё,
// иначе дробные значения менялись бы бесконечно и слой не переставал бы
// перерисовываться.
function approach(current, target, dt, tau) {
    if (!current || reducedMotion())
        return { ...target };

    const factor = 1 - Math.exp(-dt / tau);
    const next = {};
    for (const key of Object.keys(target)) {
        const value = current[key] + (target[key] - current[key]) * factor;
        next[key] = Math.abs(target[key] - value) < 0.5 ? target[key] : value;
    }
    return next;
}


function tick(time) {
    frame = requestAnimationFrame(tick);
    if (!layer?.isConnected)
        return;

    const dt = lastTime ? Math.min(100, time - lastTime) : 16;
    lastTime = time;
    const width = window.innerWidth;
    const height = window.innerHeight;

    const target = find(focusSelector);
    let wanted = null;
    if (target) {
        const rect = target.getBoundingClientRect();
        wanted = {
            x: rect.left - padding,
            y: rect.top - padding,
            w: rect.width + padding * 2,
            h: rect.height + padding * 2
        };
    } else if (hole) {
        // Без цели окно стягивается в точку там, где было, а не прыгает в угол.
        wanted = { x: hole.x + hole.w / 2, y: hole.y + hole.h / 2, w: 0, h: 0 };
    }

    if (wanted) {
        hole = approach(hole, wanted, dt, holeTau);
        // Второй слой маски вуали — эллипс с мягким краем над целью, он
        // вычитается из краевого размытия. Эллипс шире и выше цели, чтобы её
        // углы оставались в сплошной части, а граница была плавной.
        const w = Math.max(0, Math.round(hole.w * 1.5)), h = Math.max(0, Math.round(hole.h * 2));
        const x = Math.round(hole.x + hole.w / 2 - w / 2), y = Math.round(hole.y + hole.h / 2 - h / 2);
        const value = `${x},${y},${w},${h}`;
        if (value !== applied.hole) {
            veil.style.maskPosition = `0 0, ${x}px ${y}px`;
            veil.style.maskSize = `100% 100%, ${w}px ${h}px`;
            applied.hole = value;
        }
    }

    const pointed = fingerVisible ? find(fingerSelector) : null;
    if (pointed) {
        const rect = pointed.getBoundingClientRect();
        const wantedPoint = { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
        // Палец появляется снизу экрана, а дальше едет от цели к цели.
        fingerPoint = approach(fingerPoint ?? { x: width / 2, y: height + 40 }, wantedPoint, dt, fingerTau);
        // Позиция — свойством translate, а не transform: scale нажатия применяется
        // поверх transform и сжал бы заодно и сдвиг, палец уехал бы в угол.
        const fingerValue = `${Math.round(fingerPoint.x)}px ${Math.round(fingerPoint.y)}px`;
        if (fingerValue !== applied.finger) {
            finger.style.translate = fingerValue;
            applied.finger = fingerValue;
        }
    }

    // Цель могла исчезнуть сама — например, кнопка закрыла своё окно. Рука
    // тогда остаётся там, где нажала, до следующего шага, а не пропадает.
    const fingerShown = fingerVisible && !!fingerPoint;
    if (fingerShown !== applied.fingerShown) {
        finger.classList.toggle('tutorial-finger--visible', fingerShown);
        applied.fingerShown = fingerShown;
    }
}

export function attach(element) {
    layer = element;
    veil = element.querySelector('[data-tutorial-veil]');
    finger = element.querySelector('[data-tutorial-finger]');
    hole = null;
    fingerPoint = null;
    lastTime = 0;
    applied = { hole: '', finger: '', fingerShown: null };
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(tick);
}

// Ближайший прокручиваемый предок элемента — страница или окно с прокруткой.
function scrollerOf(element) {
    for (let node = element.parentElement; node; node = node.parentElement) {
        const overflow = getComputedStyle(node).overflowY;
        if ((overflow === 'auto' || overflow === 'scroll') && node.scrollHeight > node.clientHeight + 1)
            return node;
    }
    return document.scrollingElement;
}

// Своя плавная прокрутка вместо scrollIntoView: та слишком резкая, и за ней
// не уследить. Медленный разгон и торможение, длительность растёт с
// расстоянием. Обещание выполняется, когда страница остановилась.
//
// Цель пересчитывается в каждом кадре: пока страница едет, над элементом
// может вырасти содержимое (например, подпись оценки), и без этого прокрутка
// остановилась бы там, где элемент был в начале, — под нижним меню.
function scrollToCenter(element) {
    const box = scrollerOf(element);
    const view = box === document.scrollingElement
        ? { top: 0, height: window.innerHeight }
        : box.getBoundingClientRect();
    const target = () => {
        const rect = element.getBoundingClientRect();
        const max = box.scrollHeight - box.clientHeight;
        return Math.min(Math.max(box.scrollTop + (rect.top + rect.height / 2) - (view.top + view.height / 2), 0), max);
    };
    const from = box.scrollTop;
    const to = target();
    if (Math.abs(to - from) < 2)
        return Promise.resolve();

    if (reducedMotion()) {
        box.scrollTop = to;
        return Promise.resolve();
    }

    // Синусоида, а не кубическая кривая: пик скорости в середине вдвое ниже,
    // и на телефоне с ~25 кадрами в секунду страница не прыгает рывками.
    const duration = Math.min(2600, 900 + Math.abs(to - from) * 1.8);
    return new Promise(resolve => {
        const start = performance.now();
        const step = now => {
            const k = Math.min(1, Math.max(0, (now - start) / duration));
            const eased = (1 - Math.cos(Math.PI * k)) / 2;
            const end = element.isConnected ? target() : to;
            box.scrollTop = from + (end - from) * eased;
            if (k < 1)
                requestAnimationFrame(step);
            else
                resolve();
        };
        requestAnimationFrame(step);
    });
}

// Выводит элемент из-под краевого размытия и, если он у края экрана, плавно
// докручивает к нему; рука едет к fingerTarget, а без него прячется. Одним
// вызовом, чтобы шаг сценария стоил одного обращения к WebView. Сценарий
// ждёт конца прокрутки и только потом «нажимает». Возвращает false, если
// элемента нет: сценарий тогда идёт дальше без подсветки.
export async function focus(selector, fingerTarget) {
    focusSelector = selector;
    fingerSelector = fingerTarget ?? null;
    fingerVisible = !!fingerTarget;
    const element = find(selector);
    if (!element)
        return false;

    const rect = element.getBoundingClientRect();
    const margin = 110;
    if (rect.top < margin || rect.bottom > window.innerHeight - margin)
        await scrollToCenter(element);
    return true;
}

// Ждёт, пока элемент появится на экране, — например, пока после перехода
// загрузится страница. Не дождавшись, всё равно отпускает сценарий.
export function waitFor(selector, timeout) {
    return new Promise(resolve => {
        const deadline = performance.now() + timeout;
        const check = () => {
            if (find(selector) || performance.now() > deadline)
                resolve(!!find(selector));
            else
                setTimeout(check, 100);
        };
        check();
    });
}

// Обычный клик по настоящему элементу: срабатывает его собственный
// обработчик, ссылка ведёт на свою страницу, как от пальца пользователя.
export function click(selector) {
    const element = find(selector);
    element?.click();
    return !!element;
}

export function hideFinger() {
    fingerVisible = false;
}

export function tap() {
    if (!finger)
        return;

    finger.classList.remove('tutorial-finger--tap');
    // Перезапуск анимации: класс снимается и ставится в разных кадрах стиля.
    void finger.offsetWidth;
    finger.classList.add('tutorial-finger--tap');

    // Сам элемент отвечает на касание, как на настоящее: коротко пружинит.
    // Через scale, а не transform — свои transform у элементов не затираются.
    const element = find(fingerSelector);
    if (element && !reducedMotion()) {
        const depth = element.matches('input, textarea') ? .98 : .94;
        element.animate([
            { scale: '1', filter: 'brightness(1)' },
            { scale: String(depth), filter: 'brightness(1.25)', offset: .35 },
            { scale: '1', filter: 'brightness(1)' }
        ], { duration: 340, easing: 'ease-out', delay: 90 });
    }
}

// Удержание: рука и кнопка остаются вдавленными, пока идёт анимация завершения.
export function press(active) {
    finger?.classList.toggle('tutorial-finger--press', active);
    pressAnimation?.cancel();
    pressAnimation = null;
    const element = active ? find(fingerSelector) : null;
    if (element && !reducedMotion()) {
        pressAnimation = element.animate([
            { scale: '1', filter: 'brightness(1)' },
            { scale: '.97', filter: 'brightness(1.15)' }
        ], { duration: 160, easing: 'ease-out', fill: 'forwards' });
    }
}

export function clear() {
    focusSelector = null;
}

export function detach() {
    pressAnimation?.cancel();
    pressAnimation = null;
    cancelAnimationFrame(frame);
    frame = 0;
    layer = veil = finger = null;
    focusSelector = fingerSelector = null;
    fingerVisible = false;
    hole = fingerPoint = null;
}
