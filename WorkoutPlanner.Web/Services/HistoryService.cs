using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class HistoryService
{
    private readonly WorkoutDbContext _db;

    public HistoryService(
        WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task AddHistoryAsync(
        WorkoutHistory history)
    {
        _db.WorkoutHistory.Add(history);

        await _db.SaveChangesAsync();
    }

    public async Task<List<WorkoutHistory>>
        GetHistoryAsync()
    {
        return await _db.WorkoutHistory
            .OrderByDescending(x => x.Date)
            .ToListAsync();
    }
    public async Task DeleteHistoryAsync(int id)
    {
        var item =
            await _db.WorkoutHistory
                .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return;

        _db.WorkoutHistory.Remove(item);

        await _db.SaveChangesAsync();
    }
}