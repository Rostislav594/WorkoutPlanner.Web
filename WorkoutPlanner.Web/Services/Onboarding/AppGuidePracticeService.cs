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

    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;
    private readonly UserManager<IdentityUser> _userManager;

    public AppGuidePracticeService(
        WorkoutDbContext db,
        CurrentUserService currentUser,
        UserManager<IdentityUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public async Task<int> EnsureTutorialPlanAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Authenticated user was not found.");
        var storedPlanId = await _userManager.GetAuthenticationTokenAsync(
            user,
            LoginProvider,
            TutorialPlanTokenName);

        TrainingPlan? plan = null;
        if (int.TryParse(storedPlanId, out var tutorialPlanId))
        {
            plan = await _db.TrainingPlans
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
            _db.TrainingPlans.Add(plan);
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

        await _db.SaveChangesAsync(cancellationToken);

        var tokenResult = await _userManager.SetAuthenticationTokenAsync(
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
        var plan = await _db.TrainingPlans
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .FirstAsync(x => x.Id == planId && x.UserId == userId, cancellationToken);

        foreach (var exercise in plan.Exercises)
        {
            exercise.Status = ExerciseStatus.NotCompleted;
            foreach (var set in exercise.Sets)
                set.Completed = false;
        }

        var today = await _db.WorkoutDays.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Date.Date == DateTime.Today,
            cancellationToken);

        if (today is null)
        {
            _db.WorkoutDays.Add(new WorkoutDay
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

        await _db.SaveChangesAsync(cancellationToken);
    }
}
