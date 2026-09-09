# Этап 8. Минимальная локальная база и offline-first — результат

Дата проверки: 9 сентября 2026 года.

## Выполнено

Основное действие Minimal MVP переведено на local-first модель. При завершении
подхода Room в одной транзакции:

1. отмечает `LocalWorkoutSet` выполненным;
2. создаёт `PendingSyncOperation` с новым GUID;
3. сохраняет payload `CompleteSet`, исходную server version и порядок операции;
4. сохраняет rest deadline.

UI наблюдает Room, поэтому сразу открывает таймер и не ждёт сеть. После process
death восстанавливаются локальное выполнение, таймер/следующий подход и состояние
очереди.

## Локальная модель

Созданы отдельные Room entities:

- `LocalWorkout` — активность, последняя проверка и persisted rest state;
- `LocalExercise` — имя и порядок упражнения;
- `LocalWorkoutSet` — постоянный set ID, индивидуальные вес/повторы, completion и
  server version;
- `PendingSyncOperation` — operation ID, тип, entity ID, JSON payload, UTC-время,
  sequence, attempts, last attempt/error, status и retryability;
- `DeviceSessionMetadata` — только несекретные признаки локальной сессии.

Токены и pairing credentials в Room не сохраняются. Существующая граница
`AndroidKeystoreTokenStore` сохранена.

## Очередь и retry

- Minimal MVP поддерживает один mutation type: `CompleteSet`.
- Глобальный возрастающий `sequenceNumber` и последовательный queue drain сохраняют
  порядок, в том числе для одной сущности.
- Используется один unique WorkManager job `pending-workout-sync` с network
  constraint и exponential backoff; отдельный worker на tap не создаётся.
- `Pending`/retryable `Failed` можно отправлять повторно; `Synced`, terminal
  `Failed` и `Conflict` повторно автоматически не отправляются.
- Операция, оставшаяся в `Syncing` после прерывания процесса, возвращается в
  `Pending` перед новым drain.
- При `401` предусмотрен ровно один refresh-and-resend внутри попытки.
- При `409` локальный set заменяется авторитетным состоянием, операция получает
  `Conflict`, silent overwrite и бесконечный retry отсутствуют.

`FakeWatchRemoteDataSource` учитывает реальное состояние сети эмулятора и хранит
результат operation ID идемпотентно. Реальный Retrofit/backend относятся к Этапу 9.

## UI/UX

Одно крупное действие и экраны Этапа 7 сохранены. Небольшая строка статуса теперь
показывает `Синхронизировано`, `Ожидает синхронизации`,
`Синхронизация отложена` или `Конфликт данных`; состояние не передаётся только
цветом. Новый conflict screen не добавлялся.

При финальной проверке `ui-ux-pro-max` использован как checklist: touch target
остался не менее 48 dp, offline-feedback находится рядом с текущим подходом и не
создаёт дополнительного шага. Строка «Синхронизировано» проверена на квадратном
экране 360 × 360 без clipping/overflow.

## Автоматизированная проверка

Команды в `WorkoutPlanner.WearOS`:

```powershell
.\gradlew.bat testDebugUnitTest lintDebug assembleDebug
.\gradlew.bat connectedDebugAndroidTest
```

Проверено:

- атомарное создание ровно одной pending operation вместе с локальным completion;
- повтор transient-операции с тем же `operationId`;
- отсутствие повторной отправки после `Synced`;
- увеличение attempt count и server version;
- применение авторитетного set state при `Conflict` и запрет дальнейшего retry.

Instrumented suite: 3 теста на Wear OS 6 / API 36, все прошли. Существующие 5 JVM
unit-тестов также проходят. Lint и debug APK собираются успешно.

## Проверка на устройстве

На `WorkoutPlanner_Wear_Square_API_36` (360 × 360) реальными ADB-касаниями выполнен
сценарий:

1. pairing demo-кодом и загрузка mock-тренировки;
2. включение airplane mode;
3. локальное завершение первого подхода;
4. принудительная остановка процесса и повторный запуск;
5. восстановление экрана отдыха, затем подхода 2 и pending-статуса;
6. отключение airplane mode;
7. автоматическая отправка очереди и переход к `Синхронизировано`;
8. повторный process restart — сохранены подход 2, вес `82,5 кг` и synced-статус.

Отдельно при отключённой сети выполнен второй подход и перезагружен сам Wear OS
emulator. После полной загрузки сохранились очередь и переход к упражнению
«Приседания»; возврат сети синхронизировал ту же pending operation. Таким образом,
проверена не только остановка процесса, но и перезапуск часов.

Физические часы не использовались; проверка выполнена на Wear OS emulator.

## Миграции и границы

Это первая версия новой локальной Room database (`version = 1`), поэтому migration
между версиями не требуется. Серверные модели, EF migrations и API на Этапе 8 не
изменялись.

Не реализованы production network, настоящее pairing, расширенный conflict UI,
редактирование веса/повторов, undo, finish workout, workout overview и SignalR.

## Стоп-условие

Критерий Этапа 8 выполнен на fake network: offline completion и pending operation
переживают process restart, после восстановления сети операция отправляется без
потери и повторного применения. Этап 9 не начат.
