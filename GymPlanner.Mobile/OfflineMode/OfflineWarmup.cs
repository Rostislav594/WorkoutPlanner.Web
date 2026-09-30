using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Authentication;
using GymPlanner.Mobile.Lifecycle;
using GymPlanner.Mobile.Offline;
using Microsoft.Extensions.Logging;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Пока связь есть, тихо подкачивает всё, что понадобится в зале без интернета.
/// </summary>
/// <remarks>
/// Экраны сохраняют только то, что сами открывали. Без подкачки человек, ни разу
/// не заглянувший в календарь, остался бы офлайн без сегодняшней тренировки.
/// Чтения идут через офлайн-клиенты, поэтому ответы сразу попадают в копию.
/// Графики по упражнениям не подкачиваются: они сохраняются, когда их открывают.
/// После удачной подкачки из копии убирается то, чего на сервере уже нет,
/// — чтобы память телефона не засорялась фото и графиками удалённого.
/// </remarks>
public sealed class OfflineWarmup(
    IWorkoutApiClient workouts,
    IWorkoutLifecycleApiClient lifecycle,
    IProfileApiClient profile,
    IWelcomeGuideApiClient welcomeGuide,
    IProgressApiClient progress,
    IInboxApiClient inbox,
    IExercisePhotoApiClient photos,
    MobileAuthenticationService authentication,
    ServerReachability reachability,
    OutboxSync outbox,
    OfflineRuntime runtime,
    MobileLifecycleService appLifecycle,
    TimeProvider timeProvider,
    ILogger<OfflineWarmup> logger)
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PendingRetryInterval = TimeSpan.FromSeconds(45);
    private bool _suspended;
    private int _running;
    private DateTimeOffset _lastRunUtc = DateTimeOffset.MinValue;
    private bool _wasOffline;

    public void Start()
    {
        _wasOffline = reachability.IsOffline;
        authentication.AuthenticationChanged += () => Run(force: true);
        reachability.Changed += ReachabilityChanged;
        // Ушли изменения, сделанные без связи: копию надо сверить с сервером.
        outbox.Flushed += _ => Run(force: true);
        appLifecycle.Resumed += () =>
        {
            _suspended = false;
            _ = FlushPendingAsync();
            Run(force: false);
        };
        appLifecycle.Suspended += () => _suspended = true;
        _ = RunAfterSignInRestoredAsync();
        _ = RetryPendingAsync();
    }

    /// <summary>
    /// Изменения могли застрять в очереди из-за медленной связи или сбоя сервера;
    /// пока приложение открыто, очередь периодически пробует уйти снова.
    /// </summary>
    private async Task RetryPendingAsync()
    {
        using var timer = new PeriodicTimer(PendingRetryInterval, timeProvider);
        while (await timer.WaitForNextTickAsync())
        {
            if (!_suspended)
                await FlushPendingAsync();
        }
    }

    private async Task FlushPendingAsync()
    {
        if (!authentication.IsAuthenticated || reachability.IsOffline)
            return;

        try
        {
            await outbox.FlushAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not send pending offline changes.");
        }
    }

    // Сохранённая сессия читается асинхронно, и событие входа при этом не приходит.
    private async Task RunAfterSignInRestoredAsync()
    {
        try
        {
            await authentication.InitializeAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning(exception, "Could not restore the session before the offline warm-up.");
            return;
        }

        Run(force: true);
    }

    private void ReachabilityChanged()
    {
        var isOffline = reachability.IsOffline;
        if (_wasOffline && !isOffline)
            Run(force: true);

        _wasOffline = isOffline;
    }

    private void Run(bool force)
    {
        if (!authentication.IsAuthenticated || reachability.IsOffline)
            return;
        if (!force && timeProvider.GetUtcNow() - _lastRunUtc < MinimumInterval)
            return;
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            // Даём первому экрану загрузиться без конкуренции за сеть.
            await Task.Delay(TimeSpan.FromSeconds(2), timeProvider);
            // Сначала то, что сделано без связи, иначе свежие данные с сервера его затрут.
            await outbox.FlushAsync();
            var plans = await workouts.GetPlansAsync();
            await workouts.GetExerciseDefinitionsAsync();

            var today = timeProvider.GetLocalNow().Date;
            await lifecycle.GetCalendarAsync(today.AddDays(-62), today.AddDays(120));

            var draft = await lifecycle.GetFreeWorkoutDraftAsync();
            if (draft.Value is { } activeDraft)
                await workouts.GetPlanAsync(activeDraft.TrainingPlanId);

            var todayWorkout = await lifecycle.StartTodayWorkoutAsync();
            await lifecycle.GetHistoryAsync();
            await profile.GetAsync();
            await welcomeGuide.GetStateAsync();
            var overview = await progress.GetOverviewAsync();
            await inbox.GetMessagesAsync();

            // Фото нужны на экране тренировки; остальные сохранятся, когда их откроют.
            if (todayWorkout.Value is { } workout)
            {
                foreach (var exercise in workout.TrainingPlan.Exercises.Where(x => x.HasPhoto))
                    await photos.DownloadAsync(exercise.Id);
            }

            if (!reachability.IsOffline)
            {
                _lastRunUtc = timeProvider.GetUtcNow();

                // Чистим, только когда всё, по чему судим о «лишнем», пришло с сервера:
                // иначе можно выбросить черновик идущей тренировки или нужные графики.
                if (plans.Succeeded && overview.Succeeded && draft.Succeeded)
                {
                    var removed = await runtime.CleanUpCopyAsync(draft.Value?.TrainingPlanId);
                    if (removed > 0)
                        logger.LogInformation("Removed {Count} stale offline records.", removed);
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning(exception, "Offline warm-up stopped early.");
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }
}
