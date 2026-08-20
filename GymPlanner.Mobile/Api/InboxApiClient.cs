using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class InboxApiClient(HttpClient client) : IInboxApiClient
{
    public Task<ApiResult<InboxMessagesResponse>> GetMessagesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<InboxMessagesResponse>("api/v1/inbox/messages", cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> GetUnreadCountAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<InboxUnreadCountResponse>("api/v1/inbox/unread-count", cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> MarkReadAsync(
        long messageId,
        CancellationToken cancellationToken = default) =>
        PostAsync($"api/v1/inbox/messages/{messageId}/read", cancellationToken);

    public Task<ApiResult<InboxUnreadCountResponse>> MarkAllReadAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/inbox/messages/read-all", cancellationToken);

    private async Task<ApiResult<T>> GetAsync<T>(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            return await ReadAsync<T>(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Failure("Нет соединения с сервером.");
        }
    }

    private async Task<ApiResult<InboxUnreadCountResponse>> PostAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsync(url, null, cancellationToken);
            return await ReadAsync<InboxUnreadCountResponse>(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ApiResult<InboxUnreadCountResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }

    private static async Task<ApiResult<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            return ApiResult<T>.Failure(
                (await MobileApiErrorReader.ReadAsync(response, cancellationToken)).ToArray());
        var content = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return content is null
            ? ApiResult<T>.Failure("Сервер вернул неполный ответ.")
            : ApiResult<T>.Success(content);
    }
}
