using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface ITrainingPlanService
{
    Task<List<TrainingPlan>> GetTrainingPlansAsync(
        CancellationToken cancellationToken = default);
    Task<TrainingPlan?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RenameAsync(
        int id,
        string workoutName,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет таймеры отдыха шаблона: отдых между упражнениями и отдых
    /// между подходами перечисленных упражнений. Незаданное не меняется.
    /// </summary>
    Task<RestTimersUpdateResult> UpdateRestTimersAsync(
        int id,
        int? restBetweenExercisesSeconds,
        IReadOnlyDictionary<int, int> restBetweenSetsByExerciseId,
        IReadOnlyDictionary<(int ExerciseId, int SetNumber), int> restAfterBySet,
        IReadOnlyDictionary<int, int> restAfterByExerciseId,
        CancellationToken cancellationToken = default);
}
