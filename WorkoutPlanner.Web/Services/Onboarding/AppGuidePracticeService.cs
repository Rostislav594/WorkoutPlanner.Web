using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.Onboarding;

public sealed class AppGuidePracticeService
{
    private const string LoginProvider = "GymPlanner";
    private const string TutorialPlanTokenName = "AppGuideTutorialPlanV2";
    public const string TutorialWorkoutName = "Знакомство с GymPlanner";
    public const string TutorialExerciseName = "Первое упражнение";

    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;

    public AppGuidePracticeService(
        IDbContextFactory<WorkoutDbContext> dbFactory,
        CurrentUserService currentUser,
        IServiceScopeFactory scopeFactory)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _scopeFactory = scopeFactory;
    }

    public async Task<int> EnsureTutorialPlanAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var identityScope = _scopeFactory.CreateAsyncScope();
        var userManager = identityScope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Authenticated user was not found.");
        var storedPlanId = await userManager.GetAuthenticationTokenAsync(
            user,
            LoginProvider,
            TutorialPlanTokenName);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        TrainingPlan? plan = null;
        if (int.TryParse(storedPlanId, out var tutorialPlanId))
        {
            plan = await db.TrainingPlans
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .FirstOrDefaultAsync(
                x => x.Id == tutorialPlanId && x.UserId == userId,
                cancellationToken);
        }

        if (plan is null)
        {
            plan = new TrainingPlan
            {
                UserId = userId,
                WorkoutName = TutorialWorkoutName,
                Date = DateTime.Today
            };
            db.TrainingPlans.Add(plan);
        }

        if (plan.Exercises.Count == 0)
        {
            plan.Exercises.Add(new Exercise
            {
                UserId = userId,
                Name = TutorialExerciseName,
                WorkoutName = TutorialWorkoutName,
                SetsCount = 3,
                Status = ExerciseStatus.NotCompleted,
                Sets =
                [
                    new() { SetNumber = 1, Repetitions = 10, Weight = 0 },
                    new() { SetNumber = 2, Repetitions = 8, Weight = 0 },
                    new() { SetNumber = 3, Repetitions = 6, Weight = 0 }
                ]
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var tokenResult = await userManager.SetAuthenticationTokenAsync(
            user,
            LoginProvider,
            TutorialPlanTokenName,
            plan.Id.ToString());
        if (!tokenResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(" ", tokenResult.Errors.Select(x => x.Description)));
        }

        return plan.Id;
    }

    public async Task<string> GetTutorialRouteAsync(
        CancellationToken cancellationToken = default)
    {
        var planId = await EnsureTutorialPlanAsync(cancellationToken);
        return $"/workout/{planId}";
    }

    public async Task ScheduleTutorialTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var planId = await EnsureTutorialPlanAsync(cancellationToken);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var plan = await db.TrainingPlans
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .FirstAsync(x => x.Id == planId && x.UserId == userId, cancellationToken);

        foreach (var exercise in plan.Exercises)
        {
            exercise.Status = ExerciseStatus.NotCompleted;
            foreach (var set in exercise.Sets)
                set.Completed = false;
        }

        var today = await db.WorkoutDays.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Date.Date == DateTime.Today,
            cancellationToken);

        if (today is null)
        {
            db.WorkoutDays.Add(new WorkoutDay
            {
                UserId = userId,
                Date = DateTime.Today,
                TrainingPlanId = planId
            });
        }
        else
        {
            today.TrainingPlanId = planId;
            today.IsCompleted = false;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
