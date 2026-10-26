// Ручной перенос уведомления в архив. Долгое нажатие поднимает карточку, её
// ведут пальцем к коробке «Архив» внизу окна и отпускают над ней. Обычный тап
// по-прежнему открывает сообщение, а движение до поднятия — прокрутка списка.
//
// Все обработчики висят на самом окне и пропадают вместе с ним. Палец после
// поднятия захвачен окном, поэтому события идут сюда, даже если он ушёл за край.
const HOLD_MS = 380;
const MOVE_TOLERANCE = 8;
const EDGE = 56;
const DROP_SLACK = 18;

const reducedMotion = () => matchMedia('(prefers-reduced-motion: reduce)').matches;

let cleanupActive = null;

export function attach(menu, dotnet) {
    cleanupActive?.();

    let pending = null;
    let drag = null;
    let suppressClickUntil = 0;

    const archiveBox = () => menu.querySelector('.inbox-menu__archive');

    const cancelPending = () => {
        if (pending)
            clearTimeout(pending.timer);
        pending = null;
    };

    const onPointerDown = event => {
        if (event.button > 0 || drag || menu.hasAttribute('data-archive-disabled'))
            return;
        const item = event.target.closest('[data-inbox-id]');
        if (!item || event.target.closest('.inbox-menu__delete'))
            return;

        cancelPending();
        pending = {
            item,
            pointerId: event.pointerId,
            x: event.clientX,
            y: event.clientY,
            timer: setTimeout(() => pickUp(), HOLD_MS)
        };
    };

    function pickUp() {
        const { item, pointerId, x, y } = pending;
        pending = null;
        if (!item.isConnected)
            return;

        const rect = item.getBoundingClientRect();
        // Окно размыто через backdrop-filter, а такой элемент становится опорой
        // для position: fixed. Поэтому призрак живёт в body, а не в окне.
        const ghost = item.cloneNode(true);
        ghost.classList.add('inbox-menu__item--ghost');
        ghost.removeAttribute('data-inbox-id');
        Object.assign(ghost.style, {
            left: `${rect.left}px`,
            top: `${rect.top}px`,
            width: `${rect.width}px`,
            height: `${rect.height}px`
        });
        document.body.appendChild(ghost);
        item.classList.add('inbox-menu__item--lifted');
        archiveBox()?.classList.add('inbox-menu__archive--ready');

        try { menu.setPointerCapture(pointerId); } catch { /* палец уже отпущен */ }
        try { navigator.vibrate?.(12); } catch { /* вибрация недоступна */ }

        drag = { item, ghost, pointerId, startX: x, startY: y, x, y, over: false, frame: 0 };
        drag.frame = requestAnimationFrame(autoScroll);
    }

    const onPointerMove = event => {
        if (pending && event.pointerId === pending.pointerId) {
            if (Math.hypot(event.clientX - pending.x, event.clientY - pending.y) > MOVE_TOLERANCE)
                cancelPending();
            return;
        }
        if (!drag || event.pointerId !== drag.pointerId)
            return;

        drag.x = event.clientX;
        drag.y = event.clientY;
        const dx = drag.x - drag.startX;
        const dy = drag.y - drag.startY;
        drag.ghost.style.transform = `translate(${dx}px, ${dy}px) scale(1.03)`;
        updateTarget();
    };

    function updateTarget() {
        const box = archiveBox();
        if (!box)
            return;
        const r = box.getBoundingClientRect();
        const over = drag.x >= r.left - DROP_SLACK && drag.x <= r.right + DROP_SLACK &&
            drag.y >= r.top - DROP_SLACK && drag.y <= r.bottom + DROP_SLACK;
        if (over !== drag.over) {
            drag.over = over;
            box.classList.toggle('inbox-menu__archive--target', over);
            drag.ghost.classList.toggle('inbox-menu__item--ghost-over', over);
        }
    }

    // У края окна список сам едет, чтобы коробка показалась и при длинном списке.
    function autoScroll() {
        if (!drag)
            return;
        const r = menu.getBoundingClientRect();
        let step = 0;
        if (drag.y > r.bottom - EDGE)
            step = Math.min(14, (drag.y - (r.bottom - EDGE)) / 3);
        else if (drag.y < r.top + EDGE)
            step = -Math.min(14, ((r.top + EDGE) - drag.y) / 3);
        if (step !== 0) {
            menu.scrollTop += step;
            updateTarget();
        }
        drag.frame = requestAnimationFrame(autoScroll);
    }

    const onPointerUp = event => {
        if (pending && event.pointerId === pending.pointerId) {
            cancelPending();
            return;
        }
        if (!drag || event.pointerId !== drag.pointerId)
            return;
        finish(drag.over && event.type === 'pointerup');
    };

    function finish(archive) {
        const { item, ghost, frame } = drag;
        drag = null;
        cancelAnimationFrame(frame);
        suppressClickUntil = Date.now() + 500;
        const box = archiveBox();
        box?.classList.remove('inbox-menu__archive--ready', 'inbox-menu__archive--target');

        const duration = reducedMotion() ? 0 : 220;
        let end;
        if (archive && box) {
            const g = ghost.getBoundingClientRect();
            const b = box.getBoundingClientRect();
            const current = new DOMMatrix(getComputedStyle(ghost).transform);
            const tx = current.m41 + (b.left + b.width / 2) - (g.left + g.width / 2);
            const ty = current.m42 + (b.top + b.height / 2) - (g.top + g.height / 2);
            end = { transform: `translate(${tx}px, ${ty}px) scale(.12)`, opacity: 0 };
        } else {
            end = { transform: 'translate(0, 0) scale(1)', opacity: 1 };
        }

        const done = () => {
            ghost.remove();
            if (!archive) {
                item.classList.remove('inbox-menu__item--lifted');
                return;
            }
            // Сервер может не ответить — тогда карточка возвращается на место.
            dotnet.invokeMethodAsync('ArchiveDroppedAsync', Number(item.dataset.inboxId), item.dataset.inboxPublication === 'true')
                .catch(() => false)
                .finally(() => item.classList.remove('inbox-menu__item--lifted'));
        };

        if (duration === 0) {
            done();
            return;
        }
        const animation = ghost.animate([end], { duration, easing: 'cubic-bezier(.3, .7, .2, 1)', fill: 'forwards' });
        animation.onfinish = animation.oncancel = done;
    }

    // Пока карточка в руке, страница не должна прокручиваться пальцем.
    const onTouchMove = event => {
        if (drag)
            event.preventDefault();
    };

    // Долгое нажатие в WebView открывает системное меню — здесь оно не нужно.
    const onContextMenu = event => {
        if (event.target.closest('[data-inbox-id]'))
            event.preventDefault();
    };

    // Отпущенная карточка не должна заодно открыть сообщение.
    const onClick = event => {
        if (Date.now() < suppressClickUntil) {
            event.preventDefault();
            event.stopPropagation();
        }
    };

    menu.addEventListener('pointerdown', onPointerDown);
    menu.addEventListener('pointermove', onPointerMove);
    menu.addEventListener('pointerup', onPointerUp);
    menu.addEventListener('pointercancel', onPointerUp);
    menu.addEventListener('touchmove', onTouchMove, { passive: false });
    menu.addEventListener('contextmenu', onContextMenu);
    menu.addEventListener('click', onClick, true);

    cleanupActive = () => {
        cancelPending();
        if (drag) {
            cancelAnimationFrame(drag.frame);
            drag.ghost.remove();
            drag = null;
        }
        menu.removeEventListener('pointerdown', onPointerDown);
        menu.removeEventListener('pointermove', onPointerMove);
        menu.removeEventListener('pointerup', onPointerUp);
        menu.removeEventListener('pointercancel', onPointerUp);
        menu.removeEventListener('touchmove', onTouchMove);
        menu.removeEventListener('contextmenu', onContextMenu);
        menu.removeEventListener('click', onClick, true);
        cleanupActive = null;
    };
}

// Окно закрыли: убираем обработчики и призрак, если карточку бросили на полпути.
export function detach() {
    cleanupActive?.();
}
