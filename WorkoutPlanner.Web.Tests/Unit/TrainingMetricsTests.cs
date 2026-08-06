using WorkoutPlanner.Domain;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class TrainingMetricsTests
{
    [Fact]
    public void ExerciseScore_UsesEverySetsOwnWeight()
    {
        var score = TrainingMetrics.CalculateExerciseScore(
            new ExercisePerformance(
                1.5,
                [0.2],
                [
                    new SetPerformance(10, 5),
                    new SetPerformance(20, 5)
                ]));

        Assert.Equal(247.5, score, precision: 10);
    }

    [Fact]
    public void PercentageChange_UsesStableBaseline()
    {
        Assert.Equal(50m, TrainingMetrics.CalculatePercentageChange(100, 150));
        Assert.Equal(0m, TrainingMetrics.CalculatePercentageChange(0, 150));
    }
}
