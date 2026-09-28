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
                        Sets = result.Sets.OrderBy(x => x.SetNumber).Select(ToSet).ToList()
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
                    (x.Sets ?? []).OrderBy(set => set.SetNumber).Select(ToSet).ToList(),
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
                    x.SupersetGroupId))
                .ToList());

    public static ExerciseApiResponse Exercise(
        int id,
        int trainingPlanId,
        SaveExerciseRequest request,
        bool hasPhoto) =>
        new(
            id,
            request.Name.Trim(),
            (request.Sets ?? []).Count,
            request.Status,
            trainingPlanId,
            request.ExerciseDefinitionId,
            hasPhoto,
            (request.Sets ?? []).OrderBy(x => x.SetNumber).Select(ToSet).ToList(),
            request.SupersetGroupId);

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

    private static ExerciseSetApiResponse ToSet(SaveExerciseSetRequest set) =>
        new(set.SetNumber, set.Repetitions, set.Weight, set.Completed, set.IsWarmup);
}
