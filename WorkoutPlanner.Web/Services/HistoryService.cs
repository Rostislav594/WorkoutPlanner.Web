using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public class HistoryService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;

    public HistoryService(
        WorkoutDbContext db,
        CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task AddHistoryAsync(
        WorkoutHistory history)
    {
        history.UserId = await _currentUser.GetRequiredUserIdAsync();

        _db.WorkoutHistory.Add(history);

        await _db.SaveChangesAsync();
    }

    public async Task<List<WorkoutHistory>>
        GetHistoryAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var history = await _db.WorkoutHistory
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ToListAsync();

        return history;
    }

    public async Task DeleteHistoryAsync(int id)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var item =
            await _db.WorkoutHistory
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

        if (item == null)
            return;

        _db.WorkoutHistory.Remove(item);

        await _db.SaveChangesAsync();
    }

}
