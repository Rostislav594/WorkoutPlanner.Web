using GymPlanner.Mobile.Localization;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Что делать, когда сервер ответил на изменение, сделанное без связи.
/// </summary>
/// <remarks>
/// Принятые изменения ничего особого не требуют: после отправки очереди копия
/// перечитывается целиком. Отказ записывается в журнал и коротко показывается
/// человеку — молча потерять его тренировку нельзя.
/// </remarks>
public sealed class OutboxResultHandler(
    OutboxSync outbox,
    OfflineStatusService status,
    TimeProvider timeProvider) : IOutboxResultHandler
{
    public Task OnSentAsync(OutboxOperation operation, string body) => Task.CompletedTask;

    public async Task OnRejectedAsync(OutboxOperation operation, int statusCode, string body)
    {
        // Удалять уже нечего — например, черновик закрыли на часах. Это не проблема.
        if (statusCode == 404 && operation.Kind is
                OutboxKinds.DeleteExercise or
                OutboxKinds.DiscardFreeWorkoutDraft)
        {
            return;
        }

        var message = body.Contains(ApiErrorCodes.WorkoutAlreadyCompleted, StringComparison.Ordinal)
            ? AppTexts.Get("Offline_Issue_AlreadyCompleted")
            : string.Join(" ", await OfflineRuntime.ReadErrorsAsync(
                new OutboxOutcome(OutboxOutcomeKind.Rejected, statusCode, body)));
        var issue = new SyncIssue(operation.Id, operation.Kind, operation.Label, message, timeProvider.GetUtcNow());
        await outbox.AddIssueAsync(issue);
        status.ShowIssue(issue);
    }
}
