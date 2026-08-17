---
name: wear-os-integration
description: Rules and staged workflow for implementing Wear OS integration in WorkoutPlanner, prioritizing a minimal current-set MVP before extended editing, SignalR, conflict UI, and full workout controls.
---

# Wear OS Integration Skill

## Purpose

Use this skill whenever working on functionality related to connecting Wear OS watches to `WorkoutPlanner`.

The goal is to build a secure, maintainable and offline-capable Wear OS client that can manage an active workout without duplicating server business logic. Delivery must be incremental: first prove the minimal current-set workout loop on real watch infrastructure, then add advanced editing and synchronization features.

This skill applies to:

- server-side watch integration;
- device pairing;
- device authentication;
- active workout API;
- set completion and editing;
- Wear OS application code;
- local Room storage;
- offline synchronization;
- WorkManager jobs;
- SignalR propagation;
- connected devices UI;
- tests and documentation.

---

## 1. Mandatory first step

Before changing code:

1. Find the repository root.
2. Read the relevant solution and project files.
3. Read:

```text
docs/wear-os/IMPLEMENTATION_PLAN.md
```

4. If it exists, read:

```text
docs/wear-os/CURRENT_ARCHITECTURE_ANALYSIS.md
```

5. Inspect the current implementation of:
   - authentication;
   - users;
   - active workouts;
   - completed workouts;
   - exercises;
   - sets;
   - per-set weight;
   - repetitions;
   - workout completion;
   - database context;
   - migrations;
   - API;
   - SignalR;
   - tests.
6. Use actual project naming and patterns.
7. Do not assume the architecture described in planning documents exactly matches the repository.

If the user requests a specific stage, execute only that stage.

---

## 2. Scope discipline

### Do

- make the smallest coherent change required by the current stage;
- preserve existing features;
- reuse existing application services;
- align with the current coding style;
- update tests together with production code;
- keep C# and Kotlin concerns separated;
- document important architectural decisions;
- stop after completing the requested stage.

### Do not

- implement all stages in one run;
- rewrite unrelated modules;
- rename large parts of the project without need;
- move the whole solution to a new architecture;
- introduce microservices for this feature;
- add Apple Watch support;
- add health sensor integration unless explicitly requested;
- add automatic weight progression;
- replace existing authentication globally unless required;
- commit or push without explicit instruction.

### 2.1 Minimal MVP first

Treat Wear OS integration as two delivery tiers.

**Tier A — Minimal MVP** must be completed and validated before Tier B starts. Its workout loop is:

```text
Pair watch
  ↓
Load active workout
  ↓
Show current exercise
  ↓
Show current set
  ↓
One-tap set completion
  ↓
Start local rest timer
  ↓
Show next set / next exercise
  ↓
Synchronize safely with backend
```

Tier A may display planned/actual weight and repetitions when the existing model already provides them, but they are read-only on the watch.

Tier A includes only what is necessary to make this loop reliable:

- pairing and watch authentication;
- active workout retrieval;
- current exercise and current set selection;
- one-tap set completion;
- durable local persistence of the completion action;
- rest timer;
- transition to the next incomplete set or exercise;
- retry after temporary connectivity loss;
- minimal sync state;
- end-to-end validation against the real backend.

Do not block Tier A on:

- editing weight;
- editing repetitions;
- rich workout overview;
- finishing the whole workout from the watch;
- full connected-device management UI;
- SignalR live updates;
- advanced conflict-resolution UI;
- complex synchronization dashboards.

**Tier B — Extended Wear OS** adds those capabilities only after the Minimal MVP milestone passes validation.

If the implementation plan assigns a feature to a later tier, do not pull it into the current stage merely because it seems convenient.

---

## 3. Core architecture rules

### 3.1 Server is the source of truth

The backend is authoritative for:

- user identity;
- device access;
- active workout ownership;
- completed state;
- workout history;
- final weights and repetitions;
- workout completion;
- conflict resolution.

The watch may maintain a local working copy, but it must not become the final authority.

### 3.2 Wear OS is a client

The Wear OS application:

- must not access the database directly;
- must not reference Entity Framework entities;
- must not contain server secrets;
- must not duplicate core workout business rules;
- must communicate through versioned DTOs and authenticated API calls;
- must work temporarily without a network connection.

### 3.3 Reuse business logic

Blazor UI and Watch API should use the same application-level operations whenever possible.

Do not implement two separate versions of:

- completing a set;
- changing reps;
- changing weight;
- finishing a workout;
- checking ownership;
- writing history.

### 3.4 DTO boundaries

Never return EF Core entities directly from Watch API.

Use explicit request and response DTOs.

DTOs must contain only fields required by the watch.

---

## 4. Project layout guidance

Preferred repository layout:

```text
WorkoutPlanner/
├── WorkoutPlanner.Web/
├── WorkoutPlanner.Api/              # only if the project already separates API
├── WorkoutPlanner.Core/             # only if such separation already exists
├── WorkoutPlanner.WearOS/
├── docs/
│   └── wear-os/
│       ├── IMPLEMENTATION_PLAN.md
│       ├── CURRENT_ARCHITECTURE_ANALYSIS.md
│       ├── ARCHITECTURE.md
│       ├── API.md
│       ├── PAIRING.md
│       └── OFFLINE_SYNC.md
└── .codex/
    └── skills/
        └── wear-os-integration/
            └── SKILL.md
```

Do not create extra .NET projects merely to match this diagram. Adapt to the actual repository.

The Wear OS module is expected to be a separate Gradle/Kotlin project and does not need to be part of the .NET solution.

---

## 5. Server-side coding rules

### 5.1 Application services

Prefer explicit application operations.

Examples:

```text
GetActiveWorkout
UpdateWorkoutSet
CompleteWorkoutSet
UndoWorkoutSet
FinishWorkout
CreateWatchPairingCode
PairWatchDevice
RefreshWatchToken
RevokeWatchDevice
```

Use actual project naming conventions.

### 5.2 Ownership validation

Every watch operation must derive the user from the authenticated watch identity.

Never trust:

```text
UserId
OwnerId
AccountId
```

from request JSON.

Validate that:

- the device is active;
- the device belongs to the user;
- the workout belongs to the user;
- the set belongs to the workout;
- the workout is still active.

### 5.3 Cancellation

Async server operations should accept and propagate `CancellationToken`.

### 5.4 Transactions

Use transactions where a multi-step write must be atomic, especially:

- pairing and consuming a code;
- refreshing and rotating tokens;
- finishing a workout;
- applying an idempotent operation and storing its operation ID.

### 5.5 Time

Store server timestamps in UTC.

Use names such as:

```text
CreatedAtUtc
UpdatedAtUtc
CompletedAtUtc
LastSeenAtUtc
ExpiresAtUtc
```

Do not rely on the watch clock as the sole trusted timestamp.

Client timestamps may be stored for diagnostics or conflict handling, but server time remains authoritative.

### 5.6 Logging

Use structured logging.

Log:

- device ID or internal watch device ID;
- endpoint/action;
- success/failure category;
- operation ID;
- status code;
- duration.

Never log:

- access token;
- refresh token;
- full pairing code;
- passwords;
- authorization header;
- sensitive user data.

---

## 6. Pairing and authentication rules

### 6.1 Pairing flow

Preferred flow:

1. Authenticated user creates a short-lived code in the main app.
2. Code is entered on the watch.
3. Watch sends code and device metadata.
4. Server validates and consumes the code atomically.
5. Server creates or updates the watch device.
6. Server returns device credentials.
7. User can later revoke the device.

### 6.2 Pairing code

A pairing code must be:

- random;
- short-lived;
- single-use;
- rate-limited;
- stored as a hash;
- invalid after successful pairing;
- invalid after expiration.

Do not use predictable sequential codes.

Do not reveal whether a specific account exists.

### 6.3 Tokens

Preferred properties:

- short-lived access token;
- longer-lived refresh token;
- refresh token rotation;
- refresh token stored as a hash server-side;
- explicit watch/device claims;
- revocation support;
- token expiry;
- audience and issuer validation.

Adapt to the project’s existing authentication stack.

Do not invent a parallel authentication framework if the existing one can securely support watch devices.

### 6.4 Device identity

The device should have a stable app-generated identifier, but server-issued credentials remain the real security boundary.

Do not treat device model, Bluetooth name, Android ID, or display name as authentication.

---

## 7. Watch API design rules

### 7.1 General

Use predictable REST semantics unless the project already follows another consistent pattern.

Recommended API prefix:

```text
/api/watch
```

Consider API versioning when the project supports it.

### 7.2 Recommended endpoints

Minimal MVP endpoints:

```http
POST   /api/watch/pairing-codes
POST   /api/watch/pair
POST   /api/watch/token/refresh
GET    /api/watch/workouts/active
POST   /api/watch/sets/{setId}/complete
```

The set-completion route may use another REST shape if the existing project conventions strongly prefer it. The important constraint is that Minimal MVP exposes a narrow completion operation rather than a generic write surface that accidentally enables later-stage editing.

Extended-stage endpoints may add:

```http
GET    /api/watch/devices
DELETE /api/watch/devices/{deviceId}
PUT    /api/watch/sets/{setId}
POST   /api/watch/sets/{setId}/undo
POST   /api/watch/workouts/{workoutId}/finish
```

Adapt all names to project conventions. Do not implement extended endpoints during Minimal MVP stages unless the stage explicitly requires them.

### 7.3 HTTP behavior

Use meaningful status codes:

```text
200 OK
201 Created
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
410 Gone
422 Unprocessable Entity
429 Too Many Requests
500 Internal Server Error
503 Service Unavailable
```

Do not return `200 OK` for every failure.

### 7.4 Problem details

Use a consistent problem details format.

Error responses should provide stable machine-readable codes, for example:

```text
WATCH_PAIRING_CODE_INVALID
WATCH_PAIRING_CODE_EXPIRED
WATCH_DEVICE_REVOKED
NO_ACTIVE_WORKOUT
WORKOUT_ALREADY_FINISHED
WORKOUT_SET_CONFLICT
OPERATION_ALREADY_PROCESSED
```

Do not expose stack traces.

---

## 8. Idempotency and synchronization rules

### 8.1 Every offline mutation needs an operation ID

Each watch-side write operation must have a client-generated GUID:

```text
operationId
```

The server must ensure that receiving the same operation more than once does not apply it more than once.

### 8.2 Server response

After updating a set, return the authoritative current server state.

Do not return only `{ success: true }`.

### 8.3 Ordering

Operations affecting the same set should preserve order.

Possible strategies:

- local sequence number;
- entity version;
- operation creation time plus version;
- serialized per-entity queue.

Choose one and document it.

### 8.4 Conflict strategy

For MVP, prefer explicit conflict detection over silent merging.

On conflict:

1. return `409 Conflict`;
2. return current server state;
3. keep the local operation visible as conflicted;
4. refresh local data;
5. apply a deterministic resolution rule or ask the user when necessary.

Do not silently overwrite newer server data.

### 8.5 Retry

Retry only transient failures:

- timeout;
- no network;
- 408;
- 429, respecting retry information;
- selected 5xx responses.

Do not endlessly retry:

- 400;
- 401 without successful refresh;
- 403;
- permanent 404;
- validation errors;
- unresolved 409 conflict.

Use exponential backoff.

---

## 9. Offline-first Wear OS rules

### 9.1 Local-first UI

When the user changes a set:

1. update local Room data immediately;
2. update the UI immediately;
3. create a pending operation;
4. schedule synchronization;
5. show sync status unobtrusively.

The user should not wait for the network to mark a set complete.

### 9.2 Persistence

Pending operations must survive:

- process death;
- watch restart;
- temporary loss of network;
- app backgrounding.

### 9.3 WorkManager

Use WorkManager for durable background sync.

Use CoroutineWorker when appropriate.

Apply constraints only when necessary.

Do not schedule a separate worker for every tap if operations can be batched safely.

### 9.4 Local database

Room entities should be separate from network DTOs and domain models where practical.

Do not store access or refresh tokens in ordinary Room columns.

### 9.5 Token storage

Use Android Keystore-backed secure storage.

Do not hardcode tokens.

Do not print tokens to Logcat.

---

## 10. Wear OS UI rules

### 10.1 Platform

Use:

- Kotlin;
- Jetpack Compose for Wear OS;
- Material components intended for Wear OS;
- lifecycle-aware state collection;
- ViewModel;
- Coroutines and Flow.

### 10.2 Interaction

The main workout flow must require minimal taps.

For Minimal MVP, the primary screen is the **current set**, not a full workout dashboard. Prioritize:

- clear current exercise name;
- clear current set number;
- one large completion control;
- optional read-only weight and reps when already available;
- automatic transition into the rest timer after local completion;
- clear rest timer;
- automatic selection of the next incomplete set after rest;
- minimal, unobtrusive sync state.

Do not require the user to open an exercise list, edit fields, or navigate through multiple screens merely to complete the next set.

Extended stages may add workout overview, editing, undo, finishing, and richer sync/conflict screens.

### 10.3 Screen sizes

Support round and square watch screens.

Avoid placing critical controls near clipped corners.

Test common emulator profiles.

### 10.4 States

Every screen that loads data should handle:

```text
Loading
Content
Empty
Offline
Error
Unauthorized
Revoked
```

### 10.5 Haptics

Use restrained haptic feedback for:

- successful set completion;
- rest timer completion;
- critical error if appropriate.

Do not vibrate repeatedly for background sync failures.

### 10.6 Accessibility

- meaningful content descriptions;
- sufficient touch target sizes;
- no information communicated only by color;
- readable text sizes;
- avoid excessive text.

---

## 11. Rest timer rules

The rest timer should:

- start after successful local completion of a set;
- continue through recomposition;
- survive normal navigation;
- derive remaining time from timestamps rather than decrement-only memory state;
- handle app backgrounding;
- support pause, resume and skip when required;
- provide haptic feedback on completion.

Do not tie timer correctness to an active UI coroutine alone.

The rest timer is local UX state and does not need to block server synchronization.

---

## 12. SignalR rules

SignalR is optional for live Blazor updates, but when used:

- reuse existing infrastructure;
- authenticate connections;
- isolate events by user;
- send minimal payloads;
- avoid echo loops;
- update only the affected UI state;
- handle reconnects;
- do not use SignalR as the only persistence path.

Persistence must complete through the server application layer. SignalR only propagates notifications.

---

## 13. Database and migration rules

Before creating a migration:

1. inspect the current provider;
2. inspect naming conventions;
3. inspect recent migrations;
4. identify delete behavior;
5. identify key types;
6. identify user key type.

Migration requirements:

- deterministic names;
- required indexes;
- unique constraints;
- safe defaults;
- reversible `Down` where applicable;
- no destructive column drops without explicit approval;
- no manual editing of generated snapshots unless necessary and understood.

Run the project’s standard migration and build checks.

---

## 14. Testing rules

### 14.1 Server

Test:

- authorization;
- ownership;
- pairing expiry;
- pairing reuse;
- token refresh;
- token revocation;
- idempotency;
- concurrency;
- conflict responses;
- cross-user isolation;
- workout completion.

### 14.2 Wear OS

Test:

- reducers/ViewModels;
- repository behavior;
- local database writes;
- pending operation creation;
- retry classification;
- token refresh;
- conflict handling;
- timer behavior;
- state restoration.

### 14.3 Integration tests

Prefer integration tests for security-sensitive endpoints.

Use a test database compatible with the real provider where practical.

### 14.4 No false success

Do not report success if:

- build was not run;
- tests were not run;
- an emulator was not available;
- migration was created but not validated.

State exactly what was and was not verified.

---

## 15. Security checklist

Before considering a stage complete, verify:

- [ ] no client-supplied user ID is trusted;
- [ ] watch device is authenticated;
- [ ] revoked device is rejected;
- [ ] pairing codes expire;
- [ ] pairing codes are single-use;
- [ ] pairing codes are hashed;
- [ ] pairing endpoint is rate-limited;
- [ ] refresh token is not stored in plaintext server-side;
- [ ] tokens are not logged;
- [ ] DTOs do not expose unnecessary data;
- [ ] cross-user access is tested;
- [ ] HTTPS is expected outside local development;
- [ ] operation replay does not duplicate changes;
- [ ] server errors do not expose internals.

---

## 16. Performance rules

- avoid loading full workout history for the active workout screen;
- avoid returning images or large descriptions to the watch unless needed;
- keep payloads compact;
- avoid N+1 EF queries;
- use projection to DTOs;
- paginate device lists if they could grow;
- avoid excessive polling;
- use a reasonable refresh policy;
- batch pending operations where safe;
- avoid waking the watch unnecessarily.

Battery usage is a first-class constraint.

---

## 17. Configuration rules

Use environment-specific configuration for:

- API base URL;
- token issuer;
- token audience;
- token lifetime;
- pairing code lifetime;
- maximum pairing attempts;
- sync interval;
- logging verbosity.

Do not commit:

- production secrets;
- signing keys;
- private certificates;
- real tokens;
- keystore passwords;
- private backend URLs when they are meant to remain secret.

Provide `.example` files when configuration templates are needed.

---

## 18. Documentation rules

When a stage introduces a major behavior, update the corresponding documentation.

Required topics:

- architecture;
- pairing;
- authentication;
- endpoints;
- errors;
- DTO schemas;
- conflict policy;
- offline sync;
- local development;
- emulator setup;
- release configuration;
- device revocation.

Code and documentation must not disagree.

---

## 19. Completion report format

After every requested stage, respond with:

### Completed

A concise description of what was implemented.

### Changed files

List each relevant file and its purpose.

### Architecture decisions

List important decisions and why they fit the existing project.

### Validation

State:

- build command and result;
- test command and result;
- migration validation;
- emulator validation, if applicable.

### Remaining risks

List unresolved concerns.

### Suggested commit

Provide one commit message, for example:

```text
wear: implement watch pairing API
```

### Stop condition

Explicitly confirm that no later stage was started.

---

## 20. Decision priorities

When requirements conflict, use this priority order:

1. security and user data isolation;
2. preservation of workout data;
3. compatibility with existing project behavior;
4. offline reliability;
5. maintainability;
6. testability;
7. user experience;
8. implementation speed.

Never sacrifice data integrity or authorization for convenience.

---

## 21. Definition of a good implementation

A good implementation:

- follows the current project architecture;
- uses a server source of truth;
- reaches and validates the Minimal MVP before expanding scope;
- supports offline watch interaction;
- prevents duplicate mutations;
- supports revocation;
- keeps the current-set watch UX simple;
- avoids data leakage;
- is testable;
- is documented;
- can be implemented and reviewed stage by stage.

A bad implementation:

- trusts IDs from the client;
- stores tokens in plaintext;
- requires permanent connectivity;
- duplicates server business logic in Kotlin;
- marks operations successful before local persistence;
- silently ignores conflicts;
- performs unrelated refactoring;
- changes multiple stages at once;
- reports success without running validation.
