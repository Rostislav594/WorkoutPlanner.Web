using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IProgressApiClient
{
    Task<ApiResult<WorkoutProgressApiResponse>> GetWorkoutProgressAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<IReadOnlyList<string>>> GetWorkoutExercisesAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default);

    Task<ApiResult<ExerciseProgressApiResponse>> GetExerciseProgressAsync(
        int trainingPlanId,
        string exerciseName,
        CancellationToken cancellationToken = default);

    Task<ApiResult> ClearWorkoutProgressAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default);

    Task<ApiResult> ClearExerciseProgressAsync(
        int trainingPlanId,
        string exerciseName,
        CancellationToken cancellationToken = default);

    Task<ApiResult> ClearAllProgressAsync(
        CancellationToken cancellationToken = default);
}
