using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Application.Mapping;
using WorkoutPlanner.Web.Data;

namespace WorkoutPlanner.Web.Services;

public sealed class ExerciseDefinitionService : IExerciseDefinitionService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;

    public ExerciseDefinitionService(
        IDbContextFactory<WorkoutDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<ExerciseDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var definitions = await db.ExerciseDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return definitions.Select(x => x.ToContract()).ToList();
    }

    public async Task<bool> ExistsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ExerciseDefinitions
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);
    }
}
