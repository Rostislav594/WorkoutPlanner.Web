using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;
using DataExercise = WorkoutPlanner.Web.Models.Exercise;

namespace WorkoutPlanner.Web.Services;

public sealed class WorkoutCompletionService(
    IDbContextFactory<WorkoutDbContext> dbFactory,
    CurrentUserService currentUser,
    ExerciseIndexService exerciseIndexService,
    TimeProvider timeProvider)
    : IWorkoutCompletionService
{
    public async Task<WorkoutCompletionResult> CompleteTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var now = timeProvider.GetLocalNow().DateTime;
        var today = now.Date;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var day = await db.WorkoutDays
            .AsNoTracking()
            .FirstOrDefaultAsync(
            x => x.UserId == userId &&
                 x.Date.Date == today &&
                 !x.IsCompleted,
            cancellationToken);
        if (day is null)
        {
            return new(
                false,
                WorkoutCompletionFailure.NoScheduledWorkout,
                null);
        }

        var plan = await db.TrainingPlans
            .Include(x => x.Exercises)
                .ThenInclude(x => x.Sets)
            .Include(x => x.Exercises)
                .ThenInclude(x => x.ExerciseDefinition)
                    .ThenInclude(x => x!.SecondaryMuscles)
            .FirstOrDefaultAsync(
                x => x.Id == day.TrainingPlanId && x.UserId == userId,
                cancellationToken);
        if (plan is null)
        {
            return new(
                false,
                WorkoutCompletionFailure.NoScheduledWorkout,
                null);
        }

        if (plan.Exercises.Count == 0)
        {
            return new(false, WorkoutCompletionFailure.NoExercises, null);
        }

        if (plan.Exercises.Any(x =>
                x.Status == Models.ExerciseStatus.NotCompleted))
        {
            return new(
                false,
                WorkoutCompletionFailure.ExerciseStatusMissing,
                null);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        var claimedDay = await db.WorkoutDays
            .Where(x =>
                x.Id == day.Id &&
                x.UserId == userId &&
                !x.IsCompleted)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsCompleted, true),
                cancellationToken);
        if (claimedDay == 0)
        {
            return new(
                false,
                WorkoutCompletionFailure.NoScheduledWorkout,
                null);
        }

        var snapshot = new WorkoutHistoryDetails
        {
            Exercises = plan.Exercises
                .OrderBy(x => x.Id)
                .Select(ToSnapshot)
                .ToList()
        };
        var historyEntity = new Models.WorkoutHistory
        {
            UserId = userId,
            WorkoutName = plan.WorkoutName,
            Date = now,
            Summary = string.Empty,
            Details = JsonSerializer.Serialize(snapshot)
        };
        db.WorkoutHistory.Add(historyEntity);

        await SaveProgressAsync(
            db,
            userId,
            plan.WorkoutName,
            plan.Exercises,
            now,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(
            true,
            WorkoutCompletionFailure.None,
            new WorkoutHistory
            {
                Id = historyEntity.Id,
                WorkoutName = historyEntity.WorkoutName,
                Date = historyEntity.Date,
                Summary = historyEntity.Summary,
                Details = historyEntity.Details
            });
    }

    private WorkoutHistoryExercise ToSnapshot(DataExercise exercise)
    {
        return new WorkoutHistoryExercise
        {
            Name = exercise.Name,
            Status = (ExerciseStatus)exercise.Status,
            Sets = exercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => new WorkoutHistorySet
                {
                    SetNumber = x.SetNumber,
                    Weight = x.Weight,
                    Repetitions = x.Repetitions,
                    Completed = x.Completed
                })
                .ToList(),
            Photos = string.IsNullOrWhiteSpace(exercise.PhotoPath)
                ? []
                : [exercise.PhotoPath]
        };
    }

    private async Task SaveProgressAsync(
        WorkoutDbContext db,
        string userId,
        string workoutName,
        IReadOnlyCollection<DataExercise> exercises,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var today = now.Date;
        var score = Math.Round(exercises.Sum(exerciseIndexService.Calculate), 2);
        var workoutSnapshot = await db.ProgressSnapshots.FirstOrDefaultAsync(
            x => x.UserId == userId &&
                 x.WorkoutName == workoutName &&
                 x.Date.Date == today,
            cancellationToken);
        if (workoutSnapshot is null)
        {
            db.ProgressSnapshots.Add(new Models.ProgressSnapshot
            {
                UserId = userId,
                WorkoutName = workoutName,
                Date = now,
                Score = score
            });
        }
        else
        {
            workoutSnapshot.Date = now;
            workoutSnapshot.Score = score;
        }

        foreach (var exercise in exercises)
        {
            var exerciseScore = Math.Round(
                exerciseIndexService.Calculate(exercise),
                2);
            var exerciseSnapshot = await db.ExerciseProgressSnapshots
                .FirstOrDefaultAsync(
                    x => x.UserId == userId &&
                         x.WorkoutName == workoutName &&
                         x.ExerciseName == exercise.Name &&
                         x.Date.Date == today,
                    cancellationToken);
            if (exerciseSnapshot is null)
            {
                db.ExerciseProgressSnapshots.Add(
                    new Models.ExerciseProgressSnapshot
                    {
                        UserId = userId,
                        WorkoutName = workoutName,
                        ExerciseName = exercise.Name,
                        Date = now,
                        Score = exerciseScore
                    });
            }
            else
            {
                exerciseSnapshot.Date = now;
                exerciseSnapshot.Score = exerciseScore;
            }
        }
    }
}
