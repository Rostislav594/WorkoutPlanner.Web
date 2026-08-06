using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class ExerciseIndexService
{
    private readonly SecondaryMuscleCoefficientService _secondaryService;

    public ExerciseIndexService(
        SecondaryMuscleCoefficientService secondaryService)
    {
        _secondaryService = secondaryService;
    }

    public double Calculate(Exercise exercise)
    {
        if (exercise.ExerciseDefinition == null)
            return 0;

        double volume =
        exercise.Sets.Sum(x => x.Weight * x.Repetitions);

        double exerciseCoefficient =
            exercise.ExerciseDefinition.ExerciseCoefficient;

        double secondaryCoefficient =
            _secondaryService.GetCoefficient(
                exercise.ExerciseDefinition.Id);

        return volume
            * exerciseCoefficient
            * secondaryCoefficient;
    }
}