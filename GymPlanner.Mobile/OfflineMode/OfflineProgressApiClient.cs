using GymPlanner.Mobile.Api;
using GymPlanner.Mobile.Offline;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Прогресс считает сервер, поэтому без связи показываются последние
/// полученные графики; очистка прогресса — только онлайн.
/// </summary>
public sealed class OfflineProgressApiClient(ProgressApiClient inner, OfflineRuntime runtime) : IProgressApiClient
{
    public Task<ApiResult<ProgressOverviewApiResponse>> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(OfflineKeys.ProgressOverview, inner.GetOverviewAsync, cancellationToken);

    public Task<ApiResult<WorkoutProgressApiResponse>> GetWorkoutProgressAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(
            OfflineKeys.WorkoutProgress(trainingPlanId),
            token => inner.GetWorkoutProgressAsync(trainingPlanId, token),
            cancellationToken);

    public Task<ApiResult<IReadOnlyList<string>>> GetWorkoutExercisesAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default) =>
        runtime.ReadAsync<IReadOnlyList<string>>(
            token => inner.GetWorkoutExercisesAsync(trainingPlanId, token),
            async () => await runtime.Store.GetAsync<List<string>>(
                OfflineKeys.WorkoutExercises(trainingPlanId),
                cancellationToken),
            names => runtime.Store.SetAsync(
                OfflineKeys.WorkoutExercises(trainingPlanId),
                names.ToList(),
                cancellationToken),
            cancellationToken);

    public Task<ApiResult<ExerciseProgressApiResponse>> GetExerciseProgressAsync(
        int trainingPlanId,
        string exerciseName,
        CancellationToken cancellationToken = default) =>
        runtime.ReadDocumentAsync(
            OfflineKeys.ExerciseProgress(trainingPlanId, exerciseName),
            token => inner.GetExerciseProgressAsync(trainingPlanId, exerciseName, token),
            cancellationToken);

    public Task<ApiResult> ClearWorkoutProgressAsync(int trainingPlanId, CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.ClearWorkoutProgressAsync(trainingPlanId, token),
            ForgetProgressAsync,
            cancellationToken);

    public Task<ApiResult> ClearExerciseProgressAsync(
        int trainingPlanId,
        string exerciseName,
        CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(
            token => inner.ClearExerciseProgressAsync(trainingPlanId, exerciseName, token),
            ForgetProgressAsync,
            cancellationToken);

    public Task<ApiResult> ClearAllProgressAsync(CancellationToken cancellationToken = default) =>
        runtime.WriteAsync(inner.ClearAllProgressAsync, ForgetProgressAsync, cancellationToken);

    // После очистки прежние графики неверны; свежие придут со следующим чтением.
    private Task ForgetProgressAsync() => runtime.Store.RemoveByPrefixAsync(OfflineKeys.ProgressPrefix);
}
