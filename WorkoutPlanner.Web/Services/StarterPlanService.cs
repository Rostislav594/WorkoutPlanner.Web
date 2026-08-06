using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public sealed class StarterPlanService
{
    private static readonly string[] StarterPlanNames =
    [
        "Верх 1",
        "Низ 1",
        "Верх 2",
        "Низ 2"
    ];

    private readonly WorkoutDbContext _db;

    public StarterPlanService(WorkoutDbContext db)
    {
        _db = db;
    }

    public async Task CreateForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existingNames = await _db.TrainingPlans
            .Where(x => x.UserId == userId)
            .Select(x => x.WorkoutName)
            .ToListAsync(cancellationToken);

        var namesToCreate = StarterPlanNames
            .Except(existingNames, StringComparer.Ordinal)
            .ToList();

        if (namesToCreate.Count == 0)
            return;

        var today = DateTime.Today;

        _db.TrainingPlans.AddRange(
            namesToCreate.Select(name => new TrainingPlan
            {
                UserId = userId,
                WorkoutName = name,
                Date = today
            }));

        await _db.SaveChangesAsync(cancellationToken);
    }
}
