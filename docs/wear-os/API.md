# Wear OS API — Minimal server API

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

### `DELETE /devices/{deviceId}`

Requires a main-app identity and scopes lookup to the current user. Returns `204` for
an owned device and `404` otherwise. Revocation invalidates refresh immediately;
already issued access tokens are rejected by the watch authorization policy because
it checks device state on every protected request.

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

Relevant failures:

- `404 WATCH_SET_NOT_FOUND` for a foreign or non-active set;
- `409 WORKOUT_SET_CONFLICT` with the current server set and current IDs;
- `409 WATCH_OPERATION_ID_CONFLICT` when an operation ID is reused for a different
  mutation;
- `410 WORKOUT_ALREADY_FINISHED`.

## Scope boundary

Generic set editing, undo and workout finish endpoints are not implemented. The API
does not expose EF entities, workout history, images or mutable template operations.
