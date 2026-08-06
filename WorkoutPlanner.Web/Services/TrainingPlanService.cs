using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Application.Mapping;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class TrainingPlanService : ITrainingPlanService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;

    public TrainingPlanService(
        IDbContextFactory<WorkoutDbContext> dbFactory,
        CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<List<TrainingPlan>> GetTrainingPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var plans = await db.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return plans.Select(x => x.ToContract()).ToList();
    }

    public async Task<TrainingPlan?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var plan = await db.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);

        return plan?.ToContract();
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(
        string workoutName,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var normalizedName = NormalizeWorkoutName(workoutName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return (false, "Введите название тренировки.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (await ExistsAsync(db, normalizedName, userId, null, cancellationToken))
            return (false, "Тренировка с таким названием уже есть.");

        db.TrainingPlans.Add(
            new Models.TrainingPlan
            {
                UserId = userId,
                WorkoutName = normalizedName,
                Date = DateTime.Today
            });

        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RenameAsync(
        int id,
        string workoutName,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var normalizedName = NormalizeWorkoutName(workoutName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return (false, "Введите название тренировки.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var plan = await db.TrainingPlans.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (plan is null)
            return (false, "Тренировка не найдена.");

        if (await ExistsAsync(db, normalizedName, userId, id, cancellationToken))
            return (false, "Тренировка с таким названием уже есть.");

        var previousName = plan.WorkoutName;
        plan.WorkoutName = normalizedName;

        await RenameRelatedDataAsync(
            db,
            previousName,
            normalizedName,
            userId,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var plan = await db.TrainingPlans
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Include(x => x.TrainingSessions)
                .ThenInclude(x => x.Exercises)
                    .ThenInclude(x => x.Sets)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);

        if (plan is null)
            return;

        var workoutDays = await db.WorkoutDays
            .Where(x => x.TrainingPlanId == plan.Id)
            .ToListAsync(cancellationToken);
        var workoutProgress = await db.ProgressSnapshots
            .Where(x => x.WorkoutName == plan.WorkoutName && x.UserId == userId)
            .ToListAsync(cancellationToken);
        var exerciseProgress = await db.ExerciseProgressSnapshots
            .Where(x => x.WorkoutName == plan.WorkoutName && x.UserId == userId)
            .ToListAsync(cancellationToken);

        db.WorkoutDays.RemoveRange(workoutDays);
        db.ProgressSnapshots.RemoveRange(workoutProgress);
        db.ExerciseProgressSnapshots.RemoveRange(exerciseProgress);
        db.TrainingPlans.Remove(plan);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<bool> ExistsAsync(
        WorkoutDbContext db,
        string workoutName,
        string userId,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        return db.TrainingPlans.AnyAsync(
            x => x.WorkoutName == workoutName &&
                 x.UserId == userId &&
                 (!exceptId.HasValue || x.Id != exceptId.Value),
            cancellationToken);
    }

    private static async Task RenameRelatedDataAsync(
        WorkoutDbContext db,
        string previousName,
        string newName,
        string userId,
        CancellationToken cancellationToken)
    {
        var exercises = await db.Exercises
            .Where(x => x.WorkoutName == previousName && x.UserId == userId)
            .ToListAsync(cancellationToken);
        var workoutProgress = await db.ProgressSnapshots
            .Where(x => x.WorkoutName == previousName && x.UserId == userId)
            .ToListAsync(cancellationToken);
        var exerciseProgress = await db.ExerciseProgressSnapshots
            .Where(x => x.WorkoutName == previousName && x.UserId == userId)
            .ToListAsync(cancellationToken);
        var history = await db.WorkoutHistory
            .Where(x => x.WorkoutName == previousName && x.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var exercise in exercises)
            exercise.WorkoutName = newName;
        foreach (var snapshot in workoutProgress)
            snapshot.WorkoutName = newName;
        foreach (var snapshot in exerciseProgress)
            snapshot.WorkoutName = newName;
        foreach (var item in history)
            item.WorkoutName = newName;
    }

    private static string NormalizeWorkoutName(string workoutName) =>
        workoutName.Trim();
}
