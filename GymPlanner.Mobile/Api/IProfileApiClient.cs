using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IProfileApiClient
{
    Task<ApiResult<List<AccountSessionResponse>>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult> RevokeSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResult<ProfileResponse>> GetAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<ProfileResponse>> SaveAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<RestTimerSettingsResponse>> SaveRestTimerSettingsAsync(UpdateRestTimerSettingsRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<LanguageSettingsResponse>> SaveLanguageAsync(UpdateLanguageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> ChangePasswordAsync(ChangePasswordApiRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> RevokeAccessAsync(CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAccountAsync(CancellationToken cancellationToken = default);
}
