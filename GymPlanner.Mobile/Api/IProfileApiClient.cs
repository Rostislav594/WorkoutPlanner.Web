using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IProfileApiClient
{
    Task<ApiResult<ProfileResponse>> GetAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<ProfileResponse>> SaveAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<RestTimerSettingsResponse>> SaveRestTimerSettingsAsync(UpdateRestTimerSettingsRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> ChangePasswordAsync(ChangePasswordApiRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> RevokeAccessAsync(CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAccountAsync(CancellationToken cancellationToken = default);
}
