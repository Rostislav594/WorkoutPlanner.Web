namespace WorkoutPlanner.Api.Contracts;

public sealed record WatchActiveWorkoutResponse(
    int WorkoutId,
    string WorkoutName,
    DateTime ScheduledDate,
    DateTime? StartedAtUtc,
    IReadOnlyList<WatchExerciseResponse> Exercises,
    int? CurrentExerciseId,
    int? CurrentSetId);

public sealed record WatchExerciseResponse(
    int ExerciseId,
    string Name,
    int Order,
    IReadOnlyList<WatchSetResponse> Sets);

public sealed record WatchSetResponse(
    int SetId,
    int SetNumber,
    double Weight,
    int Repetitions,
    bool IsCompleted,
    bool IsWarmup,
    long Version);

public sealed record CompleteWatchSetRequest(
    Guid OperationId,
    DateTime ChangedAtUtc,
    long ClientVersion);

public sealed record CompleteWatchSetResponse(
    WatchSetResponse Set,
    int? CurrentExerciseId,
    int? CurrentSetId,
    DateTime ProcessedAtUtc);

public sealed record WatchSetConflictResponse(
    string Type,
    string Title,
    int Status,
    string Code,
    string Detail,
    WatchSetResponse? Set,
    int? CurrentExerciseId,
    int? CurrentSetId);
