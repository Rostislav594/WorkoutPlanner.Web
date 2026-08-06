using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Application.Mapping;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class TodayWorkoutService : ITodayWorkoutService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;

    public TodayWorkoutService(
        IDbContextFactory<WorkoutDbContext> dbFactory,
        CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<TrainingPlan?> GetTodayWorkoutAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var day = await db.WorkoutDays
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Date.Date == DateTime.Today &&
                     !x.IsCompleted &&
                     x.UserId == userId,
                cancellationToken);

        if (day is null)
            return null;

        var plan = await db.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .FirstOrDefaultAsync(
                x => x.Id == day.TrainingPlanId && x.UserId == userId,
                cancellationToken);

        return plan?.ToContract();
    }
}
