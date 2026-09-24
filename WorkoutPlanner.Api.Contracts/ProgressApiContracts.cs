namespace WorkoutPlanner.Api.Contracts;

public sealed record WorkoutProgressApiResponse(int TrainingPlanId, string WorkoutName, IReadOnlyList<ProgressSnapshotApiResponse> Snapshots, IReadOnlyList<ProgressChartPointApiResponse> Chart);
public sealed record ProgressSnapshotApiResponse(DateTime Date, double Score);
public sealed record ProgressChartPointApiResponse(DateTime Date, decimal Percent);
public sealed record ExerciseProgressApiResponse(int TrainingPlanId, string WorkoutName, string ExerciseName, IReadOnlyList<ProgressChartPointApiResponse> Chart);
public sealed record ExerciseWeightTrendApiResponse(string WorkoutName, string ExerciseName, double CurrentWeight, double WeightChange, IReadOnlyList<double> Weights);
public sealed record WorkoutScoreChangeApiResponse(int TrainingPlanId, string WorkoutName, decimal ChangePercent, IReadOnlyList<double> Scores);
public sealed record ProgressOverviewApiResponse(IReadOnlyList<ExerciseWeightTrendApiResponse> Exercises, IReadOnlyList<WorkoutScoreChangeApiResponse> Workouts);
