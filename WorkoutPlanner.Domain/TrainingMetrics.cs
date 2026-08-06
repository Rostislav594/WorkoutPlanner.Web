namespace WorkoutPlanner.Domain;

public sealed record SetPerformance(double Weight, int Repetitions);

public sealed record ExercisePerformance(
    double ExerciseCoefficient,
    IReadOnlyCollection<double> SecondaryMuscleCoefficients,
    IReadOnlyCollection<SetPerformance> Sets);

public static class TrainingMetrics
{
    public static double CalculateVolume(IEnumerable<SetPerformance> sets) =>
        sets.Sum(x => x.Weight * x.Repetitions);

    public static double CalculateExerciseScore(ExercisePerformance exercise)
    {
        var volume = CalculateVolume(exercise.Sets);
        var secondaryCoefficient = 1 + exercise.SecondaryMuscleCoefficients
            .Sum(x => x * 0.5);
        return volume * exercise.ExerciseCoefficient * secondaryCoefficient;
    }

    public static decimal CalculatePercentageChange(
        double baseline,
        double current) =>
        Math.Round(
            (decimal)(baseline <= 0
                ? 0
                : ((current - baseline) / baseline) * 100),
            2);
}
