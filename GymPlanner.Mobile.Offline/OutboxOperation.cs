namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Изменение, сделанное на телефоне и ещё не подтверждённое сервером.
/// </summary>
/// <param name="Id">Ключ повтора: тот же запрос, отправленный дважды, сервер выполнит один раз.</param>
/// <param name="Kind">Тип операции из <see cref="OutboxKinds"/>.</param>
/// <param name="Label">Что показать человеку — например, название тренировки.</param>
/// <param name="LocalId">Временный ID того, что операция создала на телефоне.</param>
public sealed record OutboxOperation(
    Guid Id,
    string Kind,
    string Method,
    string Path,
    string? Body,
    DateTimeOffset CreatedAtUtc,
    string? Label = null,
    int? LocalId = null);

/// <summary>Типы операций офлайн-очереди.</summary>
public static class OutboxKinds
{
    public const string CompleteScheduledWorkout = "workout.complete-day";
    public const string CompleteFreeWorkout = "workout.complete-free";
    public const string DiscardFreeWorkoutDraft = "workout.discard-free-draft";
    public const string UpdateExercise = "exercise.update";
    public const string DeleteExercise = "exercise.delete";
    public const string ReorderSuperset = "superset.reorder";
    public const string CreatePlan = "plan.create";
    public const string RenamePlan = "plan.rename";
    public const string DeletePlan = "plan.delete";
    public const string CreateExercise = "exercise.create";
    public const string ScheduleWorkout = "calendar.schedule";
    public const string MoveWorkout = "calendar.move";
    public const string DeleteWorkoutDay = "calendar.delete";
    public const string DeleteHistory = "history.delete";
    public const string SaveProfile = "profile.save";
    public const string SaveRestTimers = "profile.rest-timers";
    public const string SaveLanguage = "profile.language";

    /// <summary>Удаление: «уже удалено» на сервере — тоже нужный исход.</summary>
    public static bool IsDeletion(string kind) =>
        kind is DeleteExercise or DiscardFreeWorkoutDraft or DeletePlan or DeleteWorkoutDay or DeleteHistory;
}

/// <summary>
/// Изменение, которое сервер отклонил. Человеку показывается, что именно не
/// сохранилось, вместо того чтобы молча потерять его.
/// </summary>
public sealed record SyncIssue(
    Guid OperationId,
    string Kind,
    string? Label,
    string Message,
    DateTimeOffset OccurredAtUtc);
