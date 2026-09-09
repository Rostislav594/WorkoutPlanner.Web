# Wear OS integration — анализ текущей архитектуры

Дата анализа: 2026-09-04  
Этап: 0 из `docs/wear-os/IMPLEMENTATION_PLAN.md`  
Область: только анализ и проектирование; рабочий код, модели, UI, миграции и Wear OS-проект не изменялись.

## 1. Краткий вывод

Текущая «активная тренировка» не хранится как отдельный экземпляр тренировки. Сервер считает активной незавершённую запись `WorkoutDay` текущего пользователя, дата которой равна серверному `DateTime.Today`, а содержимое тренировки загружает непосредственно из связанного шаблона `TrainingPlan`. Во время тренировки изменяются `Exercise` и `ExerciseTemplateSet` самого шаблона.

Это главный архитектурный факт для Wear OS:

- идентификатором запланированного запуска тренировки следует считать `WorkoutDay.Id`, а не `TrainingPlan.Id`;
- текущие значения подходов находятся в `ExerciseTemplateSet`;
- отдельного `StartedAtUtc`, состояния active session и стабильного snapshot экземпляра тренировки сейчас нет;
- существующие `TrainingSession`, `TrainingSessionExercise` и `ExerciseSet` присутствуют в схеме, но в рабочем scheduled-workout flow не создаются и не читаются;
- у каждого `ExerciseTemplateSet` есть постоянный первичный ключ `int Id`, но текущий mobile API его не возвращает;
- вес и повторения индивидуальны для каждого подхода (`ExerciseTemplateSet.Weight` и `.Repetitions`);
- optimistic concurrency у `ExerciseTemplateSet` и `ExerciseSet` отсутствует полностью. Это блокирующий пробел для будущей стратегии конфликтов: до offline-записей с часов нужен SQLite-совместимый concurrency token.

## 2. Карта solution и проектов

Solution: `WorkoutPlanner.Web.slnx`, .NET 10.

| Проект | Назначение | Значение для Wear OS |
|---|---|---|
| `WorkoutPlanner.Web` | ASP.NET Core host, Blazor Server UI, Identity, Minimal API, EF Core/SQLite, сервисы и миграции | Backend и источник истины; сюда логично добавлять application operations, watch API и persistence подключения |
| `WorkoutPlanner.Api.Contracts` | Общие DTO текущего mobile API | Полезный паттерн разделения wire contracts; watch DTO должны быть отдельными и узкими |
| `WorkoutPlanner.Domain` | Небольшая библиотека расчётов (`TrainingMetrics`) | Сейчас не содержит workout aggregate или persistence models |
| `WorkoutPlanner.UI` | Общие Razor-компоненты и статические UI-ресурсы | Для Этапа 0 не затрагивается |
| `GymPlanner.Mobile` | .NET MAUI Blazor Hybrid клиент Android/iOS | Показывает существующий API/auth flow; не является Wear OS-клиентом |
| `WorkoutPlanner.Web.Tests` | xUnit unit/integration tests | База для security, ownership, API, migration и concurrency regression tests |

Backend объединён с web UI в одном host. Контроллеров нет: HTTP API реализован Minimal API extension-классами в `WorkoutPlanner.Web/Api` и регистрируется через `app.MapGymPlannerApi()`.

Хранилище — SQLite через `Microsoft.EntityFrameworkCore.Sqlite`; `WorkoutDbContext` наследуется от `IdentityDbContext<IdentityUser>`. Приложение выполняет `Database.Migrate()` при старте.

## 3. Релевантная модель данных

### 3.1 Пользователь

- Пользователь — стандартный `Microsoft.AspNetCore.Identity.IdentityUser`, ключ `string`.
- Workout-данные содержат `UserId` непосредственно: `TrainingPlan`, `Exercise`, `WorkoutDay`, `WorkoutHistory`, `TrainingSession`.
- В большинстве сервисов ownership фильтруется по `UserId`.
- Отдельный доменный тип пользователя отсутствует; `UserProfile` дополняет Identity-пользователя профильными данными.

### 3.2 Шаблон тренировки

`TrainingPlan`:

- `Id: int`;
- `UserId: string?`;
- `WorkoutName`;
- `Date`;
- `Exercises: List<Exercise>`.

`Exercise`:

- `Id: int`;
- `UserId: string?`;
- `TrainingPlanId`;
- `Name`, `ExerciseDefinitionId`, `SupersetGroupId`, `Status`;
- `Sets: List<ExerciseTemplateSet>`.

`ExerciseTemplateSet` — фактически используемый подход scheduled workout:

- `Id: int` — постоянный DB primary key;
- `ExerciseId: int`;
- `SetNumber: int`;
- `Weight: double` — индивидуальный вес этого подхода;
- `Repetitions: int` — индивидуальные повторения этого подхода;
- `Completed: bool`;
- `IsWarmup: bool`.

EF настраивает cascade delete `Exercise -> ExerciseTemplateSet`. В текущем snapshot есть индекс только по `ExerciseId`. Уникального ограничения `(ExerciseId, SetNumber)` нет.

### 3.3 Планирование и активность

`WorkoutDay`:

- `Id: int`;
- `UserId: string?`;
- `Date: DateTime`;
- `TrainingPlanId: int`;
- `IsCompleted: bool`.

Явная FK/navigation-конфигурация между `WorkoutDay` и `TrainingPlan` отсутствует; связь поддерживается числовым `TrainingPlanId` и сервисным кодом.

Активность определяется запросом:

```text
WorkoutDay.UserId == currentUserId
&& WorkoutDay.Date.Date == DateTime.Today
&& !WorkoutDay.IsCompleted
```

После этого отдельно загружается принадлежащий пользователю `TrainingPlan` по `WorkoutDay.TrainingPlanId`.

Следствия:

- активна только тренировка на сегодняшнюю локальную дату backend-host;
- нет состояния «started» отдельно от «scheduled»;
- `POST /api/workouts/today/start` сейчас является алиасом чтения и ничего не создаёт;
- нет `StartedAtUtc`;
- потенциально база не запрещает несколько незавершённых `WorkoutDay` одного пользователя на одну дату; сервис обычно обновляет первую найденную запись, но DB unique constraint отсутствует;
- `WorkoutDay.Id` — наиболее подходящий текущий `WorkoutId` для watch API, потому что `TrainingPlan.Id` обозначает переиспользуемый шаблон.

### 3.4 Неиспользуемая session-модель

В базе существуют:

- `TrainingSession` (`Id`, `UserId`, `Date`, `TrainingPlanId`);
- `TrainingSessionExercise` (`Id`, `TrainingSessionId`, `ExerciseDefinitionId`, `Status`, `ExerciseIndex`);
- `ExerciseSet` (`Id`, `TrainingSessionExerciseId`, `SetNumber`, `Weight`, `Repetitions`, `Completed`, `IsWarmup`).

Поиск production-кода не обнаружил создания или использования этих сущностей в текущем workout lifecycle. Они участвуют в `DbContext`, миграциях, account deletion и тестовых fixtures, но scheduled completion сохраняет JSON snapshot в `WorkoutHistory`, а не `TrainingSession`. Нельзя строить Этап 1 на предположении, что эти таблицы являются активной тренировкой.

### 3.5 Завершённая тренировка

`WorkoutHistory` хранит метаданные и `Details` как JSON `WorkoutHistoryDetails`. В snapshot находятся упражнения и `WorkoutHistorySet` с `SetNumber`, `Weight`, `Repetitions`, `Completed`, `IsWarmup`, но без постоянных ID исходных подходов.

`WorkoutCompletionService.CompleteTodayAsync`:

1. Находит незавершённый сегодняшний `WorkoutDay` пользователя.
2. Загружает принадлежащий пользователю `TrainingPlan` с упражнениями и подходами.
3. Открывает транзакцию.
4. Атомарно «захватывает» день через conditional `ExecuteUpdate` (`!IsCompleted -> true`).
5. Создаёт JSON snapshot истории из текущего состояния шаблона.
6. Обновляет progress snapshots.
7. Сохраняет и фиксирует транзакцию.

Эта операция уже является хорошей основой для переиспользования, но привязана к ambient `CurrentUserService` и понятию «сегодня», а не к явному workout instance ID.

## 4. Текущий жизненный цикл тренировки

### Scheduled workout

1. Пользователь создаёт `TrainingPlan` и его `Exercise`/`ExerciseTemplateSet`.
2. Календарь создаёт или обновляет `WorkoutDay` с `TrainingPlanId`.
3. `TodayWorkoutService` считает активным незавершённый `WorkoutDay` на `DateTime.Today`.
4. UI получает сам шаблон и использует его `Completed`, `Weight`, `Repetitions` как рабочее состояние.
5. Web UI сохраняет изменение checkbox немедленно через `ExerciseService.UpdateExerciseAsync`; mobile UI держит часть изменений в локальных drafts и отправляет целое упражнение через `PUT /api/exercises/{id}` при явном сохранении/завершении.
6. `WorkoutCompletionService` помечает `WorkoutDay.IsCompleted = true`, сериализует текущее состояние шаблона в историю и записывает прогресс.

### Free workout

Free workout существует только как client-side draft до завершения. `POST /api/workouts/free/complete` создаёт историю и при желании новый шаблон. Он не создаёт active server workout и поэтому не может быть получен часами в текущей архитектуре.

### Существенное ограничение

Рабочее состояние scheduled workout смешано с шаблоном. Поэтому изменение completion/weight/reps во время одной тренировки меняет переиспользуемый шаблон. Этап 0 не исправляет это, но watch application service обязан следовать существующему поведению до отдельного согласованного изменения модели. Создание `TrainingSession` только ради Wear OS в Этапе 1 было бы архитектурным расхождением и выходит за границы минимального изменения.

## 5. Текущий жизненный цикл подхода

1. Подход создаётся как `ExerciseTemplateSet` внутри `ExerciseService.AddExerciseAsync`.
2. Порядок внутри упражнения задаётся `SetNumber`; API валидирует уникальную последовательность `1..SetsCount`.
3. Обновление выполняется не узкой set operation, а заменой/синхронизацией всего списка подходов упражнения в `ExerciseService.UpdateExerciseAsync`.
4. Existing set сопоставляется сначала по `sourceSet.Id`, иначе по `SetNumber`.
5. `Weight`, `Repetitions`, `Completed`, `IsWarmup` переписываются и выполняется один `SaveChangesAsync`.
6. Лишние подходы удаляются.
7. При завершении тренировки значения копируются в JSON history snapshot.

Для Wear OS нельзя использовать существующий generic `PUT /api/exercises/{id}` как Minimal MVP completion endpoint: он не передаёт set ID, разрешает менять весь exercise aggregate и создаёт риск потерянных обновлений.

## 6. Обязательные проверки Этапа 0

### Как определяется активная тренировка

Активная тренировка — незавершённый сегодняшний `WorkoutDay` текущего пользователя плюс связанный шаблон `TrainingPlan`. Это не `TrainingSession`. Отдельного старта и отдельного active snapshot нет.

### Порядок упражнений

- `ExerciseService.GetExercisesAsync` явно сортирует упражнения по `Exercise.Id`.
- Web `Today.razor` использует этот сервис, поэтому фактический web-порядок — ascending `Exercise.Id`.
- `TodayWorkoutService` загружает `TrainingPlan.Exercises` без `OrderBy`.
- `ApplicationContractMapper` и `WorkoutApiEndpoints.ToApiResponse` сохраняют полученный порядок без дополнительной сортировки.
- Mobile today flow использует `TodayWorkoutApiResponse.TrainingPlan.Exercises`; следовательно, его порядок на уровне SQL/API сейчас формально не детерминирован, даже если SQLite обычно возвращает ожидаемый порядок.
- Отдельного поля `Order`/`Position` у `Exercise` нет. Для Minimal MVP сервер должен зафиксировать существующую фактическую семантику как `OrderBy(Exercise.Id)` (с учётом superset: `SupersetGroupId` группирует, но не заменяет общий порядок).

### Уникальный ID подхода

- В базе у `ExerciseTemplateSet` есть стабильный `int Id` primary key.
- Application contract содержит этот `Id`.
- Текущий `ExerciseSetApiResponse` содержит только `SetNumber`, `Repetitions`, `Weight`, `Completed`, `IsWarmup`; set ID намеренно/случайно теряется при API mapping.
- Поэтому текущий mobile wire contract не позволяет адресовать конкретный подход по постоянному ID.
- Для watch DTO обязателен `SetId = ExerciseTemplateSet.Id`.
- DB не гарантирует уникальность `(ExerciseId, SetNumber)`; `SetNumber` нельзя использовать как глобальный ID.

### Индивидуальный вес каждого подхода

Поддерживается полностью: и `ExerciseTemplateSet`, и неиспользуемый `ExerciseSet`, и history snapshot имеют собственные `Weight` и `Repetitions` на каждый подход. `ExerciseService`, API и completion snapshot сохраняют эти значения отдельно. Это подтверждается тестами `ExerciseSetWeightTests`, `TrainingMetricsTests`, `MuscleLoadCalculatorTests` и `MobileApiTests`.

### Optimistic concurrency

У `ExerciseTemplateSet` и `ExerciseSet` нет:

- `RowVersion`;
- `[Timestamp]`;
- свойства `Version`;
- Fluent API `.IsConcurrencyToken()`;
- обработки `DbUpdateConcurrencyException`.

`IsConcurrencyToken()` в migration snapshot относится только к стандартным Identity `ConcurrencyStamp` пользователя/роли, не к workout sets.

Вывод: текущие обновления являются last-write-wins. В частности, whole-exercise update может затереть параллельное изменение одного подхода.

Рекомендация для последующих этапов: добавить SQLite-совместимый целочисленный `Version` (`long`, начальное значение 0 или 1) на `ExerciseTemplateSet`, настроить его как concurrency token и увеличивать при каждой mutation. Узкая set operation должна выполнять условное обновление по `{ Id, expectedVersion }`; ноль затронутых строк означает `409 Conflict`, после чего сервер возвращает актуальный set DTO. SQL Server-style auto-generated `rowversion byte[]` нельзя предполагать, потому что текущий provider — SQLite. Если Этап 1 должен остаться без schema change, он может выделить интерфейс и возвращать set ID, но надёжная offline conflict detection должна считаться заблокированной до миграции версии на соответствующем этапе.

## 7. Аутентификация и идентификация пользователя

### Blazor

`CurrentUserService` получает `AuthenticationState` и читает `ClaimTypes.NameIdentifier`. Все application services сами получают current user ID; client-supplied `UserId` не используется.

### Mobile API

- ASP.NET Core Identity API endpoints выдают bearer/refresh credentials.
- Bearer lifetime настроен на 15 минут, refresh lifetime — 7 дней.
- После login создаётся `MobileSession` с `Guid Id`, `UserId`, expiry/revocation timestamps; ID добавляется в claim `gymplanner:mobile_session`.
- Policy `MobileApiAuthorization.PolicyName` требует Identity bearer и активную, неотозванную `MobileSession` данного пользователя.
- Minimal API workout endpoints используют эту policy.

Это пригодный паттерн, но watch identity должна отличаться отдельным device/session claim и проверять `WatchDevice.RevokedAtUtc`. Не следует выдавать часам пароль пользователя или бездумно переиспользовать обычный mobile login.

## 8. Релевантные сервисы

| Сервис | Текущая ответственность | Оценка для интеграции |
|---|---|---|
| `TodayWorkoutService` | Находит сегодняшний незавершённый day и загружает template | Логику чтения можно переиспользовать после явного user scope и детерминированной сортировки |
| `ExerciseService` | CRUD упражнения и whole-exercise set synchronization | Нельзя напрямую выставлять watch endpoint; нужна узкая set operation |
| `WorkoutDayService` | Календарь, scheduling, move/delete, completion flag | Полезен для active workout lookup, но часть completion дублируется более полной службой |
| `WorkoutCompletionService` | Транзакционное scheduled/free completion, history и progress | Переиспользовать для будущего finish; Minimal MVP finish не включает |
| `TrainingPlanService` | CRUD шаблонов | Только чтение ownership/context |
| `CurrentUserService` | Получение Identity user ID из principal | UI-oriented ambient dependency; API application operation лучше принимать trusted user context/ID из server auth layer |
| `MobileSessionService` | Создание, проверка и отзыв mobile sessions | Архитектурный образец для watch device auth, но не готовая device credential модель |

Безопасно вынести business operations в application service можно. Рекомендуемая граница: сервис получает доверенный `userId` (только от authenticated server context), `workoutDayId`/`setId`, expected version и cancellation token; внутри одним запросом проверяет цепочку `set -> exercise -> plan -> active WorkoutDay -> user` и выполняет mutation. Blazor и watch endpoint должны вызывать одну операцию.

## 9. Релевантные страницы и клиенты

- `WorkoutPlanner.Web/Components/Pages/Today.razor`: scheduled workout, checkbox completion, status, finish; checkbox немедленно сохраняет целое упражнение.
- `WorkoutPlanner.Web/Components/Pages/Calendar.razor`: создаёт/двигает `WorkoutDay`.
- `WorkoutPlanner.Web/Components/Pages/Workouts.razor` и `WorkoutDetails.razor`: управление шаблонами и упражнениями.
- `WorkoutPlanner.Web/Components/Pages/History.razor`: читает immutable JSON snapshots.
- `GymPlanner.Mobile/Components/Pages/Today.razor`: scheduled/free drafts, per-set editing, completion checkbox и rest timers; scheduled изменения отправляются whole-exercise API, часть — только при save/finish.
- `GymPlanner.Mobile/Api/WorkoutApiClient.cs`: CRUD plan/exercise.
- `GymPlanner.Mobile/Api/WorkoutLifecycleApiClient.cs`: today/start/complete/free/history/calendar.

Существующего Wear OS-клиента или pairing UI нет.

## 10. API и realtime

Текущий API prefix — `/api`.

Релевантные endpoints:

- Identity/mobile auth: `/api/authentication/register`, `/login`, `/refresh`, `/logout`;
- today lifecycle: `GET /api/workouts/today`, `POST /api/workouts/today/start`, `POST /api/workouts/today/complete`;
- templates/exercises: `/api/training-plans`, `/api/exercises/{id}`;
- calendar: `/api/calendar`;
- history: `/api/history`.

API использует Minimal API, typed response records, ProblemDetails и cancellation tokens. Swagger/OpenAPI доступен в Development.

SignalR application infrastructure отсутствует: нет Hub-классов, `MapHub`, `IHubContext` или client HubConnection. Blazor Server внутренне использует SignalR framework, но это не доменный workout hub и не может считаться готовой realtime-интеграцией.

## 11. Предлагаемые DTO для Minimal MVP

DTO должны жить на wire boundary и не возвращать EF entities. Рекомендуется отдельный watch namespace/contracts, чтобы не ломать текущий mobile contract.

```text
WatchActiveWorkoutResponse
  WorkoutId              = WorkoutDay.Id
  TemplateId             = TrainingPlan.Id (диагностически/для навигации, не identity запуска)
  WorkoutName
  ScheduledDate
  StartedAtUtc?          = null до появления реального start timestamp
  Exercises[]
  CurrentExerciseId?
  CurrentSetId?

WatchExerciseResponse
  ExerciseId             = Exercise.Id
  Name
  Order                  = детерминированный индекс после OrderBy(Id)
  SupersetGroupId?
  Sets[]

WatchSetResponse
  SetId                  = ExerciseTemplateSet.Id
  SetNumber
  Weight
  Repetitions
  IsCompleted
  IsWarmup
  Version                = обязательно после добавления concurrency token
  CompletedAtUtc?        = сейчас отсутствует в модели

CompleteWatchSetRequest
  OperationId: Guid
  ExpectedVersion: long
  ChangedAtUtc: DateTime (диагностический client timestamp)

CompleteWatchSetResponse
  Set: WatchSetResponse  (авторитетное server state)
  CurrentExerciseId?
  CurrentSetId?
```

Не следует притворно заполнять `StartedAtUtc`, `CompletedAtUtc`, `ActualWeight`/`PlannedWeight` или workout-level `Version`: текущая модель их не различает. Для Minimal MVP `Weight` и `Repetitions` — фактические существующие read-only значения.

## 12. Предлагаемые endpoints

Minimal MVP после соответствующих этапов:

```http
POST /api/watch/pairing-codes
POST /api/watch/pair
POST /api/watch/token/refresh
GET  /api/watch/workouts/active
POST /api/watch/sets/{setId}/complete
```

Позже, только в Extended Wear OS:

```http
GET    /api/watch/devices
DELETE /api/watch/devices/{deviceId}
PUT    /api/watch/sets/{setId}
POST   /api/watch/sets/{setId}/undo
POST   /api/watch/workouts/{workoutId}/finish
```

`GET active` должен фильтровать owner на сервере, использовать `WorkoutDay.Id` как workout identity, явно сортировать упражнения по `Exercise.Id`, подходы по `SetNumber`, и выбирать первый невыполненный подход в этом порядке. Completion должен проверять, что set принадлежит plan именно активного `WorkoutDay` authenticated watch owner.

## 13. Предлагаемые сущности подключения часов

### `WatchDevice`

- `Id: Guid` (server internal device identity);
- `UserId: string` FK Identity user;
- `DeviceId: string` app-generated stable identifier;
- `DisplayName`, `DeviceModel`, `AppVersion`, `Platform`;
- `CreatedAtUtc`, `LastSeenAtUtc`, `RevokedAtUtc`;
- `RefreshTokenHash`, `RefreshTokenExpiresAtUtc`;
- при необходимости token family/rotation metadata.

Ограничения: unique `(UserId, DeviceId)` либо global `DeviceId` после явного решения re-pair semantics; indexes по `UserId`, revocation и expiry; cascade при удалении пользователя.

### `WatchPairingCode`

- `Id: Guid`;
- `UserId: string`;
- `CodeHash`;
- `CreatedAtUtc`, `ExpiresAtUtc`, `UsedAtUtc`;
- `AttemptCount`.

Код случайный, hashed, single-use, short-lived и потребляется транзакционно; нужен rate limit.

### `WatchSyncOperation`

- `OperationId: Guid` primary/unique idempotency key;
- `WatchDeviceId: Guid`;
- `OperationType`;
- `EntityId` (set ID);
- `ReceivedAtUtc`;
- минимальный сохранённый authoritative result/version для одинакового replay response.

Для MVP отдельная таблица оправдана, потому что SQLite/единственный backend не имеет существующего idempotency store. Рекомендуемая retention policy: сохранять операции не менее максимального offline/retry окна (предварительно 30 дней), удалять подтверждённые записи периодической bounded cleanup и документировать, что клиент не повторяет операции старше окна. До реализации срок должен быть утверждён; бессрочное накопление недопустимо.

## 14. Предлагаемые миграции (только план, не созданы)

1. **Set concurrency**: добавить `Version INTEGER NOT NULL DEFAULT 0` как concurrency token минимум в `ExerciseTemplateSets`. Если `ExerciseSet` остаётся потенциальной будущей session-моделью, синхронно решить, нужен ли token и там; не расширять миграцию без реально используемого flow.
2. **Set integrity**: рассмотреть unique index `(ExerciseId, SetNumber)` после проверки существующих дублей. Он соответствует API validation и делает порядок/адресацию надёжнее.
3. **Watch devices/pairing**: таблицы `WatchDevices`, `WatchPairingCodes`, нужные FK/index/unique constraints.
4. **Idempotency**: `WatchSyncOperations` с unique `OperationId` и индексом retention cleanup по `ReceivedAtUtc`.
5. **Workout instance hardening (отдельное архитектурное решение)**: если продукт решит перестать мутировать template, нужна миграция/операция создания реальной active session. Это не следует незаметно включать в watch persistence migration.
6. **Optional timestamps/order**: `StartedAtUtc`, set `CompletedAtUtc`, explicit exercise `Order` требуют отдельного product/data migration решения; для Minimal MVP можно начать с детерминированного `Id` order и отсутствующих timestamps как nullable/неэкспонируемых.

Все будущие migration decisions должны учитывать SQLite: автообновляемый SQL Server `rowversion` недоступен.

## 15. Минимальный набор серверных изменений для Wear OS API

В порядке зависимости, без реализации на Этапе 0:

1. Выделить read operation активной тренировки с явным trusted user ID, детерминированной сортировкой и DTO projection.
2. Выделить узкую complete-set operation с ownership/active-workout validation; перевести web/mobile callers на общую бизнес-операцию по мере этапов, не дублировать логику.
3. Добавить set `Version` и conflict contract до offline synchronization.
4. Добавить watch pairing/device credentials поверх существующего Identity host, но с отдельной device identity и revocation.
5. Добавить idempotency store и `operationId` semantics.
6. Опубликовать только узкие Minimal MVP endpoints.
7. Добавить integration tests cross-user access, revoked device, duplicate operation и stale version.

## 16. Тесты и существующее покрытие

Имеются unit tests расчётов и integration tests с test host/database.

Полезное существующее покрытие:

- `ExerciseSetWeightTests`: individual per-set weight persistence;
- `MobileApiTests`: auth/session, calendar/today lifecycle, atomic completion snapshot, per-set weights и cross-user exercise access;
- `UserIsolationTests`: user-scoped services;
- `HistoryServiceTests`: immutable history data и per-set values;
- `MigrationTests`: migration/Identity базовые сценарии;
- `AccountDeletionServiceTests`: очистка связанных workout/session данных.

Отсутствует покрытие:

- set-level concurrency/conflict;
- set completion как отдельной операции;
- stable set ID в API;
- ordering active workout exercises;
- idempotency;
- watch auth/revocation/pairing;
- SignalR workout updates.

## 17. Архитектурные ограничения и риски

1. **Нет отдельного active aggregate.** `WorkoutDay` указывает на mutable template; параллельные клиенты работают с шаблоном.
2. **Нет optimistic concurrency.** Сейчас возможен silent overwrite; offline watch mutation небезопасна без версии.
3. **Whole-exercise writes.** Текущий API обновляет все подходы упражнения и увеличивает поверхность конфликта.
4. **Set ID отсутствует в mobile DTO.** `SetNumber` недостаточен для глобальной адресации.
5. **Порядок упражнений не является частью модели.** Web фактически использует ID order, active mobile API формально nondeterministic.
6. **Нет timestamps set completion.** Нельзя честно вернуть `CompletedAtUtc` или разрешать конфликты по серверному времени.
7. **Локальная дата сервера.** `DateTime.Today` и local `TimeProvider` completion timestamp могут отличаться от timezone пользователя; Wear OS API должен не смешивать client UTC с текущей local-date семантикой.
8. **Нет unique day-per-date constraint.** Теоретически несколько active candidates; API должен детерминировать/отклонять неоднозначность, а не брать случайный.
9. **Free workout не существует на сервере до finish.** Часы не смогут подключиться к нему без отдельного будущего изменения scope.
10. **TrainingSession вводит ложный след.** Таблицы существуют, но не являются production flow; их случайное использование создаст две конкурирующие модели.
11. **Нет SignalR domain infrastructure.** Это отдельная более поздняя работа, не скрытая reuse-задача.
12. **History теряет source IDs.** После завершения нельзя адресовать исходный set через snapshot.
13. **Шаблон сохраняет runtime fields.** Completion/status/weight changes могут влиять на последующее использование того же шаблона; Wear OS не должен самовольно менять эту семантику.

## 18. Рекомендуемая последовательность следующих этапов

Следовать `IMPLEMENTATION_PLAN.md` без перескоков:

1. Этап 1: выделить application operations чтения active workout и set-level completion; зафиксировать trusted user boundary и порядок.
2. Этап 2: добавить watch persistence и согласованный SQLite concurrency mechanism/idempotency storage.
3. Этап 3: pairing/authentication/revocation.
4. Этап 4: узкий Minimal Watch API (`active`, `complete`) и security/concurrency/idempotency tests.
5. Этап 5: минимальный pairing UI основного приложения.
6. Этапы 6–9: отдельный Wear OS client, mock flow, local offline queue, затем real API.
7. Этап 10: обязательная end-to-end контрольная точка Minimal MVP.
8. Только после неё Этапы 11–17 Extended Wear OS.

Перед Этапом 1 нужно принять одно явное решение: сохраняем ли на Minimal MVP текущую семантику mutable template как active state (минимальное изменение), либо сначала превращаем существующую `TrainingSession` в реальный active aggregate (существенная смена архитектуры и scope). Исходя из требования минимального coherent change, рекомендуется первый вариант с чётко задокументированным долгом, set version и узкими операциями. Переход на session aggregate следует делать отдельным осознанным этапом, а не маскировать внутри watch API.

## 19. Проверенные исходники

Основные файлы, на которых основан анализ:

- `WorkoutPlanner.Web/Data/WorkoutDbContext.cs` и текущий model snapshot;
- модели `TrainingPlan`, `WorkoutDay`, `Exercise`, `ExerciseTemplateSet`, `TrainingSession`, `TrainingSessionExercise`, `ExerciseSet`, `WorkoutHistory*`, `MobileSession`;
- `TodayWorkoutService`, `ExerciseService`, `WorkoutDayService`, `TrainingPlanService`, `WorkoutCompletionService`;
- application abstractions/contracts и `ApplicationContractMapper`;
- `WorkoutApiEndpoints`, `WorkoutLifecycleApiEndpoints`, `GymPlannerApiEndpoints`, mobile authorization/session implementation;
- web/mobile workout pages и API clients;
- существующий test project и релевантные integration/unit tests;
- migrations и `WorkoutDbContextModelSnapshot`.

## 20. Валидация Этапа 0

- `dotnet build WorkoutPlanner.Web/WorkoutPlanner.Web.csproj --no-restore` — успешно, 0 warnings, 0 errors.
- `dotnet test WorkoutPlanner.Web.Tests/WorkoutPlanner.Web.Tests.csproj --no-restore --no-build` — 55 passed, 2 failed, 57 total. Оба падения находятся в существующем `MobileApiTests` и связаны с `System.ArgumentException: The default FirebaseApp already exists` (`TelegramReply_CreatesUserInboxMessage_AndCanBeRead` и последующий `AdminPublicationsEndpoint_RejectsAnonymousAndUser_ButAllowsAdmin`). Документ Этапа 0 не изменяет исполняемый код и не мог исправлять этот отдельный test-host/Firebase lifecycle дефект.
- `dotnet build WorkoutPlanner.Web.slnx --no-restore` успел скомпилировать Domain, API Contracts, UI, Web, Tests и MAUI Android/iOS assemblies, но не завершился на MAUI packaging в разумный срок без дальнейшего вывода. Процесс был явно остановлен; успешным этот solution-level check не считается.
- Миграции не создавались и не применялись. Текущий EF model snapshot был проверен read-only.
- Device/emulator проверка неприменима: Этап 0 создаёт только документацию и не меняет пользовательское или исполняемое поведение.
- Запущенные для проверки `dotnet build`/`dotnet test` процессы завершены или остановлены; намеренно оставленных фоновых процессов нет.

## 21. Stop condition

Этап 0 завершён этим документом. Его выводы описывают состояние репозитория до реализации Этапа 1. Фактический результат Этапа 1 зафиксирован в `docs/wear-os/STAGE_1_IMPLEMENTATION.md`. Pairing, Wear OS-проект и watch migrations на Этапе 0 не создавались.
