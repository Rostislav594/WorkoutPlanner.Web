# Wear OS API

Base path: `/api/watch`. JSON responses use the existing ASP.NET Core problem-details
pipeline. Failures include a stable `code` extension where pairing clients need to
branch on the result.

## Endpoints

### `POST /pairing-codes`

Requires an authenticated main-app identity. Returns `201 Created`:

```json
{
  "code": "482913",
  "expiresAtUtc": "2026-09-04T10:30:00Z"
}
```

At most five codes per authenticated user may be requested per hour.

### `POST /pair`

Anonymous, limited to ten attempts per source IP per five minutes.

```json
{
  "code": "482913",
  "deviceId": "device-generated-stable-id",
  "displayName": "Galaxy Watch",
  "deviceModel": "SM-R960",
  "appVersion": "1.0.0"
}
```

Success returns a `WatchTokenResponse` with `tokenType`, `accessToken`, `expiresIn`
seconds and `refreshToken`.

Relevant failures:

- `400 WATCH_PAIRING_CODE_INVALID`;
- `409 WATCH_DEVICE_ALREADY_PAIRED`;
- `410 WATCH_PAIRING_CODE_EXPIRED`;
- `429` when rate-limited.

### `POST /pair/request`

Anonymous, shares the `/pair` rate limit. Started by the watch before anyone knows
which account it belongs to.

```json
{
  "deviceId": "device-generated-stable-id",
  "displayName": "WorkoutPlanner Wear",
  "deviceModel": "SM-R960",
  "appVersion": "0.1.0"
}
```

Returns `201` with `requestId`, `pollToken`, `approveUrl` and `expiresAtUtc`.
`requestId` travels to the phone inside the link; `pollToken` never leaves the watch
and is stored server-side only as a SHA-256 hash. Requests expire after
`WatchPairing:PairingRequestLifetime` (three minutes by default).

`approveUrl` is built from `WatchPairing:ApproveUrlTemplate`, which must contain the
`{requestId}` placeholder. Locally this is the app's custom scheme; production will
use a verified App Link once the domain hosts `assetlinks.json`.

### `POST /pair/status`

Anonymous, limited to 150 requests per source IP per five minutes, because the watch
polls every two seconds while the request is alive.

```json
{
  "requestId": "...",
  "pollToken": "..."
}
```

Returns `200` with `status` — `pending`, `approved`, `rejected` or `expired` — and,
exactly once alongside the first `approved`, a `WatchTokenResponse` in `tokens`.
A later poll of a completed request reports `expired`: tokens are issued once.

A wrong or missing `pollToken` returns `404 WATCH_PAIRING_REQUEST_NOT_FOUND`, so
knowing only the `requestId` grants nothing.

### `GET /pair/requests/{requestId}`

Main-app identity only. Returns what the phone shows on the confirmation screen:
`displayName`, `deviceModel`, `createdAtUtc`, `expiresAtUtc` and `status`.
No secrets and no tokens.

### `POST /pair/requests/{requestId}/approve`

### `POST /pair/requests/{requestId}/reject`

Main-app identity only; both return `204`. Approval binds the request to the
authenticated user — the watch never supplies a user ID.

Relevant failures, reported with `errorCodes`:

- `404 watch_pairing.request_not_found`;
- `409 watch_pairing.request_already_resolved` when the request was already answered;
- `409 watch_pairing.device_already_paired` when that watch belongs to another account;
- `410 watch_pairing.request_expired`.

### `POST /token/refresh`

Anonymous, limited to thirty attempts per source IP per five minutes. Accepts:

```json
{ "refreshToken": "protected-token" }
```

Success rotates both credentials and returns `WatchTokenResponse`. The previous
refresh token immediately becomes invalid. Invalid, expired, replayed or revoked
credentials return `401 WATCH_REFRESH_TOKEN_INVALID`.

### `GET /devices`

Requires a main-app identity. Returns only the current user's devices and never
returns token hashes.

### `PUT /devices/{deviceId}`

Requires a main-app identity. Accepts a bounded display name:

```json
{ "displayName": "Часы для кардио" }
```

The name is trimmed and must contain from 1 to 120 characters. The endpoint returns
the updated `WatchDeviceResponse`, `400` for invalid input, and `404` for a missing,
foreign, or already revoked device.

### `DELETE /devices/{deviceId}`

Requires a main-app identity and scopes lookup to the current user. Returns `204` for
an owned device and `404` otherwise. Revocation invalidates refresh immediately;
already issued access tokens are rejected by the watch authorization policy because
it checks device state on every protected request.

### `DELETE /devices`

Requires a main-app identity and revokes every active watch owned by the current
user. Returns `204`, including when there are no active devices. Refresh credentials
are replaced and expired; already issued access tokens are rejected by the same
per-request device-state check. Devices owned by other users are not affected.

### `GET /workouts/active`

Requires a valid watch access token. Returns the current user's active scheduled
workout, exercises ordered by persistent exercise ID and sets ordered by set number
then set ID. `CurrentExerciseId` and `CurrentSetId` identify the first incomplete set.

The wire model exposes the existing per-set `weight`, `repetitions`, completion flag
and concurrency `version`. It does not invent planned/actual values that the current
domain model cannot distinguish. `StartedAtUtc` is nullable and currently `null`
because no truthful start timestamp exists.

Relevant failures:

- `404 NO_ACTIVE_WORKOUT`;
- `410 WORKOUT_ALREADY_FINISHED`;
- `401/403` for missing, invalid or revoked watch credentials.

### `POST /sets/{setId}/complete`

Requires a valid watch access token. This is intentionally a narrow completion
operation; weight and repetitions cannot be edited.

```json
{
  "operationId": "b92f4a4f-a588-4a7b-bf68-c19884f9ab44",
  "changedAtUtc": "2026-09-04T10:30:00Z",
  "clientVersion": 3
}
```

Success returns the authoritative completed set, its new version, server
`processedAtUtc`, and IDs of the next incomplete set/exercise. Repeating the same
operation ID for the same device, route and set returns the originally stored
response without changing the set again.

After the transaction commits successfully, the server publishes one user-scoped
`WorkoutSetUpdated` notification containing only the workout ID and authoritative
set state. An idempotent replay does not publish a second notification. Open
Blazor Server circuits for that authenticated user apply the newer set version and
deliver the resulting render diff over the existing Blazor SignalR connection;
subscribers belonging to other users are never invoked.

Relevant failures:

- `404 WATCH_SET_NOT_FOUND` for a foreign or non-active set;
- `409 WORKOUT_SET_CONFLICT` with the current server set and current IDs;
- `409 WATCH_OPERATION_ID_CONFLICT` when an operation ID is reused for a different
  mutation;
- `410 WORKOUT_ALREADY_FINISHED`.

### `PUT /sets/{setId}`

Requires a valid watch access token and updates the authoritative per-set weight and
repetition count. The existing domain model stores these values directly on each
set, so `actualWeight` and `actualReps` map to that set's `Weight` and `Repetitions`.
The operation does not change the completion flag.

```json
{
  "operationId": "e46c578e-c06d-49dc-a79f-525ee523d763",
  "actualWeight": 82.5,
  "actualReps": 7,
  "changedAtUtc": "2026-09-11T12:15:00Z",
  "clientVersion": 4
}
```

Weight must be finite and between 0 and 2000; repetitions must be between 1 and
1000. Success returns `WatchSetMutationResponse`: the authoritative set with its
incremented version, `processedAtUtc`, and the current incomplete set/exercise IDs.

### `POST /sets/{setId}/undo`

Requires a valid watch access token and changes a completed active-workout set back
to incomplete without altering its weight or repetitions.

```json
{
  "operationId": "3c389ab9-4ccd-4e4b-b21c-7fdfc98da1ad",
  "changedAtUtc": "2026-09-11T12:17:00Z",
  "clientVersion": 5
}
```

Success returns the same authoritative `WatchSetMutationResponse` shape as update.
Undoing an already-incomplete set returns a conflict rather than silently succeeding.

### Shared mutation rules

Complete, update and undo all use the authenticated watch identity; no client
`UserId` is accepted. Each request needs a non-empty `operationId`, a non-default
`changedAtUtc`, and a non-negative `clientVersion`.

The server applies the set change and stores its replay result in one serializable
transaction. An exact replay for the same device, operation type and set returns the
stored response. For update, the requested weight and repetitions must also match
the original operation. Reusing the operation ID for any other mutation or altered
update payload returns `409 WATCH_OPERATION_ID_CONFLICT`.

The current server `Version` must exactly equal `clientVersion`. A stale version or
an inapplicable semantic transition returns `409 WORKOUT_SET_CONFLICT` containing
the authoritative set plus current set/exercise IDs. The server never applies silent
last-write-wins. Successful update and undo operations publish the same user-scoped
post-commit realtime notification as completion.

Other relevant failures are `400` for invalid input, `404 WATCH_SET_NOT_FOUND` for a
foreign or non-active set, and `410 WORKOUT_ALREADY_FINISHED`.

### `POST /workouts/{workoutId}/finish`

Идентификатор определяет путь завершения. Положительный — запланированная
тренировка, закрывается её `WorkoutDay`. Отрицательный — свободная тренировка:
на сервере она живёт черновиком плана (`TrainingPlan.IsFreeDraft`), и завершение
переносит её в историю, а сам черновик удаляет в той же транзакции.

Правило одно для обоих видов: незакрытые подходы завершать нельзя — `409`.

Повторный запрос ведёт себя по-разному, потому что различается след завершения.
У запланированной тренировки день остаётся с флагом `IsCompleted`, поэтому ответ
`200` с `alreadyFinished: true`. У свободной черновик удалён и опознать её больше
не по чему — ответ `404`.

Ответ `WatchActiveWorkoutResponse` содержит `isFreeWorkout`: по нему часы
выбирают текст итогового экрана. После свободной тренировки человека отправляют
в приложение, где ждёт вопрос о сохранении шаблона.

Requires a valid watch access token. The route accepts no request body. The server
rechecks that the device is active, the workout belongs to the authenticated user,
is scheduled for the current local day, and every set is completed. It then calls
the existing workout-completion service, so history creation and completion side
effects are shared with the main application rather than duplicated in the watch
API.

```json
{
  "workoutId": 42,
  "alreadyFinished": false
}
```

Repeating the request after a successful finish returns `200` with
`alreadyFinished: true`; it does not create a second history entry or publish a
second realtime event. The first successful request publishes a user-scoped
`WorkoutFinished` notification only after persistence succeeds.

Relevant failures:

- `404 WATCH_WORKOUT_NOT_FOUND` for a missing, foreign, wrong-day workout or an
  inactive/revoked device that reaches the service boundary;
- `409 WORKOUT_NOT_READY_TO_FINISH` while any set is incomplete;
- `401/403` for missing, invalid or revoked watch credentials.

## Scope boundary

Workout finish is implemented without a duplicate history pipeline or a duplicate
workout-detail endpoint. The API does not expose EF entities, workout history,
images or mutable template operations.
