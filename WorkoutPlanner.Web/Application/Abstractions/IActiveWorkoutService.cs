using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IActiveWorkoutService
{
    Task<ActiveWorkout?> GetActiveWorkoutAsync(
        CancellationToken cancellationToken = default);

    Task<WorkoutSetUpdateResult> UpdateSetAsync(
        int setId,
        UpdateWorkoutSet command,
        CancellationToken cancellationToken = default);
}
