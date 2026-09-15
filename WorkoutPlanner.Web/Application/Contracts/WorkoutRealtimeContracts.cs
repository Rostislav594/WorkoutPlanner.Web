namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record WorkoutSetUpdatedNotification(
    int WorkoutId,
    ActiveWorkoutSet Set);

public sealed record WorkoutFinishedNotification(int WorkoutId);
