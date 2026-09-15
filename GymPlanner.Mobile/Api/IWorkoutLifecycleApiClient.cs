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

    Task<ApiResult<WorkoutDayApiResponse>> MoveCalendarDayAsync(
        int id,
        MoveWorkoutRequest request,
        CancellationToken cancellationToken = default);

    Task<OptionalApiResult<TodayWorkoutApiResponse>> StartTodayWorkoutAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<WorkoutHistoryApiResponse>> CompleteTodayWorkoutAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<CompleteFreeWorkoutResponse>> CompleteFreeWorkoutAsync(
        CompleteFreeWorkoutRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Начинает свободную тренировку на сервере.
    /// </summary>
    /// <remarks>
    /// Пока тренировка живёт только в телефоне, часы её не видят: они
    /// спрашивают активную тренировку у сервера. Поэтому черновик создаётся
    /// сразу при старте, а упражнения в него добавляются обычными эндпоинтами
    /// планов по возвращённому <c>TrainingPlanId</c>.
    /// </remarks>
    Task<ApiResult<FreeWorkoutDraftResponse>> StartFreeWorkoutDraftAsync(
        CancellationToken cancellationToken = default);

    Task<OptionalApiResult<FreeWorkoutDraftResponse>> GetFreeWorkoutDraftAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult> DiscardFreeWorkoutDraftAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult<IReadOnlyList<WorkoutHistoryApiResponse>>> GetHistoryAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResult> DeleteHistoryAsync(
        int id,
        CancellationToken cancellationToken = default);
}
