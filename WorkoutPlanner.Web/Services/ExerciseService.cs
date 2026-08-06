using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Application.Mapping;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class ExerciseService : IExerciseService
{
    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly CurrentUserService _currentUser;

    public ExerciseService(
        IDbContextFactory<WorkoutDbContext> dbFactory,
        CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<List<Exercise>> GetExercisesAsync(
        string workoutName,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var exercises = await db.Exercises
            .AsNoTracking()
            .Include(x => x.ExerciseDefinition)
            .Include(x => x.Sets)
            .Where(x => x.WorkoutName == workoutName && x.UserId == userId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return exercises.Select(x => x.ToContract()).ToList();
    }

    public async Task AddExerciseAsync(
        Exercise exercise,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var plan = await db.TrainingPlans.FirstOrDefaultAsync(
            x => x.Id == exercise.TrainingPlanId && x.UserId == userId,
            cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException(
                "The selected training plan does not belong to the current user.");
        }

        var entity = new Models.Exercise
        {
            UserId = userId,
            Name = exercise.Name.Trim(),
            WorkoutName = plan.WorkoutName,
            SetsCount = exercise.SetsCount,
            Status = (Models.ExerciseStatus)exercise.Status,
            TrainingPlanId = plan.Id,
            ExerciseDefinitionId = exercise.ExerciseDefinitionId,
            PhotoPath = exercise.PhotoPath,
            Sets = exercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => new Models.ExerciseTemplateSet
                {
                    SetNumber = x.SetNumber,
                    Repetitions = x.Repetitions,
                    Weight = x.Weight,
                    Completed = x.Completed
                })
                .ToList()
        };

        db.Exercises.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        exercise.Id = entity.Id;
        exercise.Sets = entity.Sets.Select(x => x.ToContract()).ToList();
    }

    public async Task DeleteExerciseAsync(
        int exerciseId,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var exercise = await db.Exercises.FirstOrDefaultAsync(
            x => x.Id == exerciseId && x.UserId == userId,
            cancellationToken);

        if (exercise is null)
            return;

        db.Exercises.Remove(exercise);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateExerciseAsync(
        Exercise exercise,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Exercises
            .Include(x => x.Sets)
            .FirstOrDefaultAsync(
                x => x.Id == exercise.Id && x.UserId == userId,
                cancellationToken);

        if (entity is null)
            throw new InvalidOperationException("Exercise was not found.");

        entity.Name = exercise.Name.Trim();
        entity.SetsCount = exercise.SetsCount;
        entity.Status = (Models.ExerciseStatus)exercise.Status;
        entity.ExerciseDefinitionId = exercise.ExerciseDefinitionId;
        entity.PhotoPath = exercise.PhotoPath;

        var retainedSetIds = new HashSet<int>();

        foreach (var sourceSet in exercise.Sets.OrderBy(x => x.SetNumber))
        {
            var targetSet = sourceSet.Id > 0
                ? entity.Sets.FirstOrDefault(x => x.Id == sourceSet.Id)
                : entity.Sets.FirstOrDefault(x =>
                    x.SetNumber == sourceSet.SetNumber &&
                    !retainedSetIds.Contains(x.Id));

            if (targetSet is null)
            {
                targetSet = new Models.ExerciseTemplateSet();
                entity.Sets.Add(targetSet);
            }

            targetSet.SetNumber = sourceSet.SetNumber;
            targetSet.Repetitions = sourceSet.Repetitions;
            targetSet.Weight = sourceSet.Weight;
            targetSet.Completed = sourceSet.Completed;

            if (targetSet.Id > 0)
                retainedSetIds.Add(targetSet.Id);
        }

        var removedSets = entity.Sets
            .Where(x => x.Id > 0 && !retainedSetIds.Contains(x.Id))
            .ToList();
        db.ExerciseTemplateSets.RemoveRange(removedSets);

        await db.SaveChangesAsync(cancellationToken);
        exercise.Sets = entity.Sets
            .Except(removedSets)
            .OrderBy(x => x.SetNumber)
            .Select(x => x.ToContract())
            .ToList();
    }
}
