using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IExerciseService
{
    Task<List<Exercise>> GetExercisesAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task AddExerciseAsync(
        Exercise exercise,
        CancellationToken cancellationToken = default);
    Task DeleteExerciseAsync(
        int exerciseId,
        CancellationToken cancellationToken = default);
    Task UpdateExerciseAsync(
        Exercise exercise,
        CancellationToken cancellationToken = default);
}

public interface IExerciseDefinitionService
{
    Task<List<ExerciseDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default);
}

public interface IExercisePhotoService
{
    Task<string> SavePhotoAsync(
        PhotoUpload upload,
        CancellationToken cancellationToken = default);
    bool TryDeletePhoto(string? photoPath);
}
