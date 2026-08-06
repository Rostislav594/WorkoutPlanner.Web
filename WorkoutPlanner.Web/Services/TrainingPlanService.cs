using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public class TrainingPlanService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;

    public TrainingPlanService(
        WorkoutDbContext db,
        CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<TrainingPlan>>
        GetTrainingPlansAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var plans = await _db.TrainingPlans
            .Include(x => x.Exercises)
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        return plans;
    }

    public async Task<TrainingPlan?> GetByIdAsync(int id)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var plan = await _db.TrainingPlans
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId);

        return plan;
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var normalizedName = NormalizeWorkoutName(workoutName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return (false, "Введите название тренировки.");

        if (await ExistsAsync(normalizedName, userId, null))
            return (false, "Тренировка с таким названием уже есть.");

        _db.TrainingPlans.Add(
            new TrainingPlan
            {
                UserId = userId,
                WorkoutName = normalizedName,
                Date = DateTime.Today
            });

        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RenameAsync(
        int id,
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var normalizedName = NormalizeWorkoutName(workoutName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return (false, "Введите название тренировки.");

        var plan = await _db.TrainingPlans
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId);

        if (plan == null)
            return (false, "Тренировка не найдена.");

        if (await ExistsAsync(normalizedName, userId, id))
            return (false, "Тренировка с таким названием уже есть.");

        var previousName = plan.WorkoutName;
        plan.WorkoutName = normalizedName;

        await RenameRelatedDataAsync(previousName, normalizedName, userId);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task DeleteAsync(int id)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var plan = await _db.TrainingPlans
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Include(x => x.TrainingSessions)
                .ThenInclude(x => x.Exercises)
                    .ThenInclude(x => x.Sets)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId);

        if (plan == null)
            return;

        var workoutDays = await _db.WorkoutDays
            .Where(x => x.TrainingPlanId == plan.Id)
            .ToListAsync();

        var workoutProgress = await _db.ProgressSnapshots
            .Where(x =>
                x.WorkoutName == plan.WorkoutName &&
                x.UserId == userId)
            .ToListAsync();

        var exerciseProgress = await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == plan.WorkoutName &&
                x.UserId == userId)
            .ToListAsync();

        _db.WorkoutDays.RemoveRange(workoutDays);
        _db.ProgressSnapshots.RemoveRange(workoutProgress);
        _db.ExerciseProgressSnapshots.RemoveRange(exerciseProgress);
        _db.TrainingPlans.Remove(plan);

        await _db.SaveChangesAsync();
    }

    private async Task<bool> ExistsAsync(
        string workoutName,
        string userId,
        int? exceptId)
    {
        return await _db.TrainingPlans.AnyAsync(x =>
            x.WorkoutName == workoutName &&
            x.UserId == userId &&
            (!exceptId.HasValue || x.Id != exceptId.Value));
    }

    private async Task RenameRelatedDataAsync(
        string previousName,
        string newName,
        string userId)
    {
        var exercises = await _db.Exercises
            .Where(x =>
                x.WorkoutName == previousName &&
                x.UserId == userId)
            .ToListAsync();

        foreach (var exercise in exercises)
        {
            exercise.WorkoutName = newName;
        }

        var workoutProgress = await _db.ProgressSnapshots
            .Where(x =>
                x.WorkoutName == previousName &&
                x.UserId == userId)
            .ToListAsync();

        foreach (var snapshot in workoutProgress)
        {
            snapshot.WorkoutName = newName;
        }

        var exerciseProgress = await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == previousName &&
                x.UserId == userId)
            .ToListAsync();

        foreach (var snapshot in exerciseProgress)
        {
            snapshot.WorkoutName = newName;
        }

        var history = await _db.WorkoutHistory
            .Where(x =>
                x.WorkoutName == previousName &&
                x.UserId == userId)
            .ToListAsync();

        foreach (var item in history)
        {
            item.WorkoutName = newName;
        }
    }

    private static string NormalizeWorkoutName(string workoutName)
    {
        return workoutName.Trim();
    }
}
