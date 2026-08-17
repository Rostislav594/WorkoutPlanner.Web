namespace WorkoutPlanner.Api.Contracts;

public sealed record TrainingPlanApiResponse(int Id, string WorkoutName, DateTime Date, IReadOnlyList<ExerciseApiResponse> Exercises);
public sealed record CreateTrainingPlanRequest(string WorkoutName);
public sealed record RenameTrainingPlanRequest(string WorkoutName);
public sealed record ExerciseApiResponse(int Id, string Name, int SetsCount, string Status, int TrainingPlanId, int? ExerciseDefinitionId, bool HasPhoto, IReadOnlyList<ExerciseSetApiResponse> Sets, int? SupersetGroupId = null);
public sealed record ExerciseSetApiResponse(int SetNumber, int Repetitions, double Weight, bool Completed, bool IsWarmup = false);
public sealed record SaveExerciseRequest(string Name, int SetsCount, string Status, int? ExerciseDefinitionId, IReadOnlyList<SaveExerciseSetRequest>? Sets, int? SupersetGroupId = null);
public sealed record SaveExerciseSetRequest(int SetNumber, int Repetitions, double Weight, bool Completed, bool IsWarmup = false);
public sealed record ExerciseDefinitionApiResponse(int Id, string Name);
