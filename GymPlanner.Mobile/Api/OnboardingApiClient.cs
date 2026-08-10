using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class OnboardingApiClient(HttpClient client) : IOnboardingApiClient
{
    public Task<ApiResult<OnboardingStateApiResponse>> GetStateAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, "api/v1/onboarding", null, cancellationToken);

    public Task<ApiResult<OnboardingStateApiResponse>> SaveProgressAsync(
        SaveOnboardingProgressRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Put,
            "api/v1/onboarding/progress",
            request,
            cancellationToken);

    public Task<ApiResult<OnboardingStateApiResponse>> CompleteAsync(
        CompleteOnboardingRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            "api/v1/onboarding/complete",
            request,
            cancellationToken);

    public Task<ApiResult<OnboardingStateApiResponse>> ResetAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, "api/v1/onboarding", null, cancellationToken);

    private async Task<ApiResult<OnboardingStateApiResponse>> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var state = await response.Content.ReadFromJsonAsync<
                OnboardingStateApiResponse>(cancellationToken);
            return state is null
                ? ApiResult<OnboardingStateApiResponse>.Failure(
                    "Сервер вернул пустое состояние обучения.")
                : ApiResult<OnboardingStateApiResponse>.Success(state);
        }
        catch (HttpRequestException)
        {
            return ApiResult<OnboardingStateApiResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }
}
