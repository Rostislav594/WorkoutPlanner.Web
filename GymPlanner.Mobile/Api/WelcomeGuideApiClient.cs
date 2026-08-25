using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class WelcomeGuideApiClient(HttpClient client) : IWelcomeGuideApiClient
{
    public Task<ApiResult<WelcomeGuideStateApiResponse>> GetStateAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, cancellationToken);

    public Task<ApiResult<WelcomeGuideStateApiResponse>> CompleteAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, cancellationToken);

    private async Task<ApiResult<WelcomeGuideStateApiResponse>> SendAsync(HttpMethod method, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, "api/v1/welcome-guide");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var state = await response.Content.ReadFromJsonAsync<WelcomeGuideStateApiResponse>(cancellationToken);
            return state is null
                ? ApiResult<WelcomeGuideStateApiResponse>.Failure("Сервер вернул пустое состояние приветствия.")
                : ApiResult<WelcomeGuideStateApiResponse>.Success(state);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            return ApiResult<WelcomeGuideStateApiResponse>.Failure("Нет соединения с сервером.");
        }
    }
}
