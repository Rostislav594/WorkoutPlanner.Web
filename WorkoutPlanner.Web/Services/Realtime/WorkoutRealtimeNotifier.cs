using System.Collections.Concurrent;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Services.Realtime;

public sealed class WorkoutRealtimeNotifier(
    ILogger<WorkoutRealtimeNotifier> logger)
    : IWorkoutRealtimeNotifier
{
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<Guid, Func<WorkoutSetUpdatedNotification, Task>>>
        _subscribers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<Guid, Func<WorkoutFinishedNotification, Task>>>
        _finishedSubscribers = new(StringComparer.Ordinal);

    public IDisposable Subscribe(
        string userId,
        Func<WorkoutSetUpdatedNotification, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(handler);

        var subscriptionId = Guid.NewGuid();
        var userSubscribers = _subscribers.GetOrAdd(userId, _ => new());
        userSubscribers[subscriptionId] = handler;

        return new Subscription(() => Remove(userId, subscriptionId));
    }

    public IDisposable SubscribeWorkoutFinished(
        string userId,
        Func<WorkoutFinishedNotification, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(handler);

        var subscriptionId = Guid.NewGuid();
        var userSubscribers = _finishedSubscribers.GetOrAdd(userId, _ => new());
        userSubscribers[subscriptionId] = handler;
        return new Subscription(() => RemoveFinished(userId, subscriptionId));
    }

    public async Task PublishSetUpdatedAsync(
        string userId,
        WorkoutSetUpdatedNotification notification)
    {
        if (!_subscribers.TryGetValue(userId, out var userSubscribers))
            return;

        foreach (var (subscriptionId, handler) in userSubscribers.ToArray())
        {
            try
            {
                await handler(notification);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Workout update subscriber {SubscriptionId} failed for workout {WorkoutId} and set {SetId}.",
                    subscriptionId,
                    notification.WorkoutId,
                    notification.Set.SetId);
            }
        }
    }

    public async Task PublishWorkoutFinishedAsync(
        string userId,
        WorkoutFinishedNotification notification)
    {
        if (!_finishedSubscribers.TryGetValue(userId, out var userSubscribers))
            return;

        foreach (var (subscriptionId, handler) in userSubscribers.ToArray())
        {
            try
            {
                await handler(notification);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Workout finish subscriber {SubscriptionId} failed for workout {WorkoutId}.",
                    subscriptionId,
                    notification.WorkoutId);
            }
        }
    }

    private void Remove(string userId, Guid subscriptionId)
    {
        if (!_subscribers.TryGetValue(userId, out var userSubscribers))
            return;

        userSubscribers.TryRemove(subscriptionId, out _);
        if (userSubscribers.IsEmpty)
        {
            _subscribers.TryRemove(
                new KeyValuePair<
                    string,
                    ConcurrentDictionary<Guid, Func<WorkoutSetUpdatedNotification, Task>>>(
                    userId,
                    userSubscribers));
        }
    }

    private void RemoveFinished(string userId, Guid subscriptionId)
    {
        if (!_finishedSubscribers.TryGetValue(userId, out var userSubscribers))
            return;

        userSubscribers.TryRemove(subscriptionId, out _);
        if (userSubscribers.IsEmpty)
        {
            _finishedSubscribers.TryRemove(
                new KeyValuePair<
                    string,
                    ConcurrentDictionary<Guid, Func<WorkoutFinishedNotification, Task>>>(
                    userId,
                    userSubscribers));
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() =>
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
