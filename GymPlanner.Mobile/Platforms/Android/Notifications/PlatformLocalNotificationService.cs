using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace GymPlanner.Mobile.Notifications;

public sealed class PlatformLocalNotificationService : ILocalNotificationPlatform
{
    public async Task<NotificationOperationResult> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return NotificationOperationResult.Success;

        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();

        return status == PermissionStatus.Granted
            ? NotificationOperationResult.Success
            : NotificationOperationResult.Denied(
                "Разрешите уведомления в настройках приложения.");
    }

    public Task<NotificationOperationResult> ScheduleAsync(
        LocalWorkoutReminder reminder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = Platform.AppContext;
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager is null)
        {
            return Task.FromResult(NotificationOperationResult.Failure(
                "Системная служба напоминаний недоступна."));
        }

        var pendingIntent = AndroidNotificationSupport.CreateAlarmIntent(
            context,
            reminder);
        alarmManager.SetAndAllowWhileIdle(
            AlarmType.RtcWakeup,
            reminder.NotifyAt.ToUnixTimeMilliseconds(),
            pendingIntent);
        return Task.FromResult(NotificationOperationResult.Success);
    }

    public Task<NotificationOperationResult> CancelAsync(
        int workoutDayId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = Platform.AppContext;
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager is null)
        {
            return Task.FromResult(NotificationOperationResult.Failure(
                "Системная служба напоминаний недоступна."));
        }

        var pendingIntent = AndroidNotificationSupport.CreateAlarmIntent(
            context,
            workoutDayId);
        alarmManager.Cancel(pendingIntent);
        pendingIntent.Cancel();
        (context.GetSystemService(Context.NotificationService) as NotificationManager)?
            .Cancel(workoutDayId);
        return Task.FromResult(NotificationOperationResult.Success);
    }
}

internal static class AndroidNotificationSupport
{
    public const string ChannelId = "workout-reminders";
    public const string RouteExtra = "gymplanner.notification.route";
    private const string TitleExtra = "gymplanner.notification.title";
    private const string BodyExtra = "gymplanner.notification.body";
    private const string WorkoutDayIdExtra = "gymplanner.notification.workout-day-id";

    public static PendingIntent CreateAlarmIntent(
        Context context,
        LocalWorkoutReminder reminder)
    {
        var intent = new Intent(context, typeof(WorkoutReminderReceiver));
        intent.PutExtra(RouteExtra, reminder.Route);
        intent.PutExtra(TitleExtra, "Пора тренироваться");
        intent.PutExtra(BodyExtra, reminder.WorkoutName);
        intent.PutExtra(WorkoutDayIdExtra, reminder.WorkoutDayId);
        return PendingIntent.GetBroadcast(
            context,
            reminder.WorkoutDayId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    public static PendingIntent CreateAlarmIntent(Context context, int workoutDayId)
    {
        var intent = new Intent(context, typeof(WorkoutReminderReceiver));
        return PendingIntent.GetBroadcast(
            context,
            workoutDayId,
            intent,
            PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable) ??
            PendingIntent.GetBroadcast(
                context,
                workoutDayId,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    public static void Show(Context context, Intent intent)
    {
        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager is null)
            return;

        EnsureChannel(manager);
        var route = intent.GetStringExtra(RouteExtra);
        var workoutDayId = intent.GetIntExtra(WorkoutDayIdExtra, 0);
        var launchIntent = new Intent(context, typeof(MainActivity));
        launchIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        launchIntent.PutExtra(RouteExtra, route);
        var contentIntent = PendingIntent.GetActivity(
            context,
            workoutDayId,
            launchIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        Notification.Builder builder;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            builder = new Notification.Builder(context, ChannelId);
        else
#pragma warning disable CS0618
            builder = new Notification.Builder(context);
#pragma warning restore CS0618

        var notification = builder
            .SetContentTitle(intent.GetStringExtra(TitleExtra) ?? "GymPlanner")
            .SetContentText(intent.GetStringExtra(BodyExtra) ?? "Запланированная тренировка")
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true)
            .SetContentIntent(contentIntent)
            .Build();

        manager.Notify(workoutDayId, notification);
    }

    private static void EnsureChannel(NotificationManager manager)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var channel = new NotificationChannel(
            ChannelId,
            "Напоминания о тренировках",
            NotificationImportance.Default)
        {
            Description = "Локальные напоминания о запланированных тренировках"
        };
        manager.CreateNotificationChannel(channel);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class WorkoutReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && intent is not null)
            AndroidNotificationSupport.Show(context, intent);
    }
}
