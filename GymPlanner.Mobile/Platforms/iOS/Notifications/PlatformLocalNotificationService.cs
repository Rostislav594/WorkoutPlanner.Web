using Foundation;
using UserNotifications;

namespace GymPlanner.Mobile.Notifications;

public sealed class PlatformLocalNotificationService : ILocalNotificationPlatform
{
    public async Task<NotificationOperationResult> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert |
            UNAuthorizationOptions.Sound |
            UNAuthorizationOptions.Badge);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Item1
            ? NotificationOperationResult.Success
            : NotificationOperationResult.Denied(
                "Разрешите уведомления в настройках приложения.");
    }

    public async Task<NotificationOperationResult> ScheduleAsync(
        LocalWorkoutReminder reminder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seconds = (reminder.NotifyAt - DateTimeOffset.Now).TotalSeconds;
        if (seconds <= 0)
        {
            return NotificationOperationResult.Failure(
                "Время напоминания должно быть в будущем.");
        }

        var content = new UNMutableNotificationContent
        {
            Title = "Пора тренироваться",
            Body = reminder.WorkoutName,
            Sound = UNNotificationSound.Default,
            UserInfo = NSDictionary.FromObjectAndKey(
                new NSString(reminder.Route),
                new NSString(IosNotificationNavigation.RouteKey))
        };
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(
            Math.Max(1, seconds),
            false);
        var request = UNNotificationRequest.FromIdentifier(
            GetIdentifier(reminder.WorkoutDayId),
            content,
            trigger);
        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
        cancellationToken.ThrowIfCancellationRequested();
        return NotificationOperationResult.Success;
    }

    public Task<NotificationOperationResult> CancelAsync(
        int workoutDayId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(
            [GetIdentifier(workoutDayId)]);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(
            [GetIdentifier(workoutDayId)]);
        return Task.FromResult(NotificationOperationResult.Success);
    }

    public Task<NotificationOperationResult> CancelAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
        UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
        return Task.FromResult(NotificationOperationResult.Success);
    }

    private static string GetIdentifier(int workoutDayId) =>
        $"workout-reminder-{workoutDayId}";
}

internal static class IosNotificationNavigation
{
    public const string RouteKey = "gymplanner.notification.route";

    public static void Open(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return;

        IPlatformApplication.Current?.Services
            .GetService<NotificationNavigationService>()?
            .Open(route);
    }
}

public sealed class WorkoutNotificationDelegate : UNUserNotificationCenterDelegate
{
    public override void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        var value = response.Notification.Request.Content.UserInfo[
            new NSString(IosNotificationNavigation.RouteKey)];
        IosNotificationNavigation.Open(value?.ToString());
        completionHandler();
    }

    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler) =>
        completionHandler(
            UNNotificationPresentationOptions.Banner |
            UNNotificationPresentationOptions.Sound);
}
