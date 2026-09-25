namespace GymPlanner.Mobile.Components;

public static class SupersetOrdering
{
    /// <summary>
    /// Ставит упражнения суперсета в новом порядке на те же места списка,
    /// которые они занимали, — остальные упражнения не двигаются.
    /// </summary>
    public static T[] Apply<T>(IEnumerable<T> exercises, IReadOnlyList<int> newOrder, Func<T, int> idOf)
    {
        var result = exercises.ToArray();
        var members = result.Where(x => newOrder.Contains(idOf(x))).ToDictionary(idOf);
        var slot = 0;
        for (var index = 0; index < result.Length; index++)
        {
            if (members.ContainsKey(idOf(result[index])))
                result[index] = members[newOrder[slot++]];
        }

        return result;
    }
}
