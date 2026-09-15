# Этап 10. Minimal MVP end-to-end milestone — результат

Дата проверки: 9 сентября 2026 года.

## Результат

Контрольная точка Minimal MVP пройдена на Wear OS Emulator. Критический цикл

```text
Exercise → Current Set → Complete → Rest → Next Set
```

проверен с реальным WorkoutPlanner backend и отдельной временной SQLite-базой.
Функции Extended Wear OS не добавлялись; Этап 11 не начинался.

## Проверенные сценарии

| № | Сценарий | Результат |
|---|---|---|
| 1 | Создание pairing code в основном приложении | В браузерном UI открыт `Профиль → Часы`, создан шестизначный одноразовый код. |
| 2–3 | Ввод кода и подключение часов | Код введён instrumentation-тестом в реальный pairing UI; backend выдал watch-сессию. |
| 4–5 | Запуск и получение активной тренировки | Тренировка создана/активирована через реальные mobile endpoints и подтверждена на странице `Сегодня`; часы загрузили её через `/api/watch/workouts/active`. |
| 6–7 | Правильные упражнение и подход | Сначала показаны `Жим — milestone`, `Подход 1 / 2`, `42.5 кг · 8 повт.`, затем `Подход 2 / 2`, `47.5 кг · 6 повт.`. |
| 8–10 | Одно действие, local-first completion и отдых | Нажат один control `Выполнить подход`; Room-транзакция завершила подход и создала operation до сетевой отправки; экран сразу перешёл в rest. |
| 11 | Haptic feedback | JVM-тест фиксирует событие независимо от задержки `StateFlow`; `dumpsys vibrator_manager` на эмуляторе зафиксировал `performHapticFeedback(constant=16)` от `com.gymplanner.wearos`. |
| 12 | Следующий невыполненный подход | После отдыха и перезапуска показан второй подход первого упражнения. |
| 13 | Переход к следующему упражнению | После второго подхода показаны `Присед — milestone`, `Подход 1 / 1`, `80 кг · 5 повт.`. |
| 14 | Offline completion | При отключённых Wi-Fi и mobile data второй подход локально завершён; Room содержит retryable pending operation. |
| 15 | Sync после возврата сети | После включения сети статус сменился с `Ожидает синхронизации` на `Синхронизировано`; основной UI показал оба подхода жима выполненными. |
| 16 | Idempotency `operationId` | Серверный integration test повторно отправляет тот же operation ID и подтверждает единственное применение операции. |
| 17 | Restart persistence | Force-stop и повторный запуск без сети сохранили pending operation и актуальный переход к следующему упражнению. |
| 18 | Revoked device | Реальное устройство отозвано через management API; следующая запись получила отказ, локальная сессия очищена, UI вернулся к `Введите код`; сервер не принял completion приседа. |

## Исправление контрольной точки

Во время проверки устранена гонка haptic-события после local-first completion.
`WorkoutRepository.completeCurrentSet()` теперь возвращает признак фактической
локальной транзакции. `MinimalMvpViewModel` отправляет `SetCompleted` по этому
результату, а не пытается успеть прочитать уже обновившийся асинхронный `StateFlow`.
Повторное нажатие или отсутствие текущего подхода возвращает `false` и не создаёт
ложную вибрацию.

Real API smoke-test теперь принимает необязательный `expectedExercise` и умеет
продолжить проверку уже подключённой сессии. Добавлены отдельные opt-in шаги для
offline persistence и revoked-device transition.

## Валидация

### Server

- `dotnet build WorkoutPlanner.Web/WorkoutPlanner.Web.csproj --no-restore` с
  изолированным output: успешно, 0 ошибок, 1 существующее предупреждение об
  устаревшем `FirebaseAdmin.Messaging.Message.Token`.
- Watch-фильтр integration tests: 9/9 успешно.
- Migration tests: 2/2 успешно.
- `dotnet ef migrations has-pending-model-changes`: модель соответствует последней
  миграции.
- Полный запуск: 69/71 успешно. Два несвязанных push/admin теста столкнулись при
  параллельном запуске с process-global `FirebaseApp already exists`; оба теста
  затем отдельно прошли 1/1. Wear OS сценарии в этом запуске не падали.

### Wear OS

- `testDebugUnitTest`: успешно.
- `lintDebug`: успешно.
- `assembleDebug`: успешно.
- `assembleRelease`: успешно.
- `assembleDebugAndroidTest`: успешно.
- Instrumented suite на эмуляторе: 7/7 успешно (4 постоянных offline/Room теста и
  3 opt-in smoke-теста без аргументов корректно skipped внутри runner).
- Реальные opt-in шаги отдельно: online pairing/completion, offline persistence и
  revoked-device transition — каждый успешно.

### Устройство и UI

- Wear OS Emulator: `WorkoutPlanner_Wear_Square_API_36`, Wear OS 6 / API 36,
  360 × 360.
- Основной Blazor UI проверен интерактивно в браузере: профиль, pairing code,
  тренировка дня и синхронизированные completion-состояния.
- На экране часов проверены читаемость индивидуального веса/повторов, порядок
  подходов, sync status и доступность единственного крупного completion control;
  обрезки критичного содержимого на 360 × 360 нет. Для финальной проверки применён
  checklist `ui-ux-pro-max`.
- Физические Wear OS-часы в окружении отсутствуют. Подключённый Redmi является
  Android-телефоном, поэтому Wear APK на него намеренно не устанавливался. Нельзя
  физически подтвердить ощущение вибрации, Always-On/выключенный экран и поведение
  конкретного OEM; haptic подтверждён системной историей эмулятора.

## Авторитетное состояние сервера

После online и offline sync mobile API вернул оба подхода `Жим — milestone` как
выполненные ровно один раз. После отзыва часов попытка завершить
`Присед — milestone` не была принята: его единственный подход остался
`completed: false`. Server остаётся source of truth.

## Границы и оставшиеся риски

- Новых EF/Room migrations и новых production dependencies нет.
- Не добавлялись devices management UI, SignalR, overview, undo/edit set,
  редактирование веса/повторов или finish workout.
- Полный server suite чувствителен к параллельному созданию default FirebaseApp;
  это существующая, не относящаяся к Wear OS изоляция тестов.
- Проверка на реальных часах остаётся обязательной перед публичным релизом.

## Стоп-условие

Критерий Этапа 10 выполнен на эмуляторе. Minimal MVP зафиксирован как рабочий;
Этап 11 и Extended Wear OS в рамках этой работы не начинались.
