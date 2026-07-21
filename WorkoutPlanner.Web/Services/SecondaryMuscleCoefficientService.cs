using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;

namespace WorkoutPlanner.Web.Services;

public class SecondaryMuscleCoefficientService
{
    private readonly WorkoutDbContext _db;

    // Насколько учитывается каждая вторичная мышца.
    private const double SecondaryInfluence = 0.5;

    public SecondaryMuscleCoefficientService(WorkoutDbContext db)
    {
        _db = db;
    }

    public double GetCoefficient(int exerciseDefinitionId)
    {
        var coefficient = 1.0;

        var secondaryMuscles = _db.ExerciseSecondaryMuscles
            .Where(x => x.ExerciseDefinitionId == exerciseDefinitionId)
            .ToList();

        foreach (var muscle in secondaryMuscles)
        {
            coefficient += muscle.Coefficient * SecondaryInfluence;
        }

        return coefficient;
    }
}
