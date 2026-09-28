using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Входящие без связи доступны только для чтения: список и открытые письма
/// берутся из копии, а отметки и удаление требуют сервера.
/// </summary>
public sealed class OfflineInboxApiClient(InboxApiClient inner, OfflineRuntime runtime) : IInboxApiClient
{
    public Task<ApiResult<InboxMessagesResponse>> GetMessagesAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(OfflineKeys.InboxMessages, inner.GetMessagesAsync, cancellationToken);

    public Task<ApiResult<InboxMessageResponse>> GetMessageAsync(long messageId, CancellationToken cancellationToken = default) =>
        runtime.ReadAsync(
            token => inner.GetMessageAsync(messageId, token),
            async () => (await runtime.Store.GetAsync<InboxMessagesResponse>(OfflineKeys.InboxMessages, cancellationToken))?
                .Messages
                .FirstOrDefault(x => x.Id == messageId),
            _ => Task.CompletedTask,
            cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(OfflineKeys.InboxUnreadCount, inner.GetUnreadCountAsync, cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> MarkReadAsync(long messageId, CancellationToken cancellationToken = default) =>
        SaveCountAsync(token => inner.MarkReadAsync(messageId, token), cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> MarkPublicationReadAsync(long publicationId, CancellationToken cancellationToken = default) =>
        SaveCountAsync(token => inner.MarkPublicationReadAsync(publicationId, token), cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default) =>
        SaveCountAsync(token => inner.DeleteMessageAsync(messageId, token), cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> DeletePublicationAsync(long publicationId, CancellationToken cancellationToken = default) =>
        SaveCountAsync(token => inner.DeletePublicationAsync(publicationId, token), cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> DeleteAllAsync(CancellationToken cancellationToken = default) =>
        SaveCountAsync(inner.DeleteAllAsync, cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> MarkAllReadAsync(CancellationToken cancellationToken = default) =>
        SaveCountAsync(inner.MarkAllReadAsync, cancellationToken);

    private Task<ApiResult<InboxUnreadCountResponse>> SaveCountAsync(
        Func<CancellationToken, Task<ApiResult<InboxUnreadCountResponse>>> send,
        CancellationToken cancellationToken) =>
        runtime.WriteAsync(
            send,
            count => runtime.Store.SetAsync(OfflineKeys.InboxUnreadCount, count, cancellationToken),
            cancellationToken);
}
