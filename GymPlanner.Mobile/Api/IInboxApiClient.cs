using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IInboxApiClient
{
    Task<ApiResult<InboxMessagesResponse>> GetMessagesAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<InboxUnreadCountResponse>> GetUnreadCountAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<InboxUnreadCountResponse>> MarkReadAsync(
        long messageId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<InboxUnreadCountResponse>> MarkPublicationReadAsync(
        long publicationId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<InboxUnreadCountResponse>> DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default);
    Task<ApiResult<InboxUnreadCountResponse>> DeletePublicationAsync(long publicationId, CancellationToken cancellationToken = default);
    Task<ApiResult<InboxUnreadCountResponse>> DeleteAllAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<InboxUnreadCountResponse>> MarkAllReadAsync(
        CancellationToken cancellationToken = default);
}
