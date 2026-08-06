namespace WorkoutPlanner.Api.Contracts;

public sealed record WorkoutProgressApiResponse(int TrainingPlanId, string WorkoutName, IReadOnlyList<ProgressSnapshotApiResponse> Snapshots, IReadOnlyList<ProgressChartPointApiResponse> Chart);
public sealed record ProgressSnapshotApiResponse(DateTime Date, double Score);
public sealed record ProgressChartPointApiResponse(string Label, decimal Percent);
public sealed record ExerciseProgressApiResponse(int TrainingPlanId, string WorkoutName, string ExerciseName, IReadOnlyList<ProgressChartPointApiResponse> Chart);
