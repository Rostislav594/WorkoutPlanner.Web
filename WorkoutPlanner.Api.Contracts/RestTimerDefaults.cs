namespace WorkoutPlanner.Api.Contracts;

/// <summary>
/// Длительности отдыха по умолчанию и допустимые границы.
/// </summary>
/// <remarks>
/// Отдых между подходами хранится в каждом упражнении шаблона, отдых между
/// упражнениями — в самом шаблоне. Эти значения получают новые упражнения и
/// шаблоны, пока человек не выставит свои.
/// </remarks>
public static class RestTimerDefaults
{
    public const int BetweenSetsSeconds = 90;
    public const int BetweenExercisesSeconds = 120;
    public const int MinSeconds = 5;
    public const int MaxSeconds = 3600;

    public static bool IsValid(int seconds) => seconds is >= MinSeconds and <= MaxSeconds;
}
