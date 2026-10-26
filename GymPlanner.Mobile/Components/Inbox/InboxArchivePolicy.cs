using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Components.Inbox;

/// <summary>
/// Сообщение старше месяца уходит из окна уведомлений в архив. Это только
/// представление: на сервере сообщение не меняется. Раньше срока в архив
/// попадает то, что пользователь перетащил туда сам (флаг хранит сервер).
/// </summary>
internal static class InboxArchivePolicy
{
    public const string ArchiveRoute = "/notifications/archive";

    public static bool IsArchived(InboxMessageResponse message) =>
        message.IsArchived || message.CreatedAtUtc < DateTime.UtcNow.AddMonths(-1);
}
