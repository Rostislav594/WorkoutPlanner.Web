using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IOnboardingApiClient
{
    Task<ApiResult<OnboardingStateApiResponse>> GetStateAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<OnboardingStateApiResponse>> SaveProgressAsync(
        SaveOnboardingProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResult<OnboardingStateApiResponse>> CompleteAsync(
        CompleteOnboardingRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResult<OnboardingStateApiResponse>> ResetAsync(
        CancellationToken cancellationToken = default);
}
