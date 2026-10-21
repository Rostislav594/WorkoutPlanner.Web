using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Components.Inbox;

/// <summary>
/// Сообщение старше месяца уходит из окна уведомлений в архив. Это только
/// представление: на сервере сообщение не меняется.
/// </summary>
internal static class InboxArchivePolicy
{
    public const string ArchiveRoute = "/notifications/archive";

    public static bool IsArchived(InboxMessageResponse message) =>
        message.CreatedAtUtc < DateTime.UtcNow.AddMonths(-1);
}
