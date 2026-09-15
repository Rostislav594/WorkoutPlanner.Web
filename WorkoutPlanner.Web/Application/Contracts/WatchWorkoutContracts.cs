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
/// Запасные длительности отдыха: используются, когда профиль ещё не создан.
/// Совпадают со значениями по умолчанию в <c>UserProfile</c>, иначе часы
/// показывали бы не то, что человек видит в приложении.
/// </summary>
public static class WatchRestDefaults
{
    public const int BetweenSetsSeconds = 90;
    public const int BetweenExercisesSeconds = 120;
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
