using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Сохранённые планы: список «Мои тренировки» в серверном порядке плюс все
/// известные планы по ID, включая черновик свободной тренировки.
/// </summary>
public sealed record OfflinePlans(
    IReadOnlyList<int> Order,
    IReadOnlyDictionary<int, TrainingPlanApiResponse> Plans)
{
    public static OfflinePlans Empty { get; } = new([], new Dictionary<int, TrainingPlanApiResponse>());

    public IReadOnlyList<TrainingPlanApiResponse> Listed =>
        Order.Where(Plans.ContainsKey).Select(x => Plans[x]).ToList();

    public TrainingPlanApiResponse? Find(int id) => Plans.GetValueOrDefault(id);

    /// <summary>Свежий список с сервера заменяет прежний; остальные известные планы остаются.</summary>
    public OfflinePlans WithListed(IEnumerable<TrainingPlanApiResponse> listed)
    {
        var plans = Plans
            .Where(x => !Order.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value);
        var order = new List<int>();
        foreach (var plan in listed)
        {
            plans[plan.Id] = plan;
            order.Add(plan.Id);
        }

        return new(order, plans);
    }

    /// <summary>Добавляет или заменяет план; новый план в списке «Мои тренировки» встаёт в конец.</summary>
    public OfflinePlans Upsert(TrainingPlanApiResponse plan, bool listed = true)
    {
        var plans = new Dictionary<int, TrainingPlanApiResponse>(Plans) { [plan.Id] = plan };
        var order = Order.ToList();
        if (listed && !order.Contains(plan.Id))
            order.Add(plan.Id);

        return new(order, plans);
    }

    public OfflinePlans Remove(int planId)
    {
        var plans = new Dictionary<int, TrainingPlanApiResponse>(Plans);
        plans.Remove(planId);
        return new(Order.Where(x => x != planId).ToList(), plans);
    }

    public OfflinePlans UpsertExercise(ExerciseApiResponse exercise)
    {
        if (!Plans.TryGetValue(exercise.TrainingPlanId, out var plan))
            return this;

        var exercises = plan.Exercises.ToList();
        var index = exercises.FindIndex(x => x.Id == exercise.Id);
        if (index >= 0)
            exercises[index] = exercise;
        else
            exercises.Add(exercise);

        return Upsert(plan with { Exercises = exercises }, listed: Order.Contains(plan.Id));
    }

    public OfflinePlans RemoveExercise(int exerciseId)
    {
        var owner = Plans.Values.FirstOrDefault(x => x.Exercises.Any(y => y.Id == exerciseId));
        return owner is null
            ? this
            : Upsert(
                owner with { Exercises = owner.Exercises.Where(x => x.Id != exerciseId).ToList() },
                listed: Order.Contains(owner.Id));
    }

    public ExerciseApiResponse? FindExercise(int exerciseId) =>
        Plans.Values.SelectMany(x => x.Exercises).FirstOrDefault(x => x.Id == exerciseId);
}
