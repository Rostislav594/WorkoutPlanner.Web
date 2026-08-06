using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public class WorkoutDayService
{
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;

    public WorkoutDayService(
        WorkoutDbContext db,
        CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<WorkoutDay>> GetDaysAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var days = await _db.WorkoutDays
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Date)
            .ToListAsync();

        return days;
    }

    public async Task SaveDayAsync(
        DateTime date,
        int trainingPlanId)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var ownsPlan = await _db.TrainingPlans.AnyAsync(x =>
            x.Id == trainingPlanId &&
            x.UserId == userId);

        if (!ownsPlan)
        {
            throw new InvalidOperationException(
                "The selected training plan does not belong to the current user.");
        }

        var existing =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Date.Date == date.Date &&
                    x.UserId == userId);

        if (existing != null)
        {
            existing.TrainingPlanId = trainingPlanId;
        }
        else
        {
            _db.WorkoutDays.Add(
                new WorkoutDay
                {
                    UserId = userId,
                    Date = date,
                    TrainingPlanId = trainingPlanId
                });
        }

        await _db.SaveChangesAsync();
    }

    public async Task RemoveTodayWorkoutAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var todayRecord =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Date.Date == DateTime.Today &&
                    x.UserId == userId);

        if (todayRecord == null)
            return;

        _db.WorkoutDays.Remove(todayRecord);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteDayAsync(int id)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var day =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

        if (day == null)
            return;

        _db.WorkoutDays.Remove(day);

        await _db.SaveChangesAsync();
    }

    public async Task CompleteTodayWorkoutAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var todayWorkout =
            await _db.WorkoutDays
                .FirstOrDefaultAsync(x =>
                    x.Date.Date == DateTime.Today &&
                    x.UserId == userId);

        if (todayWorkout == null)
            return;

        todayWorkout.IsCompleted = true;

        await _db.SaveChangesAsync();
    }

}
