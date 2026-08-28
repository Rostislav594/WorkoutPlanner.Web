using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IInboxService
{
    Task PublishToUserAsync(
        InboxMessagePublication publication,
        CancellationToken cancellationToken = default);

    Task<InboxPage> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<InboxMessageItem?> GetMessageAsync(
        long messageId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(long messageId, CancellationToken cancellationToken = default);

    Task<bool> MarkPublicationReadAsync(long publicationId, CancellationToken cancellationToken = default);

    Task<bool> DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default);

    Task<bool> DeletePublicationAsync(long publicationId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(CancellationToken cancellationToken = default);

    Task<bool> CreateSupportReplyFromTelegramAsync(
        TelegramSupportReply reply,
        CancellationToken cancellationToken = default);
}
