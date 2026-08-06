using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public class ExerciseService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;

    public ExerciseService(
        WorkoutDbContext db,
        CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<Exercise>> GetExercisesAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var exercises = await _db.Exercises
            .Include(x => x.ExerciseDefinition)
                .ThenInclude(x => x!.SecondaryMuscles)
                    .ThenInclude(x => x.Muscle)
            .Include(x => x.Sets)
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.UserId == userId)
            .ToListAsync();

        return exercises;
    }

    public async Task AddExerciseAsync(
        Exercise exercise)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var ownsPlan = await _db.TrainingPlans.AnyAsync(x =>
            x.Id == exercise.TrainingPlanId &&
            x.UserId == userId);

        if (!ownsPlan)
        {
            throw new InvalidOperationException(
                "The selected training plan does not belong to the current user.");
        }

        exercise.UserId = userId;

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
        var userId = await _currentUser.GetRequiredUserIdAsync();

        if (exercise.UserId != userId)
            return;

        _db.Exercises.Remove(exercise);

        await _db.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _currentUser.GetRequiredUserIdAsync();
        await _db.SaveChangesAsync();
    }

}
