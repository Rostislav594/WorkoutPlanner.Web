namespace WorkoutPlanner.Web.Application.Contracts;

public enum WatchWorkoutAvailability
{
    Active,
    None,
    Finished
}

public sealed record WatchActiveWorkoutResult(
    WatchWorkoutAvailability Availability,
    ActiveWorkout? Workout,
    int RestBetweenSetsSeconds = WatchRestDefaults.BetweenSetsSeconds,
    int RestBetweenExercisesSeconds = WatchRestDefaults.BetweenExercisesSeconds);

/// <summary>
/// Запасные длительности отдыха, если в тренировке нет упражнений.
/// Совпадают со значениями по умолчанию для шаблонов.
/// </summary>
public static class WatchRestDefaults
{
    public const int BetweenSetsSeconds = global::WorkoutPlanner.Api.Contracts.RestTimerDefaults.BetweenSetsSeconds;
    public const int BetweenExercisesSeconds = global::WorkoutPlanner.Api.Contracts.RestTimerDefaults.BetweenExercisesSeconds;
}

public enum CompleteWatchSetFailure
{
    None,
    ActiveWorkoutOrSetNotFound,
    WorkoutFinished,
    InvalidValues,
    Conflict,
    OperationIdConflict
}

public sealed record CompleteWatchSetResult(
    bool Succeeded,
    CompleteWatchSetFailure Failure,
    ActiveWorkoutSet? Set,
    int? CurrentExerciseId,
    int? CurrentSetId,
    DateTime? ProcessedAtUtc);

public enum FinishWatchWorkoutFailure
{
    None,
    ActiveWorkoutNotFound,
    WorkoutNotReady
}

public sealed record FinishWatchWorkoutResult(
    bool Succeeded,
    FinishWatchWorkoutFailure Failure,
    bool AlreadyFinished);
