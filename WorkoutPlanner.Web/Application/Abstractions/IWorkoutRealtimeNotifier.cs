using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IWorkoutRealtimeNotifier
{
    IDisposable Subscribe(
        string userId,
        Func<WorkoutSetUpdatedNotification, Task> handler);

    IDisposable SubscribeWorkoutFinished(
        string userId,
        Func<WorkoutFinishedNotification, Task> handler);

    Task PublishSetUpdatedAsync(
        string userId,
        WorkoutSetUpdatedNotification notification);

    Task PublishWorkoutFinishedAsync(
        string userId,
        WorkoutFinishedNotification notification);
}
