using WorkoutPlanner.Web.Models;

using WorkoutPlanner.Domain;

namespace WorkoutPlanner.Web.Services;

public class MuscleLoadCalculator
{
    public Dictionary<string, double> Calculate(Exercise exercise)
    {
        var result = new Dictionary<string, double>();

        if (exercise.ExerciseDefinition == null)
            return result;

        var volume = TrainingMetrics.CalculateVolume(
            exercise.Sets.Select(x =>
                new SetPerformance(x.Weight, x.Repetitions)));

        // Основная мышца
        result.Add(
            exercise.ExerciseDefinition.PrimaryMuscle!.Name,
            volume * exercise.ExerciseDefinition.ExerciseCoefficient);

        // Вторичные
        foreach (var secondary in exercise.ExerciseDefinition.SecondaryMuscles)
        {
            result.Add(
                secondary.Muscle!.Name,
                volume *
                exercise.ExerciseDefinition.ExerciseCoefficient *
                secondary.Coefficient);
        }

        return result;
    }
}
