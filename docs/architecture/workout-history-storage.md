# Workout history storage

## Decision

`WorkoutHistory.Details` is the authoritative record shown to users. It stores an
immutable JSON snapshot of the completed workout: exercise names, status,
per-set weight, repetitions, completion flags, and photo references as they
existed when the workout was finished.

The normalized `TrainingSession`, `TrainingSessionExercise`, and `ExerciseSet`
tables are an analytics model. They are not currently the source used to render
history and must not overwrite or reinterpret an existing snapshot.

## Compatibility rules

- Existing `WorkoutHistory` rows and their JSON are retained unchanged.
- Readers tolerate legacy JSON and missing optional fields.
- New writers use the application history DTOs rather than EF entities.
- The authenticated server identity supplies ownership; history requests do not
  contain a trusted `UserId`.
- Per-set weight is preserved verbatim. History processing must not apply
  automatic progression or recompute a shared exercise weight.
- Photo paths are internal references. A client receives photos only through an
  authenticated ownership-checked endpoint.

## Future analytics migration

If normalized analytics becomes necessary, introduce versioned snapshot
contracts and dual-write new completions first. Backfill normalized rows from
snapshots in an idempotent migration or background job, record conversion
failures without changing the original JSON, and add compatibility tests before
switching any analytical reader. The immutable snapshot remains available for
display and audit even after a successful backfill.
