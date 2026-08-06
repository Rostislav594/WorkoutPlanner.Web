using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Application.Mapping;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class WorkoutDayService : IWorkoutDayService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;

    public WorkoutDayService(
        IDbContextFactory<WorkoutDbContext> dbFactory,
        CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<List<WorkoutDay>> GetDaysAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var days = await db.WorkoutDays
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return days.Select(x => x.ToContract()).ToList();
    }

    public async Task<List<WorkoutDay>> GetDaysAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var start = from.Date;
        var endExclusive = to.Date == DateTime.MaxValue.Date
            ? DateTime.MaxValue
            : to.Date.AddDays(1);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var days = await db.WorkoutDays
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Date >= start &&
                x.Date < endExclusive)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return days.Select(x => x.ToContract()).ToList();
    }

    public async Task<WorkoutDay?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var day = await db.WorkoutDays
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);

        return day?.ToContract();
    }

    public async Task<WorkoutDay> SaveDayAsync(
        DateTime date,
        int trainingPlanId,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var ownsPlan = await db.TrainingPlans.AnyAsync(
            x => x.Id == trainingPlanId && x.UserId == userId,
            cancellationToken);

        if (!ownsPlan)
            throw new InvalidOperationException("The selected training plan does not belong to the current user.");

        var existing = await db.WorkoutDays.FirstOrDefaultAsync(
            x => x.Date.Date == date.Date && x.UserId == userId,
            cancellationToken);

        Models.WorkoutDay day;
        if (existing is null)
        {
            day = new Models.WorkoutDay
            {
                UserId = userId,
                Date = date,
                TrainingPlanId = trainingPlanId
            };
            db.WorkoutDays.Add(day);
        }
        else
        {
            existing.TrainingPlanId = trainingPlanId;
            day = existing;
        }

        await db.SaveChangesAsync(cancellationToken);
        return day.ToContract();
    }

    public Task RemoveTodayWorkoutAsync(
        CancellationToken cancellationToken = default) =>
        DeleteForDateAsync(DateTime.Today, requireIncomplete: false, cancellationToken);

    public async Task<bool> DeleteDayAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var day = await db.WorkoutDays.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (day is null)
            return false;

        db.WorkoutDays.Remove(day);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CompleteTodayWorkoutAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var today = await db.WorkoutDays.FirstOrDefaultAsync(
            x => x.Date.Date == DateTime.Today && x.UserId == userId,
            cancellationToken);

        if (today is null)
            return;

        today.IsCompleted = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteForDateAsync(
        DateTime date,
        bool requireIncomplete,
        CancellationToken cancellationToken)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var day = await db.WorkoutDays.FirstOrDefaultAsync(
            x => x.Date.Date == date.Date &&
                 x.UserId == userId &&
                 (!requireIncomplete || !x.IsCompleted),
            cancellationToken);

        if (day is null)
            return;

        db.WorkoutDays.Remove(day);
        await db.SaveChangesAsync(cancellationToken);
    }
}
