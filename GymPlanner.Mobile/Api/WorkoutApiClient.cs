using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class WorkoutApiClient(HttpClient client) : IWorkoutApiClient
{
    public async Task<ApiResult<IReadOnlyList<TrainingPlanApiResponse>>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync("api/v1/training-plans", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            var plans = await response.Content.ReadFromJsonAsync<List<TrainingPlanApiResponse>>(cancellationToken);
            return ApiResult<IReadOnlyList<TrainingPlanApiResponse>>.Success(plans ?? []);
        }
        catch (HttpRequestException) { return ApiResult<IReadOnlyList<TrainingPlanApiResponse>>.Failure("Нет соединения с сервером."); }
    }

    public Task<ApiResult<TrainingPlanApiResponse>> GetPlanAsync(int id, CancellationToken cancellationToken = default) =>
        GetPlanResponseAsync(HttpMethod.Get, $"api/v1/training-plans/{id}", null, cancellationToken);

    public Task<ApiResult<TrainingPlanApiResponse>> CreatePlanAsync(CreateTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        GetPlanResponseAsync(HttpMethod.Post, "api/v1/training-plans", request, cancellationToken);

    public Task<ApiResult<TrainingPlanApiResponse>> RenamePlanAsync(int id, RenameTrainingPlanRequest request, CancellationToken cancellationToken = default) =>
        GetPlanResponseAsync(HttpMethod.Put, $"api/v1/training-plans/{id}", request, cancellationToken);

    public async Task<ApiResult> DeletePlanAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.DeleteAsync($"api/v1/training-plans/{id}", cancellationToken);
            return response.IsSuccessStatusCode ? ApiResult.Success : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException) { return ApiResult.Failure("Нет соединения с сервером."); }
    }

    public async Task<ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>> GetExerciseDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync("api/v1/exercise-definitions", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            var definitions = await response.Content.ReadFromJsonAsync<List<ExerciseDefinitionApiResponse>>(cancellationToken);
            return ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>.Success(definitions ?? []);
        }
        catch (HttpRequestException) { return ApiResult<IReadOnlyList<ExerciseDefinitionApiResponse>>.Failure("Нет соединения с сервером."); }
    }

    public Task<ApiResult<ExerciseApiResponse>> CreateExerciseAsync(int planId, SaveExerciseRequest request, CancellationToken cancellationToken = default) =>
        GetExerciseResponseAsync(HttpMethod.Post, $"api/v1/training-plans/{planId}/exercises", request, cancellationToken);

    public Task<ApiResult<ExerciseApiResponse>> UpdateExerciseAsync(int id, SaveExerciseRequest request, CancellationToken cancellationToken = default) =>
        GetExerciseResponseAsync(HttpMethod.Put, $"api/v1/exercises/{id}", request, cancellationToken);

    public async Task<ApiResult> DeleteExerciseAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.DeleteAsync($"api/v1/exercises/{id}", cancellationToken);
            return response.IsSuccessStatusCode ? ApiResult.Success : new(false, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
        }
        catch (HttpRequestException) { return ApiResult.Failure("Нет соединения с сервером."); }
    }

    private async Task<ApiResult<TrainingPlanApiResponse>> GetPlanResponseAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            var plan = await response.Content.ReadFromJsonAsync<TrainingPlanApiResponse>(cancellationToken);
            return plan is null ? ApiResult<TrainingPlanApiResponse>.Failure("Сервер вернул пустой план.") : ApiResult<TrainingPlanApiResponse>.Success(plan);
        }
        catch (HttpRequestException) { return ApiResult<TrainingPlanApiResponse>.Failure("Нет соединения с сервером."); }
    }

    private async Task<ApiResult<ExerciseApiResponse>> GetExerciseResponseAsync(
        HttpMethod method, string path, SaveExerciseRequest body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            var exercise = await response.Content.ReadFromJsonAsync<ExerciseApiResponse>(cancellationToken);
            return exercise is null ? ApiResult<ExerciseApiResponse>.Failure("Сервер вернул пустое упражнение.") : ApiResult<ExerciseApiResponse>.Success(exercise);
        }
        catch (HttpRequestException) { return ApiResult<ExerciseApiResponse>.Failure("Нет соединения с сервером."); }
    }
}
