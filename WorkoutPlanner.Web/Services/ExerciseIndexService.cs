using WorkoutPlanner.Web.Models;

using WorkoutPlanner.Domain;

namespace WorkoutPlanner.Web.Services;

public class ExerciseIndexService
{
    public double Calculate(Exercise exercise)
    {
        if (exercise.ExerciseDefinition == null)
            return 0;

        return TrainingMetrics.CalculateExerciseScore(
            new ExercisePerformance(
                exercise.ExerciseDefinition.ExerciseCoefficient,
                exercise.ExerciseDefinition.SecondaryMuscles
                    .Select(x => x.Coefficient)
                    .ToArray(),
                exercise.Sets
                    .Select(x => new SetPerformance(x.Weight, x.Repetitions))
                    .ToArray()));
    }
}
