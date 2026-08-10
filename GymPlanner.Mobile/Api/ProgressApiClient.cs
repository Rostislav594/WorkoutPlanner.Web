using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class ProgressApiClient(HttpClient client) : IProgressApiClient
{
    public Task<ApiResult<WorkoutProgressApiResponse>> GetWorkoutProgressAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default) =>
        GetAsync<WorkoutProgressApiResponse>(
            $"api/v1/progress/workouts/{trainingPlanId}",
            "Сервер вернул пустой прогресс тренировки.",
            cancellationToken);

    public async Task<ApiResult<IReadOnlyList<string>>> GetWorkoutExercisesAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<List<string>>(
            $"api/v1/progress/workouts/{trainingPlanId}/exercises",
            "Сервер вернул пустой список упражнений.",
            cancellationToken);
        return result.Succeeded
            ? ApiResult<IReadOnlyList<string>>.Success(result.Value!)
            : ApiResult<IReadOnlyList<string>>.Failure([.. result.Errors]);
    }

    public Task<ApiResult<ExerciseProgressApiResponse>> GetExerciseProgressAsync(
        int trainingPlanId,
        string exerciseName,
        CancellationToken cancellationToken = default) =>
        GetAsync<ExerciseProgressApiResponse>(
            $"api/v1/progress/workouts/{trainingPlanId}/exercises/chart?exerciseName={Uri.EscapeDataString(exerciseName)}",
            "Сервер вернул пустой прогресс упражнения.",
            cancellationToken);

    public Task<ApiResult> ClearWorkoutProgressAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(
            $"api/v1/progress/workouts/{trainingPlanId}",
            cancellationToken);

    public Task<ApiResult> ClearExerciseProgressAsync(
        int trainingPlanId,
        string exerciseName,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(
            $"api/v1/progress/workouts/{trainingPlanId}/exercises?exerciseName={Uri.EscapeDataString(exerciseName)}",
            cancellationToken);

    public Task<ApiResult> ClearAllProgressAsync(
        CancellationToken cancellationToken = default) =>
        DeleteAsync("api/v1/progress", cancellationToken);

    private async Task<ApiResult<T>> GetAsync<T>(
        string path,
        string emptyResponseError,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    default,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
            return value is null
                ? ApiResult<T>.Failure(emptyResponseError)
                : ApiResult<T>.Success(value);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Failure("Нет соединения с сервером.");
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
                : new(
                    false,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failure("Нет соединения с сервером.");
        }
    }
}
