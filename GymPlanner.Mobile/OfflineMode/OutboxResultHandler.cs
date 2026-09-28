using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Notifications;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Что делать, когда сервер ответил на изменение, сделанное без связи.
/// </summary>
/// <remarks>
/// Принятые изменения почти ничего не требуют: после отправки очереди копия
/// перечитывается целиком. Отказ записывается в журнал и коротко показывается
/// человеку — молча потерять его тренировку нельзя.
/// </remarks>
public sealed class OutboxResultHandler(
    OutboxSync outbox,
    OfflineStatusService status,
    ILocalWorkoutReminderService reminders,
    TimeProvider timeProvider) : IOutboxResultHandler
{
    public async Task OnSentAsync(OutboxOperation operation, string body)
    {
        // Напоминание о дне, назначенном без связи, висит на временном ID;
        // переносим его на настоящий, иначе отмена дня его не найдёт.
        if (operation.Kind == OutboxKinds.ScheduleWorkout &&
            operation.LocalId is { } localId &&
            reminders.GetScheduled(localId) is { } reminder &&
            LocalIdMap.ReadCreatedId(operation.Kind, body) is { } realId)
        {
            var planId = await outbox.ResolveAsync(reminder.TrainingPlanId);
            var moved = await reminders.ScheduleAsync(reminder with { WorkoutDayId = realId, TrainingPlanId = planId });
            if (moved.Succeeded)
                await reminders.CancelAsync(localId);
        }
    }

    public async Task OnRejectedAsync(OutboxOperation operation, int statusCode, string body)
    {
        // Удалять уже нечего — например, удалили на часах или на другом телефоне.
        if (statusCode == 404 && OutboxKinds.IsDeletion(operation.Kind))
            return;

        var message = body.Contains(ApiErrorCodes.WorkoutAlreadyCompleted, StringComparison.Ordinal)
            ? AppTexts.Get("Offline_Issue_AlreadyCompleted")
            : string.Join(" ", await OfflineRuntime.ReadErrorsAsync(
                new OutboxOutcome(OutboxOutcomeKind.Rejected, statusCode, body)));
        var issue = new SyncIssue(operation.Id, operation.Kind, operation.Label, message, timeProvider.GetUtcNow());
        await outbox.AddIssueAsync(issue);
        status.ShowIssue(issue);
    }
}
