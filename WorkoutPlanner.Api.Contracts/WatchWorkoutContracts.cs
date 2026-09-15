namespace WorkoutPlanner.Api.Contracts;

public sealed record WatchActiveWorkoutResponse(
    int WorkoutId,
    string WorkoutName,
    DateTime ScheduledDate,
    DateTime? StartedAtUtc,
    IReadOnlyList<WatchExerciseResponse> Exercises,
    int? CurrentExerciseId,
    int? CurrentSetId,
    int RestBetweenSetsSeconds,
    int RestBetweenExercisesSeconds,
    // От вида тренировки зависит, что часы скажут человеку в конце: шаблонная
    // уходит в историю сама, а после свободной на телефоне ждёт вопрос,
    // сохранять ли её шаблоном.
    bool IsFreeWorkout = false);

public sealed record WatchExerciseResponse(
    int ExerciseId,
    string Name,
    int Order,
    int? SupersetGroupId,
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

public sealed record UpdateWatchSetRequest(
    Guid OperationId,
    double ActualWeight,
    int ActualReps,
    DateTime ChangedAtUtc,
    long ClientVersion);

public sealed record UndoWatchSetRequest(
    Guid OperationId,
    DateTime ChangedAtUtc,
    long ClientVersion);

public sealed record CompleteWatchSetResponse(
    WatchSetResponse Set,
    int? CurrentExerciseId,
    int? CurrentSetId,
    DateTime ProcessedAtUtc);

public sealed record WatchSetMutationResponse(
    WatchSetResponse Set,
    int? CurrentExerciseId,
    int? CurrentSetId,
    DateTime ProcessedAtUtc);

public sealed record FinishWatchWorkoutResponse(
    int WorkoutId,
    bool AlreadyFinished);

public sealed record WatchSetConflictResponse(
    string Type,
    string Title,
    int Status,
    string Code,
    string Detail,
    WatchSetResponse? Set,
    int? CurrentExerciseId,
    int? CurrentSetId);
