using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class ExerciseDefinitionService
{
    private readonly WorkoutDbContext _db;

    public ExerciseDefinitionService(WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task<List<ExerciseDefinition>> GetAllAsync()
    {
        return await _db.ExerciseDefinitions
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}