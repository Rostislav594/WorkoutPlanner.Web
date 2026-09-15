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
    IWorkoutCompletionService workoutCompletion,
    IWorkoutRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider,
    ILogger<WatchWorkoutService> logger)
    : IWatchWorkoutService
{
    private const string CompleteSetOperationType = "CompleteSet";
    private const string UpdateSetOperationType = "UpdateSet";
    private const string UndoSetOperationType = "UndoSet";
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
        {
            // Длительности отдыха живут в профиле и настраиваются в приложении.
            // Часы получают их вместе с тренировкой, чтобы таймер на запястье
            // совпадал с тем, что человек выставил на телефоне.
            var rest = await db.UserProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new
                {
                    x.RestBetweenSetsSeconds,
                    x.RestBetweenExercisesSeconds,
                })
                .FirstOrDefaultAsync(cancellationToken);
            return new(
                WatchWorkoutAvailability.Active,
                workout,
                rest?.RestBetweenSetsSeconds ?? WatchRestDefaults.BetweenSetsSeconds,
                rest?.RestBetweenExercisesSeconds ?? WatchRestDefaults.BetweenExercisesSeconds);
        }

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

    public Task<CompleteWatchSetResult> CompleteSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default) =>
        MutateSetAsync(
            watchDeviceId,
            setId,
            operationId,
            CompleteSetOperationType,
            expectedVersion,
            changedAtUtc,
            static set => set.Completed,
            static set => set.Completed = true,
            static _ => true,
            cancellationToken);

    public Task<CompleteWatchSetResult> UpdateSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        double weight,
        int repetitions,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(weight) ||
            weight is < 0 or > 2000 ||
            repetitions is < 1 or > 1000)
        {
            return Task.FromResult(Failure(CompleteWatchSetFailure.InvalidValues));
        }

        return MutateSetAsync(
            watchDeviceId,
            setId,
            operationId,
            UpdateSetOperationType,
            expectedVersion,
            changedAtUtc,
            static _ => false,
            set =>
            {
                set.Weight = weight;
                set.Repetitions = repetitions;
            },
            storedSet =>
                storedSet.Weight.Equals(weight) &&
                storedSet.Repetitions == repetitions,
            cancellationToken);
    }

    public Task<CompleteWatchSetResult> UndoSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        long expectedVersion,
        DateTime changedAtUtc,
        CancellationToken cancellationToken = default) =>
        MutateSetAsync(
            watchDeviceId,
            setId,
            operationId,
            UndoSetOperationType,
            expectedVersion,
            changedAtUtc,
            static set => !set.Completed,
            static set => set.Completed = false,
            static _ => true,
            cancellationToken);

    /// <summary>
    /// Завершение свободной тренировки, начатой на телефоне.
    ///
    /// Правило то же, что и у запланированной: незакрытые подходы завершать
    /// нельзя. Повторный вызов после успешного завершения уже не найдёт
    /// черновик — он удаляется вместе с записью в историю, — поэтому такой
    /// запрос честно отвечает «тренировки нет», а не молча делает вид, что всё
    /// прошло.
    /// </summary>
    private async Task<FinishWatchWorkoutResult> FinishFreeDraftAsync(
        WorkoutDbContext db,
        string userId,
        Guid watchDeviceId,
        int planId,
        CancellationToken cancellationToken)
    {
        var draft = await db.TrainingPlans.AsNoTracking()
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .SingleOrDefaultAsync(
                x => x.Id == planId && x.UserId == userId && x.IsFreeDraft,
                cancellationToken);
        if (draft is null)
            return FinishFailure(FinishWatchWorkoutFailure.ActiveWorkoutNotFound);

        if (draft.Exercises.Count == 0 ||
            draft.Exercises.SelectMany(x => x.Sets).Any(x => !x.Completed))
        {
            return FinishFailure(FinishWatchWorkoutFailure.WorkoutNotReady);
        }

        var completion = await workoutCompletion.CompleteFreeDraftAsync(
            planId,
            cancellationToken);
        if (!completion.Succeeded)
            return FinishFailure(FinishWatchWorkoutFailure.ActiveWorkoutNotFound);

        logger.LogInformation(
            "Watch device {WatchDeviceId} finished a free workout draft {PlanId}.",
            watchDeviceId,
            planId);
        await realtimeNotifier.PublishWorkoutFinishedAsync(
            userId,
            new WorkoutFinishedNotification(FreeWorkoutDraftId.FromPlanId(planId)));
        return new(true, FinishWatchWorkoutFailure.None, AlreadyFinished: false);
    }

    public async Task<FinishWatchWorkoutResult> FinishWorkoutAsync(
        Guid watchDeviceId,
        int workoutId,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = timeProvider.GetLocalNow().Date;
        var tomorrow = today.AddDays(1);

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var deviceIsActive = await db.WatchDevices.AsNoTracking().AnyAsync(
                x => x.Id == watchDeviceId &&
                     x.UserId == userId &&
                     x.RevokedAtUtc == null &&
                     x.RefreshTokenExpiresAtUtc > now,
                cancellationToken);
            if (!deviceIsActive)
                return FinishFailure(FinishWatchWorkoutFailure.ActiveWorkoutNotFound);

            // Свободная тренировка живёт на сервере черновиком плана, а не днём
            // календаря, поэтому у неё другой путь завершения.
            if (FreeWorkoutDraftId.IsDraft(workoutId))
            {
                return await FinishFreeDraftAsync(
                    db,
                    userId,
                    watchDeviceId,
                    FreeWorkoutDraftId.ToPlanId(workoutId),
                    cancellationToken);
            }

            var day = await db.WorkoutDays.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == workoutId &&
                         x.UserId == userId &&
                         x.Date >= today &&
                         x.Date < tomorrow,
                    cancellationToken);
            if (day is null)
                return FinishFailure(FinishWatchWorkoutFailure.ActiveWorkoutNotFound);
            if (day.IsCompleted)
                return new(true, FinishWatchWorkoutFailure.None, AlreadyFinished: true);

            var plan = await db.TrainingPlans.AsNoTracking()
                .Include(x => x.Exercises)
                    .ThenInclude(x => x.Sets)
                .SingleOrDefaultAsync(
                    x => x.Id == day.TrainingPlanId && x.UserId == userId,
                    cancellationToken);
            if (plan is null)
                return FinishFailure(FinishWatchWorkoutFailure.ActiveWorkoutNotFound);
            if (plan.Exercises.Count == 0 ||
                plan.Exercises.SelectMany(x => x.Sets).Any(x => !x.Completed))
            {
                return FinishFailure(FinishWatchWorkoutFailure.WorkoutNotReady);
            }
        }

        var completion = await workoutCompletion.CompleteTodayAsync(
            workoutId,
            cancellationToken);
        if (!completion.Succeeded)
        {
            await using var verificationDb = await dbFactory.CreateDbContextAsync(cancellationToken);
            var completedAfterRace = await verificationDb.WorkoutDays.AsNoTracking().AnyAsync(
                x => x.Id == workoutId && x.UserId == userId && x.IsCompleted,
                cancellationToken);
            return completedAfterRace
                ? new(true, FinishWatchWorkoutFailure.None, AlreadyFinished: true)
                : FinishFailure(FinishWatchWorkoutFailure.ActiveWorkoutNotFound);
        }

        logger.LogInformation(
            "Watch device {WatchDeviceId} finished workout {WorkoutId}.",
            watchDeviceId,
            workoutId);
        await realtimeNotifier.PublishWorkoutFinishedAsync(
            userId,
            new WorkoutFinishedNotification(workoutId));
        return new(true, FinishWatchWorkoutFailure.None, AlreadyFinished: false);
    }

    private async Task<CompleteWatchSetResult> MutateSetAsync(
        Guid watchDeviceId,
        int setId,
        Guid operationId,
        string operationType,
        long expectedVersion,
        DateTime changedAtUtc,
        Func<Models.ExerciseTemplateSet, bool> hasSemanticConflict,
        Action<Models.ExerciseTemplateSet> applyMutation,
        Func<ActiveWorkoutSet, bool> isReplayEquivalent,
        CancellationToken cancellationToken)
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
            return ReadReplay(
                replay,
                watchDeviceId,
                setId,
                operationType,
                isReplayEquivalent);

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

        if (set.Version != expectedVersion || hasSemanticConflict(set))
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

        applyMutation(set);
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
                OperationType = operationType,
                EntityId = setId,
                ReceivedAtUtc = now,
                ExpiresAtUtc = now.Add(WatchSyncOperation.RetentionPeriod),
                ResultJson = JsonSerializer.Serialize(
                    new StoredSetMutationResult(
                        result.Set!,
                        result.CurrentExerciseId,
                        result.CurrentSetId,
                        now),
                    JsonOptions)
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Watch device {WatchDeviceId} applied {OperationType} to set {SetId} with operation {OperationId}; client timestamp {ChangedAtUtc}.",
                watchDeviceId,
                operationType,
                setId,
                operationId,
                changedAtUtc);
            await realtimeNotifier.PublishSetUpdatedAsync(
                userId,
                new WorkoutSetUpdatedNotification(activeWorkout.WorkoutId, result.Set!));
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var replayAfterRace = await TryReadReplayAfterRaceAsync(
                operationId,
                watchDeviceId,
                setId,
                operationType,
                isReplayEquivalent,
                cancellationToken);
            if (replayAfterRace is not null)
                return replayAfterRace;

            await using var conflictDb = await dbFactory.CreateDbContextAsync(cancellationToken);
            var currentSet = await conflictDb.ExerciseTemplateSets.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == setId &&
                         x.Exercise != null &&
                         x.Exercise.UserId == userId &&
                         x.Exercise.TrainingPlanId == activeWorkout.TrainingPlanId,
                    cancellationToken);
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
                operationType,
                isReplayEquivalent,
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
        string operationType,
        Func<ActiveWorkoutSet, bool> isReplayEquivalent,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.WatchSyncOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);
        return operation is null
            ? null
            : ReadReplay(
                operation,
                watchDeviceId,
                setId,
                operationType,
                isReplayEquivalent);
    }

    private static CompleteWatchSetResult ReadReplay(
        WatchSyncOperation operation,
        Guid watchDeviceId,
        int setId,
        string operationType,
        Func<ActiveWorkoutSet, bool> isReplayEquivalent)
    {
        if (operation.WatchDeviceId != watchDeviceId ||
            operation.EntityId != setId ||
            operation.OperationType != operationType)
            return Failure(CompleteWatchSetFailure.OperationIdConflict);

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSetMutationResult>(
                operation.ResultJson,
                JsonOptions);
            return stored is null || !isReplayEquivalent(stored.Set)
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

    private static FinishWatchWorkoutResult FinishFailure(
        FinishWatchWorkoutFailure failure) =>
        new(false, failure, AlreadyFinished: false);

    private sealed record StoredSetMutationResult(
        ActiveWorkoutSet Set,
        int? CurrentExerciseId,
        int? CurrentSetId,
        DateTime ProcessedAtUtc);
}
