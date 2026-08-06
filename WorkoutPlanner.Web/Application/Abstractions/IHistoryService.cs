using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IHistoryService
{
    Task AddHistoryAsync(
        WorkoutHistory history,
        CancellationToken cancellationToken = default);
    Task<List<WorkoutHistory>> GetHistoryAsync(
        CancellationToken cancellationToken = default);
    Task<WorkoutHistory?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteHistoryAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public interface IWorkoutCompletionService
{
    Task<WorkoutCompletionResult> CompleteTodayAsync(
        CancellationToken cancellationToken = default);
}
