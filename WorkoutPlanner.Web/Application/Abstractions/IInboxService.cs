using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IInboxService
{
    Task PublishToUserAsync(
        InboxMessagePublication publication,
        CancellationToken cancellationToken = default);

    Task<InboxPage> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(long messageId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(CancellationToken cancellationToken = default);

    Task<bool> CreateSupportReplyFromTelegramAsync(
        TelegramSupportReply reply,
        CancellationToken cancellationToken = default);
}
