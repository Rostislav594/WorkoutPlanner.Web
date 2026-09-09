# Wear OS Minimal MVP synchronization contract

## Completion operation

Every locally persisted set-completion action must have a client-generated GUID
`operationId` and the set version observed by the watch. The client timestamp is
diagnostic only; server UTC remains authoritative.

The server applies completion and writes `WatchSyncOperation` in one serializable
SQLite transaction. The stored operation contains the authoritative response and is
retained for 30 days. A replay with the same device, operation type and set returns
that stored response. Reusing the ID for another set or mutation returns
`WATCH_OPERATION_ID_CONFLICT`.

Operations older than the 30-day server retention window must not be retried by the
future watch client.

## Ordering and conflicts

Ordering is enforced per set through `ExerciseTemplateSet.Version`:

1. the watch sends `clientVersion`;
2. the server updates only the set belonging to the authenticated user's active
   workout and only when its current version matches;
3. successful completion increments the version exactly once;
4. a stale version or already-completed set returns `409 WORKOUT_SET_CONFLICT` with
   the authoritative set and current set/exercise IDs.

The Minimal MVP does not silently merge or overwrite. A future offline client should
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

## Stage 8 local implementation

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
from being created per tap. Stage 8 uses `FakeWatchRemoteDataSource`; Retrofit and
the real backend are intentionally deferred to Stage 9.
