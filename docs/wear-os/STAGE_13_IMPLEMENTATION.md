# Stage 13 — Extended Watch API

## Implemented scope

The Watch API now supports all three set mutations required by this stage:

- `POST /api/watch/sets/{setId}/complete`;
- `PUT /api/watch/sets/{setId}` for per-set weight and repetitions;
- `POST /api/watch/sets/{setId}/undo`.

No Wear OS UI, client queue, pairing, schema migration, workout-finish endpoint or
Stage 14 work is included.

## Domain and application reuse

All endpoints call `IWatchWorkoutService`. The shared mutation pipeline loads the
active workout through the existing `ActiveWorkoutService`, scopes it to the
authenticated user and authenticated watch device, mutates the existing
`ExerciseTemplateSet`, and returns the API projection rather than an EF entity.

The current domain already stores `Weight`, `Repetitions`, `Completed` and the
SQLite-compatible optimistic-concurrency `Version` on each individual set. No model
or database change was necessary. The existing active-workout response is already a
detailed ordered overview, so an additional overview endpoint would duplicate the
same data and was not added.

## Consistency and conflict behavior

Each mutation requires its own GUID `operationId` and the exact `clientVersion` last
observed by the watch. Mutation plus `WatchSyncOperation` replay record are committed
in one serializable transaction. A matching replay returns the stored authoritative
response without another write or realtime notification. Reuse across devices,
sets, operation types, or with altered update values returns
`409 WATCH_OPERATION_ID_CONFLICT`.

The server is the source of truth. Version mismatch, repeated complete, or repeated
undo returns deterministic `409 WORKOUT_SET_CONFLICT` with the authoritative set and
current incomplete set/exercise IDs; there is no last-write-wins path. Update changes
only the set's weight/repetitions, while undo preserves them.

After a successful commit, complete, update and undo publish the existing
user-scoped `WorkoutSetUpdated` notification so an open main-app session can refresh
from the authoritative state.

## Validation and boundaries

Update accepts finite weight from 0 through 2000 and repetitions from 1 through
1000. Every mutation rejects an empty operation ID, default client timestamp, or
negative version. The API never accepts a client-supplied `UserId`.

Workout finish remains deliberately unimplemented. The generalized mutation
pipeline, authoritative current IDs and reusable conflict response prepare the API
for that later operation without defining its business semantics prematurely.

## Verification

The integration suite covers completion and its replay, weight/repetition update and
its replay, undo, operation-ID reuse across mutation types, stale-version conflict,
invalid input, finished workout, foreign ownership and realtime notification
targeting. Build, complete Watch test-suite, model-change check and live API smoke
results are recorded in the task handoff after execution.
