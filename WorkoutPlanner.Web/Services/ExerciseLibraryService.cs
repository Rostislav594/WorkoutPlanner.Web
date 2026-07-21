using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class ExerciseLibraryService
{
    private readonly WorkoutDbContext _db;

    public ExerciseLibraryService(WorkoutDbContext db)
    {
        _db = db;
    }

    public ExerciseDefinition? GetDefinition(string exerciseName)
    {
        return _db.ExerciseDefinitions
            .Include(x => x.PrimaryMuscle)
            .Include(x => x.SecondaryMuscles)
                .ThenInclude(x => x.Muscle)
            .FirstOrDefault(x => x.Name == exerciseName);
    }
}
