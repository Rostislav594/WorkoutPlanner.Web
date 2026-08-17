---
name: ui-ux-audit
description: Проводит системный UI/UX-аудит существующего приложения, выявляет визуальные, адаптивные, навигационные, accessibility и usability-проблемы, находит дубли UI/CSS/поведения и выполняет контролируемый рефакторинг с обязательной повторной проверкой интерфейса.
---

# UI/UX Audit

## Цель
Проводить системное улучшение интерфейса: понять текущий UI, воспроизвести пользовательские сценарии, найти и приоритизировать проблемы, исправить их небольшими итерациями и повторно проверить без нарушения бизнес-логики.

## Рабочий процесс
1. Прочитать применимые `AGENTS.md` и изучить архитектуру UI, layout, навигацию, страницы, компоненты, CSS, дизайн-токены, формы, модальные окна, состояния loading/empty/error, JS Interop и тесты.
2. Если среда позволяет — собрать и запустить приложение. Использовать Playwright или другой браузерный инструмент, если доступен.
3. Проверять реальные пользовательские flow, а не только исходный код.
4. Проверять Desktop и Mobile отдельно.
5. После аудита классифицировать проблемы как Critical, High, Medium или Low.
6. Исправлять небольшими логическими итерациями.
7. После каждой существенной итерации повторно проходить затронутый сценарий.
8. В конце выполнить regression-проверку.

## Категории аудита
Проверять:
- визуальную иерархию и заметность CTA;
- typography, line-height, переносы и контраст;
- padding, margin, gap и вертикальный rhythm;
- цвета и состояния success/warning/destructive/disabled/hover/focus/selected;
- консистентность карточек, кнопок, toolbar, modal и empty states;
- responsive layout, overflow, fixed/min widths, flex/grid wrapping;
- loading, empty, error, success, disabled, submitting и unauthorized states;
- формы, labels, validation, double submit и server errors;
- навигацию, Back/browser back, deep links, refresh и возврат после сохранения;
- semantic HTML, ARIA, focus, keyboard navigation и accessibility;
- hover/active/focus/pressed/selected/expanded states;
- анимации и layout shift;
- ненужные рендеры, тяжёлые изображения и JS listeners.

## Desktop
Проверить широкие экраны, визуальную иерархию, пустые зоны, растянутые блоки, ширину карточек, размеры текста и расположение действий.

## Mobile
Проверить вертикальную ориентацию, horizontal overflow, touch targets, CTA, modal, sticky/fixed элементы, safe area, bottom navigation, длинные названия, viewport height и виртуальную клавиатуру.

Не считать Desktop-реализацию автоматически корректной для Mobile.

## Поиск дублей
Искать не только буквальные дубли кода.

### UI-дубли
- одинаковые карточки с разной реализацией;
- одинаковые кнопки с разными классами;
- повторяющиеся toolbar/modal/empty states.

### CSS-дубли
- одинаковые наборы свойств;
- почти идентичные классы;
- повторяющиеся breakpoint rules, цвета, radius, shadow и магические размеры.

### Поведенческие дубли
- одинаковая логика modal, submit, loading, toast, navigation и JS.

Не объединять компоненты только ради уменьшения количества файлов. Рефакторинг должен давать реальную пользу.

## Responsive
Особое внимание уделять:
```css
width
min-width
max-width
height
100vh
position: fixed
position: sticky
overflow
flex-shrink
flex-wrap
grid-template-columns
```
Избегать решений, работающих только на одном разрешении.

## Accessibility
Проверять semantic HTML, labels, `aria-label`, `aria-expanded`, `aria-controls`, `aria-current`, focus state, tab order, keyboard navigation, contrast, icon-only buttons и modal focus. Если доступен axe-core — использовать его, но не считать автоматический audit заменой ручной UX-проверки.

## Приоритизация
### Critical
Ломает сценарий: недоступная кнопка, невозможность сохранить, сломанный modal, контент вне viewport.

### High
Сильно ухудшает UX: непонятный CTA, неудобная навигация, слишком мелкий текст, важный элемент визуально теряется.

### Medium
Визуальная или системная неконсистентность.

### Low
Косметика.

Не начинать с Low, пока остаются Critical/High.

## Правила рефакторинга
- не ломать бизнес-логику;
- не менять модели, API и маршруты без необходимости;
- не менять архитектуру всего проекта ради UI;
- сохранять существующий визуальный язык;
- использовать существующие дизайн-токены;
- избегать `!important` и чрезмерно специфичных селекторов;
- не внедрять новую UI-библиотеку без необходимости;
- не выполнять массовый несвязанный рефакторинг;
- не маскировать первопричину визуального бага случайным CSS workaround.

## Blazor
Учитывать render lifecycle, EventCallback, state changes, async handlers, loading, double submit, JS Interop, disposal, navigation, prerender и render mode. UI-баг может быть вызван состоянием компонента, а не CSS.

## MAUI Blazor Hybrid
Если применимо, дополнительно проверять safe area, status bar, bottom navigation, virtual keyboard, touch, Android back, iOS safe area и WebView-specific поведение. Не считать браузерный viewport полной заменой проверки Hybrid.

## Работа с Playwright
Если Playwright доступен, использовать его для открытия страниц, кликов, ввода, проверки viewport, console errors, navigation, user flows, mobile emulation, accessibility и regression. Не ограничиваться screenshot — проверять реальное поведение.

## Проверка после изменений
После каждой существенной итерации:
1. собрать проект;
2. открыть затронутый экран;
3. пройти пользовательский сценарий;
4. проверить Desktop;
5. проверить Mobile;
6. проверить console/network errors;
7. проверить regression.

## Финальная проверка
Минимально проверить:
- старт приложения;
- навигацию;
- создание/редактирование/удаление;
- формы и modal;
- Desktop/Mobile responsive;
- loading/empty/error states;
- отсутствие horizontal overflow;
- accessibility ключевых действий;
- отсутствие новых runtime и console errors;
- сохранность существующей бизнес-логики.

## Формат итогового отчёта
В конце указать:
- найденные проблемы по Critical/High/Medium/Low;
- что исправлено;
- какие файлы изменены;
- какие UI/CSS/logic дубли устранены;
- что проверено (Desktop, Mobile, Playwright, build, tests, console, accessibility);
- что требует ручной проверки;
- что сознательно не менялось;
- какие улучшения можно сделать позже.

Не утверждать, что UI «полностью идеален».

## Главный принцип
Codex должен работать по циклу:
```text
Увидел
↓
Воспроизвёл
↓
Понял причину
↓
Исправил
↓
Проверил
↓
Сравнил
```
Не выполнять UI/UX-рефакторинг вслепую только по исходному коду.
