using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IWatchWorkoutService
{
    Task<WatchActiveWorkoutResult> GetActiveWorkoutAsync(
        CancellationToken cancellationToken = default);

    Task<CompleteWatchSetResult> CompleteSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default);

    Task<CompleteWatchSetResult> UpdateSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        double weight,
        int repetitions,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default);

    Task<CompleteWatchSetResult> UndoSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default);

    Task<FinishWatchWorkoutResult> FinishWorkoutAsync(
        Guid watchDeviceId,
        int workoutId,
        CancellationToken cancellationToken = default);
}
