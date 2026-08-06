using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public sealed class StarterPlanService : IStarterPlanService
{
    private static readonly string[] StarterPlanNames =
    [
        "Верх 1",
        "Низ 1",
        "Верх 2",
        "Низ 2"
    ];

    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;

    public StarterPlanService(IDbContextFactory<WorkoutDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task CreateForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existingNames = await db.TrainingPlans
            .Where(x => x.UserId == userId)
            .Select(x => x.WorkoutName)
            .ToListAsync(cancellationToken);

        var namesToCreate = StarterPlanNames
            .Except(existingNames, StringComparer.Ordinal)
            .ToList();

        if (namesToCreate.Count == 0)
            return;

        var today = DateTime.Today;

        db.TrainingPlans.AddRange(
            namesToCreate.Select(name => new TrainingPlan
            {
                UserId = userId,
                WorkoutName = name,
                Date = today
            }));

        await db.SaveChangesAsync(cancellationToken);
    }
}
