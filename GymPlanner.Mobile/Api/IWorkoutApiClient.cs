using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IWorkoutApiClient
{
    Task<ApiResult<IReadOnlyList<TrainingPlanApiResponse>>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<TrainingPlanApiResponse>> GetPlanAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<TrainingPlanApiResponse>> CreatePlanAsync(CreateTrainingPlanRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<TrainingPlanApiResponse>> RenamePlanAsync(int id, RenameTrainingPlanRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeletePlanAsync(int id, CancellationToken cancellationToken = default);
}
