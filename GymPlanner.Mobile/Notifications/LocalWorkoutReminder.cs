using System.Text.Json;

namespace GymPlanner.Mobile.Notifications;

public sealed record LocalWorkoutReminder(
    int WorkoutDayId,
    int TrainingPlanId,
    string WorkoutName,
    DateTimeOffset NotifyAt)
{
    public string Route => $"/workouts/{TrainingPlanId}";
}

public sealed record NotificationOperationResult(
    bool Succeeded,
    bool PermissionDenied,
    IReadOnlyList<string> Errors)
{
    public static NotificationOperationResult Success { get; } =
        new(true, false, []);

    public static NotificationOperationResult Denied(string error) =>
        new(false, true, [error]);

    public static NotificationOperationResult Failure(params string[] errors) =>
        new(false, false, errors);
}

public interface ILocalWorkoutReminderService
{
    Task<NotificationOperationResult> RequestPermissionAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationOperationResult> ScheduleAsync(
        LocalWorkoutReminder reminder,
        CancellationToken cancellationToken = default);

    Task<NotificationOperationResult> CancelAsync(
        int workoutDayId,
        CancellationToken cancellationToken = default);

    Task<NotificationOperationResult> CancelAllAsync(
        CancellationToken cancellationToken = default);

    LocalWorkoutReminder? GetScheduled(int workoutDayId);
}

public interface ILocalNotificationPlatform
{
    Task<NotificationOperationResult> RequestPermissionAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationOperationResult> ScheduleAsync(
        LocalWorkoutReminder reminder,
        CancellationToken cancellationToken = default);

    Task<NotificationOperationResult> CancelAsync(
        int workoutDayId,
        CancellationToken cancellationToken = default);
}

public sealed class LocalWorkoutReminderService(
    ILocalNotificationPlatform platform) : ILocalWorkoutReminderService
{
    private const string PreferencePrefix = "workout-reminder:";
    private const string ReminderIndexKey = "workout-reminder:index";

    public Task<NotificationOperationResult> RequestPermissionAsync(
        CancellationToken cancellationToken = default) =>
        platform.RequestPermissionAsync(cancellationToken);

    public async Task<NotificationOperationResult> ScheduleAsync(
        LocalWorkoutReminder reminder,
        CancellationToken cancellationToken = default)
    {
        if (reminder.WorkoutDayId <= 0 ||
            reminder.TrainingPlanId <= 0 ||
            string.IsNullOrWhiteSpace(reminder.WorkoutName) ||
            reminder.NotifyAt <= DateTimeOffset.Now)
        {
            return NotificationOperationResult.Failure(
                "Время напоминания должно быть в будущем.");
        }

        var result = await platform.ScheduleAsync(reminder, cancellationToken);
        if (result.Succeeded)
        {
            Preferences.Default.Set(
                GetPreferenceKey(reminder.WorkoutDayId),
                JsonSerializer.Serialize(reminder));
            var ids = GetReminderIds();
            if (ids.Add(reminder.WorkoutDayId))
                SaveReminderIds(ids);
        }

        return result;
    }

    public async Task<NotificationOperationResult> CancelAsync(
        int workoutDayId,
        CancellationToken cancellationToken = default)
    {
        var result = await platform.CancelAsync(workoutDayId, cancellationToken);
        if (result.Succeeded)
        {
            Preferences.Default.Remove(GetPreferenceKey(workoutDayId));
            var ids = GetReminderIds();
            if (ids.Remove(workoutDayId))
                SaveReminderIds(ids);
        }
        return result;
    }

    public async Task<NotificationOperationResult> CancelAllAsync(
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        foreach (var workoutDayId in GetReminderIds())
        {
            var result = await platform.CancelAsync(workoutDayId, cancellationToken);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
                continue;
            }

            Preferences.Default.Remove(GetPreferenceKey(workoutDayId));
        }

        if (errors.Count > 0)
            return NotificationOperationResult.Failure([.. errors.Distinct()]);

        Preferences.Default.Remove(ReminderIndexKey);
        return NotificationOperationResult.Success;
    }

    public LocalWorkoutReminder? GetScheduled(int workoutDayId)
    {
        var value = Preferences.Default.Get(
            GetPreferenceKey(workoutDayId),
            default(string));
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var reminder = JsonSerializer.Deserialize<LocalWorkoutReminder>(value);
            if (reminder is not null && reminder.NotifyAt > DateTimeOffset.Now)
                return reminder;
        }
        catch (JsonException)
        {
        }

        Preferences.Default.Remove(GetPreferenceKey(workoutDayId));
        var ids = GetReminderIds();
        if (ids.Remove(workoutDayId))
            SaveReminderIds(ids);
        return null;
    }

    private static string GetPreferenceKey(int workoutDayId) =>
        $"{PreferencePrefix}{workoutDayId}";

    private static HashSet<int> GetReminderIds()
    {
        var value = Preferences.Default.Get(ReminderIndexKey, default(string));
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            return JsonSerializer.Deserialize<HashSet<int>>(value) ?? [];
        }
        catch (JsonException)
        {
            Preferences.Default.Remove(ReminderIndexKey);
            return [];
        }
    }

    private static void SaveReminderIds(HashSet<int> ids)
    {
        if (ids.Count == 0)
            Preferences.Default.Remove(ReminderIndexKey);
        else
            Preferences.Default.Set(ReminderIndexKey, JsonSerializer.Serialize(ids));
    }
}
