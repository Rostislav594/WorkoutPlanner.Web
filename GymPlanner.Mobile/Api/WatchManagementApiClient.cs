using GymPlanner.Mobile.Localization;
using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class WatchManagementApiClient(HttpClient client) : IWatchManagementApiClient
{
    public async Task<ApiResult<CreateWatchPairingCodeResponse>> CreatePairingCodeAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsync(
                "api/watch/pairing-codes",
                content: null,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var pairingCode = await response.Content
                .ReadFromJsonAsync<CreateWatchPairingCodeResponse>(cancellationToken);
            return pairingCode is null
                ? ApiResult<CreateWatchPairingCodeResponse>.Failure(
                    ApiErrorMessages.EmptyResponse())
                : ApiResult<CreateWatchPairingCodeResponse>.Success(pairingCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<CreateWatchPairingCodeResponse>.Failure(
                ApiErrorMessages.NetworkUnavailable());
        }
    }

    public async Task<ApiResult<IReadOnlyList<WatchDeviceResponse>>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync("api/watch/devices", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var devices = await response.Content
                .ReadFromJsonAsync<List<WatchDeviceResponse>>(cancellationToken);
            return devices is null
                ? ApiResult<IReadOnlyList<WatchDeviceResponse>>.Failure(
                    ApiErrorMessages.EmptyResponse())
                : ApiResult<IReadOnlyList<WatchDeviceResponse>>.Success(devices);
        }
        catch (HttpRequestException)
        {
            return ApiResult<IReadOnlyList<WatchDeviceResponse>>.Failure(
                ApiErrorMessages.NetworkUnavailable());
        }
    }

    public async Task<ApiResult> RevokeAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return await DeleteAsync(
            $"api/watch/devices/{Uri.EscapeDataString(deviceId)}",
            cancellationToken);
    }

    public Task<ApiResult> RevokeAllAsync(CancellationToken cancellationToken = default) =>
        DeleteAsync("api/watch/devices", cancellationToken);

    public async Task<ApiResult<WatchPairingRequestDetailsResponse>> GetPairingRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        try
        {
            using var response = await client.GetAsync(
                $"api/watch/pair/requests/{Uri.EscapeDataString(requestId)}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var details = await response.Content
                .ReadFromJsonAsync<WatchPairingRequestDetailsResponse>(cancellationToken);
            return details is null
                ? ApiResult<WatchPairingRequestDetailsResponse>.Failure(
                    ApiErrorMessages.EmptyResponse())
                : ApiResult<WatchPairingRequestDetailsResponse>.Success(details);
        }
        catch (HttpRequestException)
        {
            return ApiResult<WatchPairingRequestDetailsResponse>.Failure(
                ApiErrorMessages.NetworkUnavailable());
        }
    }

    public Task<ApiResult> ApprovePairingRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default) =>
        PostDecisionAsync(requestId, "approve", cancellationToken);

    public Task<ApiResult> RejectPairingRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default) =>
        PostDecisionAsync(requestId, "reject", cancellationToken);

    private async Task<ApiResult> PostDecisionAsync(
        string requestId,
        string decision,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        try
        {
            using var response = await client.PostAsync(
                $"api/watch/pair/requests/{Uri.EscapeDataString(requestId)}/{decision}",
                content: null,
                cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Success
                : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure(ApiErrorMessages.NetworkUnavailable());
        }
    }

    private async Task<ApiResult> DeleteAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.DeleteAsync(path, cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Success
                : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure(ApiErrorMessages.NetworkUnavailable());
        }
    }
}
