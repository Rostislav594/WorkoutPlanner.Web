using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class TrainingPlanService
{
    private readonly WorkoutDbContext _db;

    public TrainingPlanService(
        WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task<List<TrainingPlan>>
        GetTrainingPlansAsync()
    {
        return await _db.TrainingPlans
            .OrderBy(x => x.Id)
            .ToListAsync();
    }
    public async Task<TrainingPlan?> GetByIdAsync(int id)
    {
        return await _db.TrainingPlans
            .FirstOrDefaultAsync(x => x.Id == id);
    }
}
