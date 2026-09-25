namespace WorkoutPlanner.Web.Models;

public static class ExerciseOrdering
{
    /// <summary>
    /// Порядок упражнений в тренировке, общий для телефона, часов и истории.
    /// </summary>
    /// <remarks>
    /// Упражнения идут в порядке добавления. Суперсет стоит на месте своего
    /// первого упражнения, а внутри него действует порядок, выбранный человеком.
    /// </remarks>
    public static IEnumerable<Exercise> InWorkoutOrder(this IEnumerable<Exercise> exercises)
    {
        var list = exercises.ToList();
        var supersetAnchors = list
            .Where(x => x.SupersetGroupId is not null)
            .GroupBy(x => x.SupersetGroupId!.Value)
            .ToDictionary(x => x.Key, x => x.Min(y => y.Id));

        return list
            .OrderBy(x => x.SupersetGroupId is { } groupId ? supersetAnchors[groupId] : x.Id)
            .ThenBy(x => x.SupersetOrder ?? 0)
            .ThenBy(x => x.Id);
    }
}
