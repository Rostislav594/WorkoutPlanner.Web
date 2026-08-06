namespace WorkoutPlanner.Api.Contracts;

public sealed record WorkoutDayApiResponse(int Id, DateTime Date, int TrainingPlanId, bool IsCompleted);
public sealed record ScheduleWorkoutRequest(DateTime Date, int TrainingPlanId);
public sealed record TodayWorkoutApiResponse(WorkoutDayApiResponse Day, TrainingPlanApiResponse TrainingPlan);
public sealed record WorkoutHistoryApiResponse(int Id, string WorkoutName, DateTime Date, string Summary, bool SnapshotAvailable, IReadOnlyList<WorkoutHistoryExerciseApiResponse> Exercises);
public sealed record WorkoutHistoryExerciseApiResponse(string Name, string Status, IReadOnlyList<ExerciseSetApiResponse> Sets);
