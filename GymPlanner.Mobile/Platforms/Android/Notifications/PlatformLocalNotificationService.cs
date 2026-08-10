using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using System.Text.Json;

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
        if (!AndroidNotificationSupport.TryScheduleAlarm(context, reminder))
        {
            return Task.FromResult(NotificationOperationResult.Failure(
                "Системная служба напоминаний недоступна."));
        }

        AndroidReminderStore.Save(context, reminder);
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
        AndroidReminderStore.Remove(context, workoutDayId);
        return Task.FromResult(NotificationOperationResult.Success);
    }

    public Task<NotificationOperationResult> CancelAllAsync(
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

        var notificationManager =
            context.GetSystemService(Context.NotificationService) as NotificationManager;
        foreach (var workoutDayId in AndroidReminderStore.GetWorkoutDayIds(context))
        {
            var pendingIntent = AndroidNotificationSupport.CreateAlarmIntent(
                context,
                workoutDayId);
            alarmManager.Cancel(pendingIntent);
            pendingIntent.Cancel();
            notificationManager?.Cancel(workoutDayId);
        }

        AndroidReminderStore.Clear(context);
        return Task.FromResult(NotificationOperationResult.Success);
    }
}

internal static class AndroidNotificationSupport
{
    public const string ChannelId = "workout-reminders";
    public const string RouteExtra = "gymplanner.notification.route";
    private const string TitleExtra = "gymplanner.notification.title";
    private const string BodyExtra = "gymplanner.notification.body";
    internal const string WorkoutDayIdExtra = "gymplanner.notification.workout-day-id";

    public static bool TryScheduleAlarm(
        Context context,
        LocalWorkoutReminder reminder)
    {
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (alarmManager is null)
            return false;

        var pendingIntent = CreateAlarmIntent(context, reminder);
        alarmManager.SetAndAllowWhileIdle(
            AlarmType.RtcWakeup,
            reminder.NotifyAt.ToUnixTimeMilliseconds(),
            pendingIntent);
        return true;
    }

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
        if (context is null || intent is null)
            return;

        var workoutDayId = intent.GetIntExtra(
            AndroidNotificationSupport.WorkoutDayIdExtra,
            0);
        if (workoutDayId > 0)
            AndroidReminderStore.Remove(context, workoutDayId);
        AndroidNotificationSupport.Show(context, intent);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[]
{
    "android.intent.action.BOOT_COMPLETED",
    "android.intent.action.MY_PACKAGE_REPLACED"
})]
public sealed class WorkoutReminderRestoreReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
            return;

        foreach (var reminder in AndroidReminderStore.LoadFuture(context))
            AndroidNotificationSupport.TryScheduleAlarm(context, reminder);
    }
}

internal static class AndroidReminderStore
{
    private const string PreferenceName = "gymplanner.workout-reminders";
    private const string VersionKey = "schema-version";
    private const string IndexKey = "reminder-index";
    private const string ReminderPrefix = "reminder:";
    private const int CurrentVersion = 1;
    private static readonly object Sync = new();

    public static void Save(Context context, LocalWorkoutReminder reminder)
    {
        lock (Sync)
        {
            var preferences = GetPreferences(context);
            var ids = GetIds(preferences);
            ids.Add(reminder.WorkoutDayId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            preferences.Edit()!
                .PutInt(VersionKey, CurrentVersion)!
                .PutString(GetReminderKey(reminder.WorkoutDayId), JsonSerializer.Serialize(reminder))!
                .PutStringSet(IndexKey, ids)!
                .Commit();
        }
    }

    public static void Remove(Context context, int workoutDayId)
        => Remove(
            context,
            workoutDayId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void Remove(Context context, string workoutDayId)
    {
        lock (Sync)
        {
            var preferences = GetPreferences(context);
            var ids = GetIds(preferences);
            ids.Remove(workoutDayId);
            var editor = preferences.Edit()!
                .Remove($"{ReminderPrefix}{workoutDayId}")!;
            if (ids.Count == 0)
                editor.Remove(IndexKey)!.Remove(VersionKey)!.Commit();
            else
                editor.PutStringSet(IndexKey, ids)!.Commit();
        }
    }

    public static IReadOnlyList<LocalWorkoutReminder> LoadFuture(Context context)
    {
        lock (Sync)
        {
            var preferences = GetPreferences(context);
            if (preferences.GetInt(VersionKey, CurrentVersion) != CurrentVersion)
            {
                preferences.Edit()!.Clear()!.Commit();
                return [];
            }

            var reminders = new List<LocalWorkoutReminder>();
            var invalidIds = new List<string>();
            foreach (var idValue in GetIds(preferences))
            {
                if (!int.TryParse(
                        idValue,
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var workoutDayId))
                {
                    invalidIds.Add(idValue);
                    continue;
                }

                try
                {
                    var json = preferences.GetString(
                        GetReminderKey(workoutDayId),
                        null);
                    var reminder = string.IsNullOrWhiteSpace(json)
                        ? null
                        : JsonSerializer.Deserialize<LocalWorkoutReminder>(json);
                    if (reminder is not null &&
                        reminder.WorkoutDayId == workoutDayId &&
                        reminder.NotifyAt > DateTimeOffset.Now)
                    {
                        reminders.Add(reminder);
                    }
                    else
                    {
                        invalidIds.Add(idValue);
                    }
                }
                catch (JsonException)
                {
                    invalidIds.Add(idValue);
                }
            }

            foreach (var workoutDayId in invalidIds)
                Remove(context, workoutDayId);
            return reminders;
        }
    }

    public static IReadOnlyList<int> GetWorkoutDayIds(Context context)
    {
        lock (Sync)
        {
            return GetIds(GetPreferences(context))
                .Select(value => int.TryParse(
                    value,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var workoutDayId)
                    ? workoutDayId
                    : 0)
                .Where(workoutDayId => workoutDayId > 0)
                .Distinct()
                .ToArray();
        }
    }

    public static void Clear(Context context)
    {
        lock (Sync)
            GetPreferences(context).Edit()!.Clear()!.Commit();
    }

    private static ISharedPreferences GetPreferences(Context context) =>
        context.GetSharedPreferences(PreferenceName, FileCreationMode.Private)!;

    private static HashSet<string> GetIds(ISharedPreferences preferences) =>
        new(
            preferences.GetStringSet(IndexKey, null) ?? [],
            StringComparer.Ordinal);

    private static string GetReminderKey(int workoutDayId) =>
        $"{ReminderPrefix}{workoutDayId}";
}
