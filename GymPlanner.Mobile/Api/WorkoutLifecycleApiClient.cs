using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GymPlanner.Mobile.Authentication;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public sealed class WorkoutLifecycleApiClient(HttpClient client)
    : IWorkoutLifecycleApiClient
{
    public async Task<ApiResult<IReadOnlyList<WorkoutDayApiResponse>>> GetCalendarAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var fromValue = from.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toValue = to.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            using var response = await client.GetAsync(
                $"api/v1/calendar?from={fromValue}&to={toValue}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var days = await response.Content.ReadFromJsonAsync<
                List<WorkoutDayApiResponse>>(cancellationToken);
            return ApiResult<IReadOnlyList<WorkoutDayApiResponse>>.Success(days ?? []);
        }
        catch (HttpRequestException)
        {
            return ApiResult<IReadOnlyList<WorkoutDayApiResponse>>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult<WorkoutDayApiResponse>> ScheduleWorkoutAsync(
        ScheduleWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                "api/v1/calendar",
                request,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var day = await response.Content.ReadFromJsonAsync<WorkoutDayApiResponse>(
                cancellationToken);
            return day is null
                ? ApiResult<WorkoutDayApiResponse>.Failure(
                    "Сервер вернул пустой календарный день.")
                : ApiResult<WorkoutDayApiResponse>.Success(day);
        }
        catch (HttpRequestException)
        {
            return ApiResult<WorkoutDayApiResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult> DeleteCalendarDayAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.DeleteAsync(
                $"api/v1/calendar/{id}",
                cancellationToken);
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

    public async Task<ApiResult<WorkoutDayApiResponse>> MoveCalendarDayAsync(
        int id,
        MoveWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PutAsJsonAsync(
                $"api/v1/calendar/{id}/date",
                request,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(null, await MobileApiErrorReader.ReadAsync(response, cancellationToken));

            var day = await response.Content.ReadFromJsonAsync<WorkoutDayApiResponse>(cancellationToken);
            return day is null
                ? ApiResult<WorkoutDayApiResponse>.Failure("Сервер вернул пустую тренировку.")
                : ApiResult<WorkoutDayApiResponse>.Success(day);
        }
        catch (HttpRequestException)
        {
            return ApiResult<WorkoutDayApiResponse>.Failure("Нет соединения с сервером.");
        }
    }

    public async Task<OptionalApiResult<TodayWorkoutApiResponse>> StartTodayWorkoutAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsync(
                "api/v1/workouts/today/start",
                content: null,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return OptionalApiResult<TodayWorkoutApiResponse>.Empty();
            if (!response.IsSuccessStatusCode)
            {
                return OptionalApiResult<TodayWorkoutApiResponse>.Failure(
                    [.. await MobileApiErrorReader.ReadAsync(
                        response,
                        cancellationToken)]);
            }

            var workout = await response.Content.ReadFromJsonAsync<
                TodayWorkoutApiResponse>(cancellationToken);
            return workout is null
                ? OptionalApiResult<TodayWorkoutApiResponse>.Failure(
                    "Сервер вернул пустую тренировку.")
                : OptionalApiResult<TodayWorkoutApiResponse>.Success(workout);
        }
        catch (HttpRequestException)
        {
            return OptionalApiResult<TodayWorkoutApiResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult<WorkoutHistoryApiResponse>> CompleteTodayWorkoutAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsync(
                "api/v1/workouts/today/complete",
                content: null,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var history = await response.Content.ReadFromJsonAsync<
                WorkoutHistoryApiResponse>(cancellationToken);
            return history is null
                ? ApiResult<WorkoutHistoryApiResponse>.Failure(
                    "Сервер вернул пустую запись истории.")
                : ApiResult<WorkoutHistoryApiResponse>.Success(history);
        }
        catch (HttpRequestException)
        {
            return ApiResult<WorkoutHistoryApiResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult<CompleteFreeWorkoutResponse>> CompleteFreeWorkoutAsync(
        CompleteFreeWorkoutRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                "api/v1/workouts/free/complete",
                request,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var result = await response.Content.ReadFromJsonAsync<
                CompleteFreeWorkoutResponse>(cancellationToken);
            return result is null
                ? ApiResult<CompleteFreeWorkoutResponse>.Failure(
                    "Сервер вернул пустой результат свободной тренировки.")
                : ApiResult<CompleteFreeWorkoutResponse>.Success(result);
        }
        catch (HttpRequestException)
        {
            return ApiResult<CompleteFreeWorkoutResponse>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(
                "api/v1/history",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    await MobileApiErrorReader.ReadAsync(response, cancellationToken));
            }

            var history = await response.Content.ReadFromJsonAsync<
                List<WorkoutHistoryApiResponse>>(cancellationToken);
            return ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>.Success(
                history ?? []);
        }
        catch (HttpRequestException)
        {
            return ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>.Failure(
                "Нет соединения с сервером.");
        }
    }

    public async Task<ApiResult> DeleteHistoryAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.DeleteAsync(
                $"api/v1/history/{id}",
                cancellationToken);
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
