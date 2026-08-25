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

    public async Task<FreeWorkoutCompletionResult> CompleteFreeAsync(
        FreeWorkoutCompletion workout,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateFreeWorkout(workout);
        if (validation is not null)
            return validation;

        var userId = await currentUser.GetRequiredUserIdAsync();
        var now = timeProvider.GetLocalNow().DateTime;
        var workoutName = workout.SaveAsTemplate
            ? workout.TemplateName!.Trim()
            : "Свободная тренировка";

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (workout.SaveAsTemplate && await db.TrainingPlans.AnyAsync(
                x => x.UserId == userId && x.WorkoutName == workoutName,
                cancellationToken))
        {
            return new(
                false,
                FreeWorkoutCompletionFailure.TemplateNameConflict,
                "Шаблон с таким названием уже существует.",
                null,
                null);
        }

        var definitionIds = workout.Exercises
            .Select(x => x.ExerciseDefinitionId!.Value)
            .Distinct()
            .ToArray();
        var definitions = await db.ExerciseDefinitions
            .Include(x => x.SecondaryMuscles)
            .Where(x => definitionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (definitions.Count != definitionIds.Length)
        {
            return new(
                false,
                FreeWorkoutCompletionFailure.ExerciseDefinitionMissing,
                "Одно из выбранных упражнений больше не существует.",
                null,
                null);
        }

        var historySnapshot = new WorkoutHistoryDetails
        {
            Exercises = workout.Exercises
                .Select(ToFreeSnapshot)
                .ToList()
        };
        var historyEntity = new Models.WorkoutHistory
        {
            UserId = userId,
            WorkoutName = workoutName,
            Date = now,
            Summary = workout.SaveAsTemplate
                ? "Выполнено как свободная тренировка и сохранено в шаблоны."
                : "Свободная тренировка",
            Details = JsonSerializer.Serialize(historySnapshot)
        };

        Models.TrainingPlan? plan = null;
        List<DataExercise> templateExercises = [];

        await using var transaction = await db.Database.BeginTransactionAsync(
            cancellationToken);
        if (workout.SaveAsTemplate)
        {
            plan = new Models.TrainingPlan
            {
                UserId = userId,
                WorkoutName = workoutName,
                Date = now.Date
            };
            templateExercises = workout.Exercises
                .Select(x => ToTemplateExercise(
                    x,
                    plan,
                    userId,
                    workoutName,
                    definitions[x.ExerciseDefinitionId!.Value]))
                .ToList();
            plan.Exercises.AddRange(templateExercises);
            db.TrainingPlans.Add(plan);
            SaveFreeProgress(
                db,
                userId,
                workoutName,
                templateExercises,
                now);
        }

        db.WorkoutHistory.Add(historyEntity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(
            true,
            FreeWorkoutCompletionFailure.None,
            null,
            new WorkoutHistory
            {
                Id = historyEntity.Id,
                WorkoutName = historyEntity.WorkoutName,
                Date = historyEntity.Date,
                Summary = historyEntity.Summary,
                Details = historyEntity.Details
            },
            plan?.Id);
    }

    private static FreeWorkoutCompletionResult? ValidateFreeWorkout(
        FreeWorkoutCompletion workout)
    {
        if (workout.SaveAsTemplate &&
            (string.IsNullOrWhiteSpace(workout.TemplateName) ||
             workout.TemplateName.Trim().Length > 120))
        {
            return new(
                false,
                FreeWorkoutCompletionFailure.TemplateNameRequired,
                "Введите название шаблона длиной до 120 символов.",
                null,
                null);
        }

        var invalidExercise = workout.Exercises.Count == 0 ||
            workout.Exercises.Any(exercise =>
                string.IsNullOrWhiteSpace(exercise.Name) ||
                exercise.Name.Trim().Length > 160 ||
                exercise.ExerciseDefinitionId is null ||
                !Enum.IsDefined(exercise.Status) ||
                exercise.Sets.Count is < 1 or > 20 ||
                !exercise.Sets
                    .OrderBy(x => x.SetNumber)
                    .Select(x => x.SetNumber)
                    .SequenceEqual(Enumerable.Range(1, exercise.Sets.Count)) ||
                exercise.Sets.Any(set =>
                    set.Repetitions is < 1 or > 1000 ||
                    !double.IsFinite(set.Weight) ||
                    set.Weight is < 0 or > 2000));
        if (!invalidExercise)
            return null;

        return new(
            false,
            FreeWorkoutCompletionFailure.InvalidWorkout,
            "Заполните упражнение и проверьте параметры каждого подхода.",
            null,
            null);
    }

    private static WorkoutHistoryExercise ToFreeSnapshot(Exercise exercise) =>
        new()
        {
            Name = exercise.Name.Trim(),
            Status = exercise.Status,
            SupersetGroupId = exercise.SupersetGroupId,
            Sets = exercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => new WorkoutHistorySet
                {
                    SetNumber = x.SetNumber,
                    Weight = x.Weight,
                    Repetitions = x.Repetitions,
                    Completed = x.Completed,
                    IsWarmup = x.IsWarmup
                })
                .ToList()
        };

    private static DataExercise ToTemplateExercise(
        Exercise source,
        Models.TrainingPlan plan,
        string userId,
        string workoutName,
        Models.ExerciseDefinition definition) =>
        new()
        {
            UserId = userId,
            Name = source.Name.Trim(),
            WorkoutName = workoutName,
            SetsCount = source.Sets.Count,
            Status = Models.ExerciseStatus.NotCompleted,
            TrainingPlan = plan,
            ExerciseDefinitionId = definition.Id,
            ExerciseDefinition = definition,
            SupersetGroupId = source.SupersetGroupId,
            Sets = source.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => new Models.ExerciseTemplateSet
                {
                    SetNumber = x.SetNumber,
                    Repetitions = x.Repetitions,
                    Weight = x.Weight,
                    Completed = false,
                    IsWarmup = x.IsWarmup
                })
                .ToList()
        };

    private void SaveFreeProgress(
        WorkoutDbContext db,
        string userId,
        string workoutName,
        IReadOnlyCollection<DataExercise> exercises,
        DateTime now)
    {
        db.ProgressSnapshots.Add(new Models.ProgressSnapshot
        {
            UserId = userId,
            WorkoutName = workoutName,
            Date = now,
            Score = Math.Round(exercises.Sum(exerciseIndexService.Calculate), 2)
        });

        db.ExerciseProgressSnapshots.AddRange(exercises.Select(exercise =>
            new Models.ExerciseProgressSnapshot
            {
                UserId = userId,
                WorkoutName = workoutName,
                ExerciseName = exercise.Name,
                Date = now,
                Score = Math.Round(exerciseIndexService.Calculate(exercise), 2)
            }));
    }

    private WorkoutHistoryExercise ToSnapshot(DataExercise exercise)
    {
        return new WorkoutHistoryExercise
        {
            Name = exercise.Name,
            Status = (ExerciseStatus)exercise.Status,
            SupersetGroupId = exercise.SupersetGroupId,
            Sets = exercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => new WorkoutHistorySet
                {
                    SetNumber = x.SetNumber,
                    Weight = x.Weight,
                    Repetitions = x.Repetitions,
                    Completed = x.Completed,
                    IsWarmup = x.IsWarmup
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
