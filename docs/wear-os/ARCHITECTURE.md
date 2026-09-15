# Архитектура интеграции Wear OS

## Границы системы

WorkoutPlanner backend остаётся единственным источником истины для пользователя,
устройств, активной тренировки, подходов, итоговых значений и истории. Wear OS —
отдельный Kotlin-клиент, который работает через узкий `/api/watch` contract и не
имеет доступа к EF Core, SQLite backend или паролю пользователя.

```text
Main app / Blazor UI ─┐
                     ├─ ASP.NET Core application services ─ EF Core / SQLite
Wear OS ─ /api/watch ┘                 │
                                      └─ user-scoped realtime notifications

Wear OS UI → ViewModel → OfflineFirstWorkoutRepository
                              ├─ Room snapshot + pending queue
                              ├─ WorkManager / SyncQueueProcessor
                              └─ Retrofit → /api/watch
```

Основной и watch API используют общие application services. Завершение тренировки
проходит через `WorkoutCompletionService`, поэтому snapshot истории и progress не
имеют отдельной реализации для часов.

## Minimal MVP и Extended Wear OS

Minimal MVP включает pairing, watch credentials, загрузку текущей тренировки,
текущий подход, local-first completion, отдых, следующий подход и durable retry.
Эта контрольная точка зафиксирована в `STAGE_10_MILESTONE.md`.

Extended Wear OS добавляет управление устройствами, realtime-обновление открытого
основного UI, обзор тренировки, редактирование индивидуального веса/повторов, undo,
явные sync/conflict состояния и подтверждаемое завершение всей тренировки.

## Серверная модель

- `WorkoutDay.Id` — идентификатор запланированного запуска тренировки на часах.
- Активная тренировка — незавершённый `WorkoutDay` текущего пользователя на текущую
  локальную дату backend плюс принадлежащий ему `TrainingPlan`.
- Упражнения упорядочиваются по стабильному `Exercise.Id`.
- Подходы упорядочиваются по `SetNumber`, затем по `ExerciseTemplateSet.Id`.
- `ExerciseTemplateSet.Id` — адресуемый `SetId`.
- Вес и повторения хранятся отдельно в каждом `ExerciseTemplateSet`.
- `ExerciseTemplateSet.Version` — SQLite-совместимый optimistic concurrency token;
  успешная mutation увеличивает его ровно один раз.

Текущая серверная модель по историческим причинам использует mutable training
template как рабочее состояние scheduled workout. Wear OS сохраняет эту семантику,
а не создаёт параллельный `TrainingSession` aggregate.

## Pairing и credentials

Основное приложение создаёт случайный шестизначный код с ограниченным временем
жизни. Backend хранит только salted hash, атомарно потребляет код один раз и
ограничивает частоту попыток. Часы получают короткоживущий access token и
ротируемый refresh token.

Backend хранит hash refresh token. На часах access token находится только в памяти,
а refresh token шифруется Android Keystore AES/GCM и сохраняется в private
SharedPreferences. Identity содержит отдельные watch/device claims. Authorization
policy при каждом защищённом запросе проверяет владельца, security stamp, срок и
отзыв устройства.

## Offline-first и конфликты

Room хранит локальные workout/exercise/set snapshots, session metadata и
`PendingSyncOperation`. Complete, undo и edit записывают локальное состояние и
операцию в одной транзакции. Один unique WorkManager job с network constraint и
exponential backoff дренирует глобально упорядоченную очередь.

Каждая запись имеет GUID `operationId`, `sequenceNumber` и ожидаемую server version.
Backend атомарно применяет изменение и сохраняет replay result. Повтор того же
operation ID не изменяет данные повторно. `409` применяет авторитетный server
snapshot, отмечает зависимые операции конфликтными и никогда не выполняет silent
last-write-wins.

Finish — server-confirmed barrier: клиент сначала дренирует set-очередь, блокируется
на нерешённых операциях и удаляет активный локальный workout только после `200`.

## Rest timer и энергия

Deadline отдыха сохраняется как UTC timestamp в Room. При повторном открытии
remaining time пересчитывается и переводится в monotonic elapsed deadline; таймер
не является decrement-only состоянием.

Вибрация конца отдыха отправляется из `ActiveWorkoutService` — foreground-сервиса,
который живёт ровно столько, сколько идёт тренировка. Причина: в зале человек
опускает руку, экран гаснет, приложение уходит в фон, и haptic из composable до
него уже не доходит, а процесс может быть выгружен. Сервис читает тот же дедлайн
из Room, ничего не решает сам и не дублирует бизнес-логику.

Источник вибрации ровно один: UI-событие `RestFinished` намеренно не вызывает
haptic, иначе при открытом приложении сигнал был бы двойным.

Тип сервиса — `specialUse`, а не `health`: `health` требует разрешения на датчики
тела (`BODY_SENSORS`/`ACTIVITY_RECOGNITION`), которых приложение не читает.
Обоснование для Play-ревью объявлено в манифесте через
`PROPERTY_SPECIAL_USE_FGS_SUBTYPE`. Exact alarm не используется: он потребовал бы
`USE_EXACT_ALARM`, который Play допускает только для приложений-будильников.

Тот же сервис держит Ongoing Activity — индикатор идущей тренировки на циферблате,
по которому можно вернуться в приложение одним касанием.

Активная тренировка обновляется только по действию пользователя; периодического
polling нет. Sync использует one-time unique WorkManager work, а не отдельный
worker на каждый tap.

## Ambient

Приложение поддерживает always-on: при опущенной руке UI не подменяется
циферблатом, а переключается на `AmbientWorkoutScreen`. Экран следует
рекомендациям Google — почти целиком чёрный, без крупных заливок, приглушённый
серый вместо чистого белого (защита от выгорания), время показано всегда.

Обратный отсчёт в ambient намеренно не тикает: система будит приложение примерно
раз в минуту, и посекундный таймер показывал бы неправду. О конце отдыха сообщает
вибрация, ради неё на экран смотреть не нужно.

Состояние ambient читается через `rememberAmbientModeManager()` из
`androidx.wear.compose.foundation`. Отдельного объявления always-on не требуется:
приложение таргетит SDK 36, а на Wear OS 6+ такие приложения считаются always-on
автоматически.

## Realtime

После успешного persistence сервер публикует минимальные user-scoped уведомления о
set mutation и finish. Открытая Blazor Server страница подписывается на них и
обновляет только затронутое состояние. Realtime не является persistence path и не
используется часами как обязательный канал.

## Конфигурация

- Debug emulator: `WEAR_DEBUG_API_BASE_URL`, default
  `http://10.0.2.2:5121/api/watch/`.
- Release: обязательный `WEAR_RELEASE_API_BASE_URL`; только абсолютный HTTPS URL с
  завершающим `/`, без credentials/query/fragment, не loopback, `.local`,
  `10.0.2.2` или `.invalid`.
- Debug network log содержит только method, encoded path, status и duration.
  Заголовки, bodies, pairing codes и credentials не логируются; в release logging
  отключён через `BuildConfig.DEBUG`.

## Известные ограничения

- Дата активной тренировки зависит от timezone backend, а не профиля пользователя.
- Runtime-поля scheduled workout по-прежнему изменяют mutable template.
- Физическое поведение haptic, Always-On и OEM battery policy требует проверки на
  целевых моделях часов.
- Вибрация конца отдыха и ambient проверены на Wear-эмуляторе (API 36, круглый
  профиль): сервис остаётся foreground при свёрнутом приложении и погашенном
  экране, вибрация фиксируется в истории `vibrator_manager` заданным узором,
  ambient-экран отрисовывается вместо циферблата. На физических часах не
  проверялось: поведение haptic и OEM-политики энергосбережения там могут
  отличаться.
- Тип foreground-сервиса `specialUse` требует обоснования при публикации в Play.
