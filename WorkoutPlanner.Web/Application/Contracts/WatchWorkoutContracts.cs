namespace WorkoutPlanner.Web.Application.Contracts;

public enum WatchWorkoutAvailability
{
    Active,
    None,
    Finished
}

public sealed record WatchActiveWorkoutResult(
    WatchWorkoutAvailability Availability,
    ActiveWorkout? Workout);

public enum CompleteWatchSetFailure
{
    None,
    ActiveWorkoutOrSetNotFound,
    WorkoutFinished,
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
