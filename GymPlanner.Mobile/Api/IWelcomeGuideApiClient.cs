using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IWelcomeGuideApiClient
{
    Task<ApiResult<WelcomeGuideStateApiResponse>> GetStateAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<WelcomeGuideStateApiResponse>> CompleteAsync(CancellationToken cancellationToken = default);
}
