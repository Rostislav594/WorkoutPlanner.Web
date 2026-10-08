using WorkoutPlanner.Domain;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class NextSessionTargetsTests
{
    [Fact]
    public void WeightStep_KeepsEverySetsOwnWeight()
    {
        Assert.Equal(
            new SetPerformance(82.5, 8),
            NextSessionTargets.Apply(new SetPerformance(80, 8), isWarmup: false, weightStep: 2.5, addRepetition: false));
        Assert.Equal(
            new SetPerformance(80, 8),
            NextSessionTargets.Apply(new SetPerformance(77.5, 8), isWarmup: false, weightStep: 2.5, addRepetition: false));
    }

    [Fact]
    public void WeightStep_UsesTheChosenValue()
    {
        Assert.Equal(
            new SetPerformance(85, 8),
            NextSessionTargets.Apply(new SetPerformance(80, 8), isWarmup: false, weightStep: 5, addRepetition: false));
        Assert.Equal(
            new SetPerformance(81.25, 8),
            NextSessionTargets.Apply(new SetPerformance(80, 8), isWarmup: false, weightStep: 1.25, addRepetition: false));
    }

    [Fact]
    public void AddRepetition_ChangesOnlyRepetitions()
    {
        Assert.Equal(
            new SetPerformance(30, 11),
            NextSessionTargets.Apply(new SetPerformance(30, 10), isWarmup: false, weightStep: null, addRepetition: true));
    }

    [Fact]
    public void WeightAndRepetitionCanChangeTogether()
    {
        Assert.Equal(
            new SetPerformance(82.5, 9),
            NextSessionTargets.Apply(new SetPerformance(80, 8), isWarmup: false, weightStep: 2.5, addRepetition: true));
    }

    [Fact]
    public void WarmupSetsStayAsTheyWere()
    {
        var warmup = new SetPerformance(40, 8);

        Assert.Equal(warmup, NextSessionTargets.Apply(warmup, isWarmup: true, weightStep: 2.5, addRepetition: true));
    }

    [Fact]
    public void NoChangeLeavesTheSetAsItWas()
    {
        var set = new SetPerformance(80, 8);

        Assert.Equal(set, NextSessionTargets.Apply(set, isWarmup: false, weightStep: null, addRepetition: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2.5)]
    [InlineData(0.1)]
    [InlineData(150)]
    [InlineData(double.NaN)]
    public void WeightStepOutsideTheRangeIsRejected(double weightStep)
    {
        Assert.False(NextSessionTargets.IsValidWeightStep(weightStep));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NextSessionTargets.Apply(new SetPerformance(80, 8), isWarmup: false, weightStep, addRepetition: false));
    }

    [Fact]
    public void StepNeverLeavesTheAllowedRange()
    {
        Assert.Equal(
            NextSessionTargets.MaxWeight,
            NextSessionTargets.Apply(new SetPerformance(1999, 5), isWarmup: false, weightStep: 2.5, addRepetition: false).Weight);
        Assert.Equal(
            NextSessionTargets.MaxRepetitions,
            NextSessionTargets.Apply(new SetPerformance(10, 1000), isWarmup: false, weightStep: null, addRepetition: true).Repetitions);
    }
}
