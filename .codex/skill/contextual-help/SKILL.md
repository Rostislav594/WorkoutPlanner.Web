---
name: contextual-help
description: Поддерживает First Launch Experience и контекстную помощь GymPlanner: Creator Intro, Welcome Guide, Help Button/Dialog, GP Pulse, help content и удаление legacy onboarding.
---

# GymPlanner First Launch и Contextual Help

## Продуктовая модель

Система состоит из двух связанных, но разных сценариев:

```text
Первый запуск: Creator Intro → Welcome Guide → приложение
Обычная работа: ? → GP Pulse → Help Dialog / Help Sheet
```

Не создавать параллельные onboarding/help-механизмы. Общие motion-primitives GP Pulse должны переиспользоваться в обоих сценариях.

## Обязательные правила

- Не использовать персонажей, маскотов, аватары и character assets.
- Не возвращать spotlight-tour, автоматическую навигацию, длинные цепочки UI-targets или First Help Hint.
- Welcome Guide состоит ровно из четырёх коротких кадров и не является туром по реальному DOM.
- Обычный GymPlanner сохраняет текущую тёмную тему и оранжевый продуктовый accent.
- Mint используется только для GP Pulse, помощи и первого знакомства.

## GP Pulse

GP Pulse — кратковременный motion-язык: мятная точка, мягкий локальный glow и тонкий локальный stroke.

Всегда разделять два режима:

- `Travel`: точка перемещается с коротким исчезающим comet-tail. Постоянной линии нет.
- `Trace`: stroke появляется только на объекте, который непосредственно рисуется или локально подчёркивается.

Нельзя рисовать connector-line между отдельными объектами. После завершения или прерывания не должны оставаться overlay, tail, glow или активный animation loop.

## Creator Intro

- Чёрный фон, компактная композиция и один визуальный акцент GP Pulse.
- Pulse перелетает к будущей рамке, затем отдельно обводит её.
- Основной текст появляется после рамки.
- Подпись создателя и строка о спортсменах появляются последовательно, с паузами.
- После паузы Pulse перелетает к CTA без линии и отдельно обводит кнопку.
- CTA `Начать` ведёт в Welcome Guide.
- При reduced motion быстро показать структуру и CTA без полной временной шкалы.

## Welcome Guide

Четыре кадра:

1. короткое функциональное знакомство без повторного эмоционального приветствия;
2. назначение `?`;
3. поддержка и обратная связь по фактическому UI;
4. короткое завершение.

Использовать типографику, code-native UI-иллюстрации, актуальные скриншоты и GP Pulse. Сохранять только завершение всего guide, а не номер кадра.

## Contextual Help

- Использовать единые shared `HelpButton`, `HelpDialog`/`HelpSheet` и каталог контента.
- Размещать `?` только на страницах, где реально требуется объяснение, включая сложные детали тренировки и графики.
- После нажатия дать короткую Pulse-реакцию и быстро открыть help.
- Внутри help использовать Pulse умеренно: одна локальная trace/highlight-анимация, затем статичное состояние.
- Поддерживать повторное открытие, Escape, возврат фокуса, focus containment, safe area, scroll и отсутствие horizontal overflow.

## Контент и иллюстрации

- Перед изменением текста изучить фактический пользовательский flow.
- Не описывать несуществующие функции или ссылки.
- Каждая секция отвечает: что это, зачем, где, что нажать, какой результат ожидать.
- Для сложных действий использовать актуальный screenshot или точный UI-фрагмент; не выдавать декоративную картинку за скриншот графика.
- После изменения UI проверять связанный Help Topic и иллюстрации.

## Реализация

- Предпочитать SVG, CSS transforms, opacity и stroke animations.
- Timeline должна корректно останавливаться при disposal/navigation.
- JS Interop допустим только через одну shared abstraction, если CSS/Blazor недостаточно.
- Учитывать Desktop, Mobile Hybrid, safe areas, landscape и `prefers-reduced-motion`.
- Не менять workout/domain/database логику без необходимости.

## Проверка

- build и релевантные tests;
- новый пользователь: Creator Intro → Welcome Guide → приложение;
- завершивший пользователь не видит first launch повторно;
- Travel не оставляет persistent line;
- Help открывается быстро и закрывается без ghost state;
- repeated click, Escape, focus restore, resize/orientation, disposal;
- Desktop и Mobile viewport;
- reduced motion;
- отсутствие character/onboarding/spotlight зависимостей.
