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
}
