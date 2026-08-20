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

    Task<ApiResult<InboxUnreadCountResponse>> MarkAllReadAsync(
        CancellationToken cancellationToken = default);
}
