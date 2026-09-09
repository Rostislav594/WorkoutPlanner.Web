using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public sealed class ActiveWorkoutService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    TimeProvider timeProvider)
    : IActiveWorkoutService
{
    public async Task<ActiveWorkout?> GetActiveWorkoutAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var today = timeProvider.GetLocalNow().Date;
        var tomorrow = today.AddDays(1);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await LoadActiveWorkoutAsync(
            db,
            userId,
            today,
            tomorrow,
            cancellationToken);
    }

    internal static async Task<ActiveWorkout?> LoadActiveWorkoutAsync(
        WorkoutDbContext db,
        string userId,
        DateTime today,
        DateTime tomorrow,
        CancellationToken cancellationToken)
    {
        var day = await db.WorkoutDays
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Date >= today &&
                x.Date < tomorrow &&
                !x.IsCompleted)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (day is null)
            return null;

        var plan = await db.TrainingPlans
            .AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .FirstOrDefaultAsync(
                x => x.Id == day.TrainingPlanId && x.UserId == userId,
                cancellationToken);
        if (plan is null)
            return null;

        var exercises = plan.Exercises
            .OrderBy(x => x.Id)
            .Select((exercise, index) => new ActiveWorkoutExercise(
                exercise.Id,
                exercise.Name,
                index,
                exercise.SupersetGroupId,
                exercise.Sets
                    .OrderBy(x => x.SetNumber)
                    .ThenBy(x => x.Id)
                    .Select(ToContract)
                    .ToList()))
            .ToList();

        return new ActiveWorkout(
            day.Id,
            plan.Id,
            plan.WorkoutName,
            day.Date,
            exercises);
    }

    public async Task<WorkoutSetUpdateResult> UpdateSetAsync(
        int setId,
        UpdateWorkoutSet command,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(command.Weight) ||
            command.Weight is < 0 or > 2000 ||
            command.Repetitions is < 1 or > 1000)
        {
            return new(false, WorkoutSetUpdateFailure.InvalidValues, null);
        }

        var userId = await currentUser.GetRequiredUserIdAsync();
        var today = timeProvider.GetLocalNow().Date;
        var tomorrow = today.AddDays(1);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var set = await FindActiveSetAsync(
            db,
            userId,
            today,
            tomorrow,
            setId,
            cancellationToken);
        if (set is null)
        {
            return new(
                false,
                WorkoutSetUpdateFailure.ActiveWorkoutOrSetNotFound,
                null);
        }

        if (set.Version != command.ExpectedVersion)
        {
            return new(
                false,
                WorkoutSetUpdateFailure.Conflict,
                ToContract(set));
        }

        set.Weight = command.Weight;
        set.Repetitions = command.Repetitions;
        set.Completed = command.Completed;
        set.Version++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await db.ExerciseTemplateSets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == setId, cancellationToken);
            return new(
                false,
                WorkoutSetUpdateFailure.Conflict,
                current is null ? null : ToContract(current));
        }

        return new(true, WorkoutSetUpdateFailure.None, ToContract(set));
    }

    internal static Task<Models.ExerciseTemplateSet?> FindActiveSetAsync(
        WorkoutDbContext db,
        string userId,
        DateTime today,
        DateTime tomorrow,
        int setId,
        CancellationToken cancellationToken) =>
        db.ExerciseTemplateSets
            .Include(x => x.Exercise)
            .FirstOrDefaultAsync(
                x => x.Id == setId &&
                     x.Exercise != null &&
                     x.Exercise.UserId == userId &&
                     db.WorkoutDays.Any(day =>
                         day.UserId == userId &&
                         day.TrainingPlanId == x.Exercise.TrainingPlanId &&
                         day.Date >= today &&
                         day.Date < tomorrow &&
                         !day.IsCompleted),
                cancellationToken);

    internal static Task<Models.ExerciseTemplateSet?> FindSetInPlanAsync(
        WorkoutDbContext db,
        string userId,
        int trainingPlanId,
        int setId,
        CancellationToken cancellationToken) =>
        db.ExerciseTemplateSets
            .Include(x => x.Exercise)
            .FirstOrDefaultAsync(
                x => x.Id == setId &&
                     x.Exercise != null &&
                     x.Exercise.UserId == userId &&
                     x.Exercise.TrainingPlanId == trainingPlanId,
                cancellationToken);

    internal static ActiveWorkoutSet ToContract(Models.ExerciseTemplateSet set) =>
        new(
            set.Id,
            set.SetNumber,
            set.Weight,
            set.Repetitions,
            set.Completed,
            set.IsWarmup,
            set.Version);
}
