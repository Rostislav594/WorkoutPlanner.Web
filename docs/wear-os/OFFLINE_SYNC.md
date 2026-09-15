# Wear OS Minimal MVP synchronization contract

## Set mutation operations

Every locally persisted `CompleteSet`, `UpdateSet` or `UndoSet` action must have a
client-generated GUID `operationId` and the set version observed by the watch. The
client timestamp is diagnostic only; server UTC remains authoritative.

The server applies the mutation and writes `WatchSyncOperation` in one serializable
SQLite transaction. The stored operation contains the authoritative response and is
retained for 30 days. A replay with the same device, operation type and set returns
that stored response; an `UpdateSet` replay must also contain the original weight and
repetitions. Reusing the ID for another set, mutation or altered update payload
returns `WATCH_OPERATION_ID_CONFLICT`.

Operations older than the 30-day server retention window must not be retried by the
future watch client.

## Ordering and conflicts

Ordering is enforced per set through `ExerciseTemplateSet.Version`:

1. the watch sends `clientVersion`;
2. the server updates only the set belonging to the authenticated user's active
   workout and only when its current version matches;
3. every successful mutation increments the version exactly once;
4. complete requires an incomplete set, undo requires a completed set, and update
   preserves the current completion flag;
5. a stale version or an invalid state transition returns
   `409 WORKOUT_SET_CONFLICT` with the authoritative set and current set/exercise IDs.

The server does not silently merge or overwrite. An offline client should
replace its local server snapshot with the conflict response, mark the pending action
as conflicted/resolved according to its UI policy, and not endlessly retry `409`.

## Retry classification

The watch client may retry network failures, timeouts, `408`, `429` respecting
server guidance, and selected `5xx` responses with exponential backoff. It must not
automatically retry validation errors, permanent `404`, `410`, unresolved `409`, or
`401` unless token refresh succeeds.

Server timestamps and operation results are authoritative. The server currently has
no per-set completion timestamp, so the API returns operation-level
`processedAtUtc` and does not fabricate `completedAtUtc`.

## Stage 9 client implementation

The current Wear OS client persists five separate Room models:

- `LocalWorkout`;
- `LocalExercise`;
- `LocalWorkoutSet`;
- `PendingSyncOperation`;
- `DeviceSessionMetadata`.

Credentials are not stored in Room. The existing Android Keystore-backed token
boundary remains the only intended credential store.

Completing a set is one Room transaction: it marks `LocalWorkoutSet` complete,
creates a GUID `operationId`, stores the `CompleteSet` payload and starts the local
rest deadline. The UI observes Room and therefore moves to rest without waiting for
network I/O. A global monotonic `sequenceNumber` serializes the Minimal MVP queue;
this also preserves ordering for operations targeting the same set.

`PendingSyncOperation` stores the required status, attempts and error metadata plus
`canRetry`, which distinguishes retryable transport failures from terminal failures.
An interrupted `Syncing` operation is reset to `Pending` when the queue processor
starts. `409` maps to `Conflict`, applies the authoritative set snapshot and is not
retried silently. An unauthorized response gets at most one refresh-and-resend in a
single processing attempt.

WorkManager uses one unique job named `pending-workout-sync` with a connected-network
constraint and exponential backoff. `ExistingWorkPolicy.KEEP` prevents one worker
from being created per tap. Production DI uses `RetrofitWatchRemoteDataSource`;
`FakeWatchRemoteDataSource` remains a test double only. A retryable token-refresh
response (`408`, `429`, selected `5xx`, timeout or offline) keeps the encrypted
refresh token and lets WorkManager retry. Invalid/revoked credentials clear the
local session and return the user to pairing.

## Stage 14 extended client queue

The Wear OS queue now persists and sends four logical operation types:

- `CompleteSet`;
- `UndoSet`;
- `UpdateWeight`;
- `UpdateReps`.

Both update types use the server's combined `UpdateSet` endpoint. Keeping distinct
local types makes the user-visible sync status precise while allowing adjacent
pending weight/repetition edits for one set to coalesce into one request. Coalescing
is allowed only while every replaced update is still `Pending`; an operation already
claimed by the worker is never rewritten.

All outstanding operations have one global monotonic `sequenceNumber` and are sent
strictly in that order. The expected version for a newly queued set mutation is:

```text
last confirmed server version + unresolved earlier operations for that set
```

This preserves per-set causality without treating the watch as authoritative. A
pending, unsent completion followed immediately by undo is cancelled locally as one
atomic Room transaction. Once completion has started syncing, undo is queued as a
separate versioned mutation. An edit after undo is likewise ordered after it.

Successful operations apply the authoritative server set and are deleted
immediately; the queue is not an audit log. A transient failure remains retryable.
Validation and other permanent failures remain visible until dismissed. A `409`
applies the authoritative response, marks the operation `Conflict`, and marks every
later unresolved operation for that set conflicted as well so stale dependent data
is never sent silently. Only explicitly retryable failures expose a retry action.

The client rejects new mutations when 100 unresolved operations are already stored.
This is a deliberate battery/storage bound; it surfaces an error instead of dropping
or overwriting user data. Server idempotency retention remains 30 days, so queued
operations must not be replayed beyond that window.

## Stage 15 finish barrier

Finishing a workout is a server-confirmed barrier, not another offline mutation.
After the last rest screen the local workout remains active and the UI enters
`ReadyToFinish`; it is not removed merely because all local sets are complete.

After explicit user confirmation the repository takes the following steps while
holding the same mutation mutex used by set actions:

1. directly drains the pending set-operation queue;
2. stops if transport requires retry;
3. stops and opens sync status if any failed, terminal or conflicted operation is
   still unresolved;
4. calls `POST /workouts/{workoutId}/finish` only when the queue is empty;
5. deactivates the local workout only after a successful or already-finished server
   response.

Therefore a network failure, rejected finish, process interruption or unresolved
conflict does not silently discard the local workout result. Concurrent UI and
WorkManager drains are serialized inside `SyncQueueProcessor`, preventing both
processors from claiming/resetting the same operation at once. The finish request
itself is safe to repeat on the server, so a lost success response can be retried.
