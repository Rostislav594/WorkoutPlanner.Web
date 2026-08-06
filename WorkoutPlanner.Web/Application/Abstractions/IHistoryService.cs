using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IHistoryService
{
    Task AddHistoryAsync(
        WorkoutHistory history,
        CancellationToken cancellationToken = default);
    Task<List<WorkoutHistory>> GetHistoryAsync(
        CancellationToken cancellationToken = default);
    Task DeleteHistoryAsync(
        int id,
        CancellationToken cancellationToken = default);
}
