using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class ExerciseService
{
    private readonly WorkoutDbContext _db;

    public ExerciseService(
        WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task<List<Exercise>> GetExercisesAsync(
    string workoutName)
    {
        return await _db.Exercises
        .Include(x => x.ExerciseDefinition)
            .ThenInclude(x => x!.SecondaryMuscles)
                .ThenInclude(x => x.Muscle)
        .Include(x => x.Sets)
        .Where(x => x.WorkoutName == workoutName)
        .ToListAsync();
    }

    public async Task AddExerciseAsync(
    Exercise exercise)
    {
        var definition = await _db.ExerciseDefinitions
            .FirstOrDefaultAsync(x => x.Name == exercise.Name);

        if (definition != null)
        {
            exercise.ExerciseDefinitionId = definition.Id;
        }

        _db.Exercises.Add(exercise);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteExerciseAsync(
        Exercise exercise)
    {
        _db.Exercises.Remove(exercise);

        await _db.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

}

