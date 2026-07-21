using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class TodayWorkoutService
{
    private readonly WorkoutDbContext _db;

    public TodayWorkoutService(
        WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task<TrainingPlan?> GetTodayWorkoutAsync()
    {
        var day =
    await _db.WorkoutDays
        .FirstOrDefaultAsync(x =>
            x.Date.Date == DateTime.Today
            && !x.IsCompleted);

        if (day == null)
            return null;

        return await _db.TrainingPlans
            .FirstOrDefaultAsync(x =>
                x.Id == day.TrainingPlanId);
    }
}