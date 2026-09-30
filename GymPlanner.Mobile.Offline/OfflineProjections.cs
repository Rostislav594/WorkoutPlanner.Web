using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Как изменение, ещё не дошедшее до сервера, выглядит в сохранённых данных:
/// экраны должны сразу показать завершённую тренировку, а не ждать связи.
/// Когда сервер ответит, копия перечитается и эти прикидки заменятся настоящими данными.
/// </summary>
public static class OfflineProjections
{
    public static TrainingPlanApiResponse ApplyScheduledResults(
        TrainingPlanApiResponse plan,
        CompleteScheduledWorkoutRequest request)
    {
        var results = request.Exercises.ToDictionary(x => x.ExerciseId);
        return plan with
        {
            Exercises = plan.Exercises
                .Select(exercise => results.TryGetValue(exercise.Id, out var result)
                    ? exercise with
                    {
                        Status = result.Status,
                        SetsCount = result.Sets.Count,
                        Sets = result.Sets.OrderBy(x => x.SetNumber).Select(set => ToSet(set, exercise.Sets)).ToList()
                    }
                    : exercise)
                .ToList()
        };
    }

    public static WorkoutHistoryApiResponse ScheduledHistory(
        TrainingPlanApiResponse completedPlan,
        DateTime completedAt,
        int localId) =>
        new(
            localId,
            completedPlan.WorkoutName,
            completedAt,
            string.Empty,
            true,
            completedPlan.Exercises
                .Select(x => new WorkoutHistoryExerciseApiResponse(x.Name, x.Status, x.Sets, x.SupersetGroupId))
                .ToList());

    public static WorkoutHistoryApiResponse FreeHistory(
        CompleteFreeWorkoutRequest request,
        string workoutName,
        DateTime completedAt,
        int localId) =>
        new(
            localId,
            workoutName,
            completedAt,
            string.Empty,
            true,
            request.Exercises
                .Select(x => new WorkoutHistoryExerciseApiResponse(
                    x.Name.Trim(),
                    x.Status,
                    (x.Sets ?? []).OrderBy(set => set.SetNumber).Select(set => ToSet(set)).ToList(),
                    x.SupersetGroupId))
                .ToList());

    /// <summary>Шаблон из свободной тренировки: подходы переносятся без отметок о выполнении.</summary>
    public static TrainingPlanApiResponse TemplateFromFreeWorkout(
        CompleteFreeWorkoutRequest request,
        int localPlanId,
        DateTime date) =>
        new(
            localPlanId,
            request.TemplateName!.Trim(),
            date.Date,
            request.Exercises
                .Select(x => new ExerciseApiResponse(
                    OfflineIds.Next(),
                    x.Name.Trim(),
                    (x.Sets ?? []).Count,
                    "NotCompleted",
                    localPlanId,
                    x.ExerciseDefinitionId,
                    false,
                    (x.Sets ?? []).OrderBy(set => set.SetNumber).Select(set => ToSet(set) with { Completed = false }).ToList(),
                    x.SupersetGroupId,
                    x.RestBetweenSetsSeconds ?? RestTimerDefaults.BetweenSetsSeconds,
                    x.RestAfterExerciseSeconds))
                .ToList(),
            request.RestBetweenExercisesSeconds ?? RestTimerDefaults.BetweenExercisesSeconds);

    /// <param name="current">
    /// Сохранённое упражнение: запрос без таймеров их не меняет, как и на сервере.
    /// </param>
    public static ExerciseApiResponse Exercise(
        int id,
        int trainingPlanId,
        SaveExerciseRequest request,
        bool hasPhoto,
        ExerciseApiResponse? current = null) =>
        new(
            id,
            request.Name.Trim(),
            (request.Sets ?? []).Count,
            request.Status,
            trainingPlanId,
            request.ExerciseDefinitionId,
            hasPhoto,
            (request.Sets ?? []).OrderBy(x => x.SetNumber).Select(set => ToSet(set, current?.Sets)).ToList(),
            request.SupersetGroupId,
            request.RestBetweenSetsSeconds
                ?? current?.RestBetweenSetsSeconds
                ?? RestTimerDefaults.BetweenSetsSeconds,
            request.RestAfterExerciseSeconds ?? current?.RestAfterExerciseSeconds);

    /// <summary>Таймеры отдыха шаблона; незаданное остаётся как было.</summary>
    public static TrainingPlanApiResponse ApplyRestTimers(
        TrainingPlanApiResponse plan,
        UpdateRestTimersRequest request)
    {
        var restBetweenSets = (request.Exercises ?? [])
            .GroupBy(x => x.ExerciseId)
            .ToDictionary(x => x.Key, x => x.Last().RestBetweenSetsSeconds);
        var restAfterExercises = (request.AfterExercises ?? [])
            .GroupBy(x => x.ExerciseId)
            .ToDictionary(x => x.Key, x => x.Last().RestAfterSeconds);
        var restAfterSets = (request.Sets ?? [])
            .GroupBy(x => (x.ExerciseId, x.SetNumber))
            .ToDictionary(x => x.Key, x => x.Last().RestAfterSeconds);
        return plan with
        {
            RestBetweenExercisesSeconds = request.RestBetweenExercisesSeconds ?? plan.RestBetweenExercisesSeconds,
            Exercises = plan.Exercises
                .Select(exercise => exercise with
                {
                    RestBetweenSetsSeconds = restBetweenSets.GetValueOrDefault(exercise.Id, exercise.RestBetweenSetsSeconds),
                    RestAfterExerciseSeconds = restAfterExercises.TryGetValue(exercise.Id, out var afterExercise)
                        ? afterExercise
                        : exercise.RestAfterExerciseSeconds,
                    Sets = exercise.Sets
                        .Select(set => restAfterSets.TryGetValue((exercise.Id, set.SetNumber), out var seconds)
                            ? set with { RestAfterSeconds = seconds }
                            : set)
                        .ToList()
                })
                .ToList()
        };
    }

    /// <summary>Порядок упражнений внутри суперсета; остальные остаются на местах.</summary>
    public static TrainingPlanApiResponse ReorderSuperset(
        TrainingPlanApiResponse plan,
        IReadOnlyList<int> exerciseIds)
    {
        var members = exerciseIds
            .Select(id => plan.Exercises.FirstOrDefault(x => x.Id == id))
            .OfType<ExerciseApiResponse>()
            .ToList();
        if (members.Count != exerciseIds.Count)
            return plan;

        var slots = plan.Exercises
            .Select((exercise, index) => (exercise, index))
            .Where(x => exerciseIds.Contains(x.exercise.Id))
            .Select(x => x.index)
            .ToList();
        var exercises = plan.Exercises.ToList();
        for (var position = 0; position < slots.Count; position++)
            exercises[slots[position]] = members[position];

        return plan with { Exercises = exercises };
    }

    /// <param name="current">Сохранённые подходы: подход без таймера оставляет прежний.</param>
    private static ExerciseSetApiResponse ToSet(
        SaveExerciseSetRequest set,
        IReadOnlyList<ExerciseSetApiResponse>? current = null) =>
        new(
            set.SetNumber,
            set.Repetitions,
            set.Weight,
            set.Completed,
            set.IsWarmup,
            set.RestAfterSeconds ?? current?.FirstOrDefault(x => x.SetNumber == set.SetNumber)?.RestAfterSeconds);
}
