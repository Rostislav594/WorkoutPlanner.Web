using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IExerciseService
{
    Task<List<Exercise>> GetExercisesAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task<Exercise?> GetByIdAsync(
        int exerciseId,
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
    Task<bool> ExistsAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public interface IExercisePhotoService
{
    Task<string> SavePhotoAsync(
        PhotoUpload upload,
        CancellationToken cancellationToken = default);
    Task<PhotoDownload?> OpenPhotoAsync(
        string photoPath,
        CancellationToken cancellationToken = default);
    bool TryDeletePhoto(string? photoPath);
}

public interface IExercisePhotoApplicationService
{
    Task<ExercisePhotoMutationResult> SaveAsync(
        int exerciseId,
        PhotoUpload upload,
        CancellationToken cancellationToken = default);
    Task<PhotoDownload?> OpenAsync(
        int exerciseId,
        CancellationToken cancellationToken = default);
    Task<ExercisePhotoMutationResult> DeleteAsync(
        int exerciseId,
        CancellationToken cancellationToken = default);
}
