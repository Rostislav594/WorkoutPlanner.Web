using System.Globalization;
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
}
