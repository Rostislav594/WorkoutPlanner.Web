using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class ProfileApiClient(HttpClient client) : IProfileApiClient
{
    public async Task<ApiResult<ProfileResponse>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync("api/v1/profile", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(cancellationToken);
            return profile is null
                ? ApiResult<ProfileResponse>.Failure("Сервер вернул пустой профиль.")
                : ApiResult<ProfileResponse>.Success(profile);
        }
        catch (HttpRequestException)
        {
            return ApiResult<ProfileResponse>.Failure("Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult<ProfileResponse>> SaveAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PutAsJsonAsync(
                "api/v1/profile", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(cancellationToken);
            return profile is null
                ? ApiResult<ProfileResponse>.Failure("Сервер вернул пустой профиль.")
                : ApiResult<ProfileResponse>.Success(profile);
        }
        catch (HttpRequestException)
        {
            return ApiResult<ProfileResponse>.Failure("Нет соединения с сервером.");
        }
    }

    public Task<ApiResult> ChangePasswordAsync(
        ChangePasswordApiRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/account/change-password", request, cancellationToken);

    public Task<ApiResult> RevokeAccessAsync(CancellationToken cancellationToken = default) =>
        PostEmptyAsync("api/v1/account/revoke-access", cancellationToken);

    public async Task<ApiResult> DeleteAccountAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.DeleteAsync("api/v1/account", cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Success
                : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure("Нет соединения с сервером.");
        }
    }

    private async Task<ApiResult> PostAsync<T>(
        string path,
        T? content,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(path, content, cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Success
                : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure("Нет соединения с сервером.");
        }
    }

    private async Task<ApiResult> PostEmptyAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsync(path, content: null, cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Success
                : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure("Нет соединения с сервером.");
        }
    }
}
