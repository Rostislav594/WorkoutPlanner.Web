// Обучение на странице «Сегодня»: окно в краевом размытии и рука.
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
let chrome = null;
let frame = 0;
let lastTime = 0;

let focusSelector = null;
let fingerSelector = null;
let fingerVisible = false;

let hole = null;
let fingerPoint = null;
let applied = { hole: '', finger: '', fingerShown: null, top: null };
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

        // Подпись уходит наверх, когда цель в нижней части экрана.
        const top = hole.y + hole.h / 2 > height * 0.55;
        if (top !== applied.top) {
            chrome.classList.toggle('tutorial-chrome--top', top);
            applied.top = top;
        }
    }

    const pointed = fingerVisible ? find(fingerSelector) : null;
    placeBubble(target, width, height);
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

// Закрыта ли цель чем-то со страницы — например, окном, которое открыло её
// же нажатие. Слой обучения не в счёт. Проверяется в том же кадре, в котором
// окно появилось, иначе облачко успело бы мелькнуть поверх его полей.
function isCovered(target) {
    const rect = target.getBoundingClientRect();
    const x = Math.min(Math.max(rect.left + rect.width / 2, 0), window.innerWidth - 1);
    const y = Math.min(Math.max(rect.top + rect.height / 2, 0), window.innerHeight - 1);
    for (const element of document.elementsFromPoint(x, y)) {
        if (layer.contains(element))
            continue;
        return !(element === target || target.contains(element) || element.contains(target));
    }
    return false;
}

// Облачко подсказки — над целью, по горизонтали у руки, стрелкой к ней.
// Если сверху не хватает места, облачко встаёт под руку. Пока цель закрыта
// открывшимся окном или исчезла после нажатия, облачко прячется до
// следующей цели: иначе оно легло бы на поля окна.
function placeBubble(target, width, height) {
    const bubble = layer.querySelector('[data-tutorial-bubble]');
    if (!bubble)
        return;

    // Подсветку сняли — обучение закрывается: облачко гаснет там, где стояло,
    // а не перескакивает к руке поверх окна.
    if (!focusSelector && bubble.dataset.placed)
        return;

    const covered = focusSelector ? !target || isCovered(target) : false;
    if (bubble.classList.contains('tutorial-bubble--covered') !== covered)
        bubble.classList.toggle('tutorial-bubble--covered', covered);
    if (covered)
        return;

    const anchorX = fingerPoint?.x ?? width / 2;
    const rect = target?.getBoundingClientRect();
    // Заголовок группы полей выступает над её рамкой — облачко встаёт выше него.
    const legend = target?.querySelector(':scope > legend');
    const top = rect ? Math.min(rect.top, legend?.getBoundingClientRect().top ?? rect.top) : null;
    const bubbleWidth = bubble.offsetWidth;
    const bubbleHeight = bubble.offsetHeight;
    const margin = 12;
    const x = Math.round(Math.min(Math.max(anchorX - bubbleWidth / 2, margin), width - bubbleWidth - margin));
    let y = (top ?? fingerPoint?.y ?? height / 2) - bubbleHeight - 14;
    const below = y < margin + 4;
    if (below)
        y = (fingerPoint?.y ?? height / 2) + 62;
    y = Math.round(Math.min(y, height - bubbleHeight - margin));

    const arrow = Math.round(Math.min(Math.max(anchorX - x, 16), bubbleWidth - 16));
    const value = `${x},${y},${arrow},${below}`;
    if (bubble.dataset.placed === value)
        return;

    bubble.dataset.placed = value;
    bubble.style.translate = `${x}px ${y}px`;
    bubble.style.setProperty('--tutorial-bubble-arrow', `${arrow}px`);
    bubble.classList.toggle('tutorial-bubble--below', below);
}

export function attach(element) {
    layer = element;
    veil = element.querySelector('[data-tutorial-veil]');
    finger = element.querySelector('[data-tutorial-finger]');
    chrome = element.querySelector('[data-tutorial-chrome]');
    hole = null;
    fingerPoint = null;
    lastTime = 0;
    applied = { hole: '', finger: '', fingerShown: null, top: null };
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(tick);
}

// Выводит элемент из-под краевого размытия и, если он у края экрана, плавно
// докручивает к нему; рука едет к fingerTarget, а без него прячется. Одним вызовом, чтобы шаг
// сценария стоил одного обращения к WebView. Возвращает false, если элемента
// нет: сценарий тогда идёт дальше без подсветки.
export function focus(selector, fingerTarget) {
    focusSelector = selector;
    fingerSelector = fingerTarget ?? null;
    fingerVisible = !!fingerTarget;
    const element = find(selector);
    if (!element)
        return false;

    const rect = element.getBoundingClientRect();
    const margin = 110;
    if (rect.top < margin || rect.bottom > window.innerHeight - margin)
        element.scrollIntoView({ block: 'center', behavior: reducedMotion() ? 'auto' : 'smooth' });
    return true;
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
    layer = veil = finger = chrome = null;
    focusSelector = fingerSelector = null;
    fingerVisible = false;
    hole = fingerPoint = null;
}
