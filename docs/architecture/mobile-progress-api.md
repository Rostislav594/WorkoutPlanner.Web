# Mobile progress API

Progress routes use a training-plan identifier at the HTTP boundary. The server
first resolves that plan through `ITrainingPlanService`, which applies the
authenticated user's ownership filter, and only then passes its server-owned
name to `IProgressService`. A plan owned by another user is returned as `404`.

The API exposes workout snapshots and percentage chart points, exercise names
with recorded progress, exercise charts, and scoped clear operations. It does
not accept `UserId`, raw EF entities, or a score supplied by the client.

Scores continue to be created by the completion service from actual per-set
weights and repetitions. The API is read/clear only and introduces no automatic
weight or repetition progression. No schema migration is required.
