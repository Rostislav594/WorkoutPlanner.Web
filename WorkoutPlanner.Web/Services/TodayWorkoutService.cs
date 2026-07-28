using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public class TodayWorkoutService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;

    public TodayWorkoutService(
        WorkoutDbContext db,
        CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TrainingPlan?> GetTodayWorkoutAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var day = await _db.WorkoutDays
            .FirstOrDefaultAsync(x =>
                x.Date.Date == DateTime.Today &&
                !x.IsCompleted &&
                (x.UserId == userId || x.UserId == null));

        if (day == null)
            return null;

        if (day.UserId == null)
        {
            day.UserId = userId;
            await _db.SaveChangesAsync();
        }

        return await _db.TrainingPlans
            .FirstOrDefaultAsync(x =>
                x.Id == day.TrainingPlanId &&
                (x.UserId == userId || x.UserId == null));
    }
}