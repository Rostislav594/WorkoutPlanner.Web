using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IHistoryService
{
    Task AddHistoryAsync(
        WorkoutHistory history,
        CancellationToken cancellationToken = default);
    Task<List<WorkoutHistory>> GetHistoryAsync(
        CancellationToken cancellationToken = default);
    Task<WorkoutHistory?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteHistoryAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public interface IWorkoutCompletionService
{
    Task<WorkoutCompletionResult> CompleteTodayAsync(
        CancellationToken cancellationToken = default);
    Task<WorkoutCompletionResult> CompleteTodayAsync(
        int workoutDayId,
        CancellationToken cancellationToken = default);
    Task<FreeWorkoutCompletionResult> CompleteFreeAsync(
        FreeWorkoutCompletion workout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершение свободной тренировки по её серверному черновику.
    ///
    /// Отличие от <see cref="CompleteFreeAsync"/>: здесь состав тренировки берётся
    /// из самого черновика, а не приходит от клиента. Это нужно часам — у них есть
    /// только кэш подходов, без определений упражнений и их статусов.
    /// Вопрос «сохранить шаблоном» остаётся телефону, поэтому шаблон тут не пишется.
    /// </summary>
    Task<WorkoutCompletionResult> CompleteFreeDraftAsync(
        int trainingPlanId,
        CancellationToken cancellationToken = default);
}
