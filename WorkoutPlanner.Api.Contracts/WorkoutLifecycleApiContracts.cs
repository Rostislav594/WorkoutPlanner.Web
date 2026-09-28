namespace WorkoutPlanner.Api.Contracts;

public sealed record WorkoutDayApiResponse(int Id, DateTime Date, int TrainingPlanId, bool IsCompleted);
public sealed record ScheduleWorkoutRequest(DateTime Date, int TrainingPlanId);
public sealed record MoveWorkoutRequest(DateTime Date);
public sealed record TodayWorkoutApiResponse(WorkoutDayApiResponse Day, TrainingPlanApiResponse TrainingPlan);
public sealed record WorkoutHistoryApiResponse(int Id, string WorkoutName, DateTime Date, string Summary, bool SnapshotAvailable, IReadOnlyList<WorkoutHistoryExerciseApiResponse> Exercises);
public sealed record WorkoutHistoryExerciseApiResponse(
    string Name,
    string Status,
    IReadOnlyList<ExerciseSetApiResponse> Sets,
    int? SupersetGroupId = null);
/// <param name="CompletedAt">
/// Когда тренировка закончилась по часам телефона. Нужно офлайн-очереди: запрос
/// может дойти до сервера на следующий день, а в истории должен остаться день
/// тренировки. Старые сборки поле не шлют — тогда берётся время сервера.
/// </param>
public sealed record CompleteFreeWorkoutRequest(
    bool SaveAsTemplate,
    string? TemplateName,
    IReadOnlyList<SaveExerciseRequest> Exercises,
    DateTime? CompletedAt = null);

/// <summary>
/// Завершение запланированной тренировки одним запросом: итоги подходов и сама
/// отметка о завершении сохраняются вместе или не сохраняются вовсе.
/// </summary>
/// <param name="CompletedAt">Время окончания по часам телефона (местное).</param>
public sealed record CompleteScheduledWorkoutRequest(
    DateTime CompletedAt,
    IReadOnlyList<CompletedExerciseRequest> Exercises);

public sealed record CompletedExerciseRequest(
    int ExerciseId,
    string Status,
    IReadOnlyList<SaveExerciseSetRequest> Sets);
public sealed record CompleteFreeWorkoutResponse(
    WorkoutHistoryApiResponse History,
    int? TrainingPlanId);

/// <summary>
/// Черновик свободной тренировки, которая идёт прямо сейчас.
/// </summary>
/// <param name="TrainingPlanId">
/// Идентификатор плана: по нему телефон правит упражнения обычными эндпоинтами
/// планов, отдельного набора для свободной тренировки не нужно.
/// </param>
/// <param name="WorkoutId">
/// Идентификатор активной тренировки — то же число, что увидят часы.
/// </param>
/// <param name="AlreadyStarted">
/// Черновик уже существовал: повторный старт возвращает его, а не создаёт второй.
/// </param>
public sealed record FreeWorkoutDraftResponse(
    int TrainingPlanId,
    int WorkoutId,
    string WorkoutName,
    bool AlreadyStarted);
