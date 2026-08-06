namespace WorkoutPlanner.Web.Api.Contracts;

public sealed record TrainingPlanApiResponse(
    int Id,
    string WorkoutName,
    DateTime Date,
    IReadOnlyList<ExerciseApiResponse> Exercises);

public sealed record CreateTrainingPlanRequest(string WorkoutName);

public sealed record RenameTrainingPlanRequest(string WorkoutName);

public sealed record ExerciseApiResponse(
    int Id,
    string Name,
    int SetsCount,
    string Status,
    int TrainingPlanId,
    int? ExerciseDefinitionId,
    bool HasPhoto,
    IReadOnlyList<ExerciseSetApiResponse> Sets);

public sealed record ExerciseSetApiResponse(
    int SetNumber,
    int Repetitions,
    double Weight,
    bool Completed);

public sealed record SaveExerciseRequest(
    string Name,
    int SetsCount,
    string Status,
    int? ExerciseDefinitionId,
    IReadOnlyList<SaveExerciseSetRequest>? Sets);

public sealed record SaveExerciseSetRequest(
    int SetNumber,
    int Repetitions,
    double Weight,
    bool Completed);

public sealed record ExerciseDefinitionApiResponse(int Id, string Name);
