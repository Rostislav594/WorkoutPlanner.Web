namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record ActiveWorkout(
    int WorkoutId,
    int TrainingPlanId,
    string WorkoutName,
    DateTime ScheduledDate,
    IReadOnlyList<ActiveWorkoutExercise> Exercises);

public sealed record ActiveWorkoutExercise(
    int ExerciseId,
    string Name,
    int Order,
    int? SupersetGroupId,
    IReadOnlyList<ActiveWorkoutSet> Sets);

public sealed record ActiveWorkoutSet(
    int SetId,
    int SetNumber,
    double Weight,
    int Repetitions,
    bool Completed,
    bool IsWarmup,
    long Version);

public sealed record UpdateWorkoutSet(
    double Weight,
    int Repetitions,
    bool Completed,
    long ExpectedVersion);

public enum WorkoutSetUpdateFailure
{
    None,
    ActiveWorkoutOrSetNotFound,
    InvalidValues,
    Conflict
}

public sealed record WorkoutSetUpdateResult(
    bool Succeeded,
    WorkoutSetUpdateFailure Failure,
    ActiveWorkoutSet? Set);
