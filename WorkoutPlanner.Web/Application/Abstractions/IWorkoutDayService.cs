using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IWorkoutDayService
{
    Task<List<WorkoutDay>> GetDaysAsync(
        CancellationToken cancellationToken = default);
    Task SaveDayAsync(
        DateTime date,
        int trainingPlanId,
        CancellationToken cancellationToken = default);
    Task RemoveTodayWorkoutAsync(
        CancellationToken cancellationToken = default);
    Task DeleteDayAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task CompleteTodayWorkoutAsync(
        CancellationToken cancellationToken = default);
}

public interface ITodayWorkoutService
{
    Task<TrainingPlan?> GetTodayWorkoutAsync(
        CancellationToken cancellationToken = default);
}
