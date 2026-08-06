using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Api;

public interface IWorkoutLifecycleApiClient
{
    Task<ApiResult<IReadOnlyList<WorkoutDayApiResponse>>> GetCalendarAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<ApiResult<WorkoutDayApiResponse>> ScheduleWorkoutAsync(
        ScheduleWorkoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResult> DeleteCalendarDayAsync(
        int id,
        CancellationToken cancellationToken = default);
}
