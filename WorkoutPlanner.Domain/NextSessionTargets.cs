namespace WorkoutPlanner.Domain;

/// <summary>
/// Цель на следующую тренировку: прибавка веса и повтор добавляются к каждому
/// рабочему подходу отдельно, так что у подходов остаются свои значения.
/// Разминочные не трогаем.
/// </summary>
public static class NextSessionTargets
{
    public const double DefaultWeightStep = 2.5;
    public const double MinWeightStep = 0.25;
    public const double MaxWeightStep = 100;
    public const int RepetitionStep = 1;
    public const double MaxWeight = 2000;
    public const int MaxRepetitions = 1000;

    public static bool IsValidWeightStep(double weightStep) =>
        double.IsFinite(weightStep) && weightStep is >= MinWeightStep and <= MaxWeightStep;

    /// <param name="weightStep">Прибавка веса; <c>null</c> — вес не меняется.</param>
    /// <param name="addRepetition">Добавить ли подходу повтор.</param>
    public static SetPerformance Apply(
        SetPerformance set,
        bool isWarmup,
        double? weightStep,
        bool addRepetition)
    {
        if (weightStep is { } step && !IsValidWeightStep(step))
            throw new ArgumentOutOfRangeException(nameof(weightStep), weightStep, "Weight step is out of range.");

        if (isWarmup)
            return set;

        return set with
        {
            Weight = weightStep is { } increase ? Math.Min(set.Weight + increase, MaxWeight) : set.Weight,
            Repetitions = addRepetition ? Math.Min(set.Repetitions + RepetitionStep, MaxRepetitions) : set.Repetitions
        };
    }
}
