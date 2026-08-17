using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Application.Mapping;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;

    public HistoryService(
        IDbContextFactory<WorkoutDbContext> dbFactory,
        CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task AddHistoryAsync(
        WorkoutHistory history,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.WorkoutHistory.Add(new Models.WorkoutHistory
        {
            UserId = userId,
            WorkoutName = history.WorkoutName,
            Date = history.Date,
            Summary = history.Summary,
            Details = history.Details
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<WorkoutHistory>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var history = await db.WorkoutHistory
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

        return history.Select(x => x.ToContract()).ToList();
    }

    public async Task<WorkoutHistory?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.WorkoutHistory
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);

        return item?.ToContract();
    }

    public async Task<bool> DeleteHistoryAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.WorkoutHistory.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (item is null)
            return false;

        var historyDate = item.Date.Date;
        var nextDate = historyDate.AddDays(1);
        var workoutProgress = await db.ProgressSnapshots
            .Where(x =>
                x.UserId == userId &&
                x.WorkoutName == item.WorkoutName &&
                x.Date >= historyDate &&
                x.Date < nextDate)
            .ToListAsync(cancellationToken);
        var exerciseProgress = await db.ExerciseProgressSnapshots
            .Where(x =>
                x.UserId == userId &&
                x.WorkoutName == item.WorkoutName &&
                x.Date >= historyDate &&
                x.Date < nextDate)
            .ToListAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        db.WorkoutHistory.Remove(item);
        db.ProgressSnapshots.RemoveRange(workoutProgress);
        db.ExerciseProgressSnapshots.RemoveRange(exerciseProgress);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
