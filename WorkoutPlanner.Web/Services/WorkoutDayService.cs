using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class WorkoutDayService
{
    private readonly WorkoutDbContext _db;

    public WorkoutDayService(
        WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task<List<WorkoutDay>> GetDaysAsync()
    {
        return await _db.WorkoutDays
            .OrderBy(x => x.Date)
            .ToListAsync();
    }

    public async Task SaveDayAsync(
        DateTime date,
        int trainingPlanId)
    {
        var existing =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Date.Date == date.Date);

        if (existing != null)
        {
            existing.TrainingPlanId =
                trainingPlanId;
        }
        else
        {
            _db.WorkoutDays.Add(
                new WorkoutDay
                {
                    Date = date,
                    TrainingPlanId = trainingPlanId
                });
        }

        await _db.SaveChangesAsync();
    }
    public async Task RemoveTodayWorkoutAsync()
    {
        var todayRecord =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Date.Date ==
                    DateTime.Today);

        if (todayRecord == null)
            return;

        _db.WorkoutDays.Remove(todayRecord);

        await _db.SaveChangesAsync();
    }
    public async Task DeleteDayAsync(int id)
    {
        var day =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x => x.Id == id);

        if (day == null)
            return;

        _db.WorkoutDays.Remove(day);

        await _db.SaveChangesAsync();
    }
    public async Task CompleteTodayWorkoutAsync()
    {
        var todayWorkout =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Date.Date == DateTime.Today);

        if (todayWorkout == null)
            return;

        todayWorkout.IsCompleted = true;

        await _db.SaveChangesAsync();
    }
    
}