using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IWatchManagementApiClient
{
    Task<ApiResult<CreateWatchPairingCodeResponse>> CreatePairingCodeAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<IReadOnlyList<WatchDeviceResponse>>> GetDevicesAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult> RevokeAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<ApiResult> RevokeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Данные заявки, которую часы просят подтвердить.</summary>
    Task<ApiResult<WatchPairingRequestDetailsResponse>> GetPairingRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task<ApiResult> ApprovePairingRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task<ApiResult> RejectPairingRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default);
}
