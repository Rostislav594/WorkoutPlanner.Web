using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;

namespace WorkoutPlanner.Web.Tests.Unit;

public sealed class MuscleLoadCalculatorTests
{
    [Fact]
    public void Calculate_UsesTheActualWeightOfEverySet()
    {
        var exercise = new Exercise
        {
            ExerciseDefinition = new ExerciseDefinition
            {
                ExerciseCoefficient = 1.5,
                PrimaryMuscle = new Muscle { Name = "Chest" }
            },
            Sets =
            [
                new ExerciseTemplateSet
                {
                    SetNumber = 1,
                    Weight = 10,
                    Repetitions = 5
                },
                new ExerciseTemplateSet
                {
                    SetNumber = 2,
                    Weight = 20,
                    Repetitions = 5
                }
            ]
        };

        var result = new MuscleLoadCalculator().Calculate(exercise);

        Assert.Equal(225, result["Chest"]);
    }
}
