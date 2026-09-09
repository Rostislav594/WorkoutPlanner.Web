using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services.WearOs;

public sealed class WatchWorkoutService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    TimeProvider timeProvider,
    ILogger<WatchWorkoutService> logger)
    : IWatchWorkoutService
{
    private const string CompleteSetOperationType = "CompleteSet";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<WatchActiveWorkoutResult> GetActiveWorkoutAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var today = timeProvider.GetLocalNow().Date;
        var tomorrow = today.AddDays(1);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var workout = await ActiveWorkoutService.LoadActiveWorkoutAsync(
            db,
            userId,
            today,
            tomorrow,
            cancellationToken);
        if (workout is not null)
            return new(WatchWorkoutAvailability.Active, workout);

        var finished = await db.WorkoutDays.AsNoTracking().AnyAsync(
            x => x.UserId == userId &&
                 x.Date >= today &&
                 x.Date < tomorrow &&
                 x.IsCompleted,
            cancellationToken);
        return new(
            finished ? WatchWorkoutAvailability.Finished : WatchWorkoutAvailability.None,
            null);
    }

    public async Task<CompleteWatchSetResult> CompleteSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = timeProvider.GetLocalNow().Date;
        var tomorrow = today.AddDays(1);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var deviceIsActive = await db.WatchDevices.AsNoTracking().AnyAsync(
            x => x.Id == watchDeviceId &&
                 x.UserId == userId &&
                 x.RevokedAtUtc == null &&
                 x.RefreshTokenExpiresAtUtc > now,
            cancellationToken);
        if (!deviceIsActive)
            return Failure(CompleteWatchSetFailure.ActiveWorkoutOrSetNotFound);

        var replay = await db.WatchSyncOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
        if (replay is not null)
            return ReadReplay(replay, watchDeviceId, setId);

        var activeWorkout = await ActiveWorkoutService.LoadActiveWorkoutAsync(
            db,
            userId,
            today,
            tomorrow,
            cancellationToken);
        if (activeWorkout is null)
        {
            var belongsToFinishedWorkout = await db.ExerciseTemplateSets
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == setId &&
                         x.Exercise != null &&
                         x.Exercise.UserId == userId &&
                         db.WorkoutDays.Any(day =>
                             day.UserId == userId &&
                             day.TrainingPlanId == x.Exercise.TrainingPlanId &&
                             day.Date >= today &&
                             day.Date < tomorrow &&
                             day.IsCompleted),
                    cancellationToken);
            return Failure(
                belongsToFinishedWorkout
                    ? CompleteWatchSetFailure.WorkoutFinished
                    : CompleteWatchSetFailure.ActiveWorkoutOrSetNotFound);
        }

        var set = await ActiveWorkoutService.FindSetInPlanAsync(
            db,
            userId,
            activeWorkout.TrainingPlanId,
            setId,
            cancellationToken);
        if (set is null)
            return Failure(CompleteWatchSetFailure.ActiveWorkoutOrSetNotFound);

        if (set.Version != expectedVersion || set.Completed)
        {
            var current = FindCurrent(activeWorkout);
            return new(
                false,
                CompleteWatchSetFailure.Conflict,
                ActiveWorkoutService.ToContract(set),
                current.ExerciseId,
                current.SetId,
                null);
        }

        set.Completed = true;
        set.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var workout = await ActiveWorkoutService.LoadActiveWorkoutAsync(
                db,
                userId,
                today,
                tomorrow,
                cancellationToken);
            var current = FindCurrent(workout);
            var result = new CompleteWatchSetResult(
                true,
                CompleteWatchSetFailure.None,
                ActiveWorkoutService.ToContract(set),
                current.ExerciseId,
                current.SetId,
                now);
            db.WatchSyncOperations.Add(new WatchSyncOperation
            {
                OperationId = operationId,
                WatchDeviceId = watchDeviceId,
                OperationType = CompleteSetOperationType,
                EntityId = setId,
                ReceivedAtUtc = now,
                ExpiresAtUtc = now.Add(WatchSyncOperation.RetentionPeriod),
                ResultJson = JsonSerializer.Serialize(
                    new StoredCompleteSetResult(
                        result.Set!,
                        result.CurrentExerciseId,
                        result.CurrentSetId,
                        now),
                    JsonOptions)
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Watch device {WatchDeviceId} completed set {SetId} with operation {OperationId}; client timestamp {ChangedAtUtc}.",
                watchDeviceId,
                setId,
                operationId,
                changedAtUtc);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var replayAfterRace = await TryReadReplayAfterRaceAsync(
                operationId,
                watchDeviceId,
                setId,
                cancellationToken);
            if (replayAfterRace is not null)
                return replayAfterRace;

            await using var conflictDb = await dbFactory.CreateDbContextAsync(cancellationToken);
            var currentSet = await conflictDb.ExerciseTemplateSets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == setId, cancellationToken);
            var workout = await ActiveWorkoutService.LoadActiveWorkoutAsync(
                conflictDb,
                userId,
                today,
                tomorrow,
                cancellationToken);
            var current = FindCurrent(workout);
            return new(
                false,
                CompleteWatchSetFailure.Conflict,
                currentSet is null ? null : ActiveWorkoutService.ToContract(currentSet),
                current.ExerciseId,
                current.SetId,
                null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var replayAfterRace = await TryReadReplayAfterRaceAsync(
                operationId,
                watchDeviceId,
                setId,
                cancellationToken);
            if (replayAfterRace is not null)
                return replayAfterRace;
            throw;
        }
    }

    private async Task<CompleteWatchSetResult?> TryReadReplayAfterRaceAsync(
        Guid operationId,
        Guid watchDeviceId,
        int setId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.WatchSyncOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
        return operation is null ? null : ReadReplay(operation, watchDeviceId, setId);
    }

    private static CompleteWatchSetResult ReadReplay(
        WatchSyncOperation operation,
        Guid watchDeviceId,
        int setId)
    {
        if (operation.WatchDeviceId != watchDeviceId ||
            operation.EntityId != setId ||
            operation.OperationType != CompleteSetOperationType)
            return Failure(CompleteWatchSetFailure.OperationIdConflict);

        try
        {
            var stored = JsonSerializer.Deserialize<StoredCompleteSetResult>(
                operation.ResultJson,
                JsonOptions);
            return stored is null
                ? Failure(CompleteWatchSetFailure.OperationIdConflict)
                : new(
                    true,
                    CompleteWatchSetFailure.None,
                    stored.Set,
                    stored.CurrentExerciseId,
                    stored.CurrentSetId,
                    stored.ProcessedAtUtc);
        }
        catch (JsonException)
        {
            return Failure(CompleteWatchSetFailure.OperationIdConflict);
        }
    }

    private static (int? ExerciseId, int? SetId) FindCurrent(ActiveWorkout? workout)
    {
        if (workout is null)
            return (null, null);

        foreach (var exercise in workout.Exercises)
        {
            var set = exercise.Sets.FirstOrDefault(x => !x.Completed);
            if (set is not null)
                return (exercise.ExerciseId, set.SetId);
        }

        return (null, null);
    }

    private static CompleteWatchSetResult Failure(CompleteWatchSetFailure failure) =>
        new(false, failure, null, null, null, null);

    private sealed record StoredCompleteSetResult(
        ActiveWorkoutSet Set,
        int? CurrentExerciseId,
        int? CurrentSetId,
        DateTime ProcessedAtUtc);
}
