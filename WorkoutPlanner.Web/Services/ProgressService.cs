using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services.Auth;
using DataExercise = WorkoutPlanner.Web.Models.Exercise;
using WorkoutPlanner.Domain;

namespace WorkoutPlanner.Web.Services;

public sealed class ProgressService : IProgressService
{
    private const int VisibleChartDays = 30;
    private const int OverviewPointCount = 12;
    private const int OverviewExerciseLimit = 40;

    private readonly IDbContextFactory<WorkoutDbContext> _dbFactory;
    private readonly ExerciseIndexService _exerciseIndexService;
    private readonly CurrentUserService _currentUser;

    public ProgressService(IDbContextFactory<WorkoutDbContext> dbFactory, ExerciseIndexService exerciseIndexService, CurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _exerciseIndexService = exerciseIndexService;
        _currentUser = currentUser;
    }

    public async Task<double> CalculateWorkoutScoreAsync(string workoutName, CancellationToken cancellationToken = default)
    {
        var exercises = await GetWorkoutExercisesForProgressAsync(workoutName, cancellationToken);
        return Math.Round(exercises.Sum(_exerciseIndexService.Calculate), 2);
    }

    public async Task SaveProgressAsync(string workoutName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var exercises = await LoadWorkoutExercisesAsync(db, userId, workoutName, cancellationToken);
        var now = DateTime.Now;
        var today = DateTime.Today;
        var score = Math.Round(exercises.Sum(_exerciseIndexService.Calculate), 2);
        var workoutSnapshot = await db.ProgressSnapshots.FirstOrDefaultAsync(
            x => x.WorkoutName == workoutName && x.Date.Date == today && x.UserId == userId,
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
            var exerciseScore = Math.Round(_exerciseIndexService.Calculate(exercise), 2);
            var snapshot = await db.ExerciseProgressSnapshots.FirstOrDefaultAsync(
                x => x.WorkoutName == workoutName && x.ExerciseName == exercise.Name &&
                     x.Date.Date == today && x.UserId == userId,
                cancellationToken);
            if (snapshot is null)
            {
                db.ExerciseProgressSnapshots.Add(new Models.ExerciseProgressSnapshot
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
                snapshot.Date = now;
                snapshot.Score = exerciseScore;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ProgressSnapshot>> GetWorkoutProgressAsync(string workoutName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProgressSnapshots.AsNoTracking()
            .Where(x => x.WorkoutName == workoutName && x.UserId == userId)
            .OrderBy(x => x.Date)
            .Select(x => new ProgressSnapshot { Id = x.Id, Date = x.Date, WorkoutName = x.WorkoutName, Score = x.Score })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProgressChartPoint>> GetWorkoutChartAsync(string workoutName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ProgressSnapshots.AsNoTracking()
            .Where(x => x.WorkoutName == workoutName && x.UserId == userId);
        var baselineScore = await query
            .OrderBy(x => x.Date)
            .Select(x => (double?)x.Score)
            .FirstOrDefaultAsync(cancellationToken);
        if (baselineScore is null)
            return [];

        var visibleFrom = DateTime.Today.AddDays(-(VisibleChartDays - 1));
        var snapshots = await query
            .Where(x => x.Date >= visibleFrom)
            .OrderBy(x => x.Date)
            .Select(x => new ScorePoint(x.Date, x.Score))
            .ToListAsync(cancellationToken);
        return BuildChartPoints(snapshots, baselineScore.Value);
    }

    public async Task ClearAllProgressAsync(CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.ProgressSnapshots.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.ExerciseProgressSnapshots.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task ClearWorkoutProgressAsync(string workoutName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.ProgressSnapshots.Where(x => x.UserId == userId && x.WorkoutName == workoutName).ExecuteDeleteAsync(cancellationToken);
        await db.ExerciseProgressSnapshots.Where(x => x.UserId == userId && x.WorkoutName == workoutName).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task ClearExerciseProgressAsync(string workoutName, string exerciseName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.ExerciseProgressSnapshots
            .Where(x => x.UserId == userId && x.WorkoutName == workoutName && x.ExerciseName == exerciseName)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<List<ProgressChartPoint>> GetExerciseChartAsync(string workoutName, string exerciseName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ExerciseProgressSnapshots.AsNoTracking()
            .Where(x => x.UserId == userId && x.WorkoutName == workoutName && x.ExerciseName == exerciseName);
        var baselineScore = await query
            .OrderBy(x => x.Date)
            .Select(x => (double?)x.Score)
            .FirstOrDefaultAsync(cancellationToken);
        if (baselineScore is null)
            return [];

        var visibleFrom = DateTime.Today.AddDays(-(VisibleChartDays - 1));
        var snapshots = await query
            .Where(x => x.Date >= visibleFrom)
            .OrderBy(x => x.Date)
            .Select(x => new ScorePoint(x.Date, x.Score))
            .ToListAsync(cancellationToken);
        return BuildChartPoints(snapshots, baselineScore.Value);
    }

    public async Task<List<string>> GetWorkoutExercisesAsync(string workoutName, CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ExerciseProgressSnapshots.AsNoTracking()
            .Where(x => x.WorkoutName == workoutName && x.UserId == userId)
            .Select(x => x.ExerciseName).Distinct().OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProgressOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var exercises = await GetExerciseWeightTrendsAsync(db, userId, cancellationToken);
        var workouts = await GetWorkoutScoreChangesAsync(db, userId, cancellationToken);
        return new ProgressOverview(exercises, workouts);
    }

    private static async Task<List<ExerciseWeightTrend>> GetExerciseWeightTrendsAsync(
        WorkoutDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        // Очистка прогресса упражнения удаляет снимки, но не историю тренировок,
        // поэтому история учитывается только с самого раннего оставшегося снимка.
        var trackedSince = await db.ExerciseProgressSnapshots.AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => new { x.WorkoutName, x.ExerciseName })
            .Select(x => new { x.Key.WorkoutName, x.Key.ExerciseName, Since = x.Min(y => y.Date) })
            .ToListAsync(cancellationToken);
        if (trackedSince.Count == 0)
            return [];

        var sinceByExercise = trackedSince.ToDictionary(
            x => (x.WorkoutName, x.ExerciseName),
            x => x.Since.Date);
        var visibleFrom = DateTime.Today.AddDays(-(VisibleChartDays - 1));
        var histories = await db.WorkoutHistory.AsNoTracking()
            .Where(x => x.UserId == userId && x.Date >= visibleFrom)
            .OrderBy(x => x.Date)
            .Select(x => new { x.WorkoutName, x.Date, x.Details })
            .ToListAsync(cancellationToken);

        var weightsByExercise = new Dictionary<(string WorkoutName, string ExerciseName), List<double>>();
        foreach (var history in histories)
        {
            foreach (var exercise in ReadHistoryExercises(history.Details))
            {
                var key = (history.WorkoutName, exercise.Name);
                if (!sinceByExercise.TryGetValue(key, out var since) || history.Date.Date < since)
                    continue;

                var weight = GetWorkingWeight(exercise);
                if (weight is null)
                    continue;

                if (!weightsByExercise.TryGetValue(key, out var weights))
                    weightsByExercise[key] = weights = [];
                weights.Add(weight.Value);
            }
        }

        return weightsByExercise
            .Where(x => x.Value.Count >= 2)
            .Select(x => new ExerciseWeightTrend(
                x.Key.WorkoutName,
                x.Key.ExerciseName,
                x.Value[^1],
                Math.Round(x.Value[^1] - x.Value[0], 2),
                x.Value.TakeLast(OverviewPointCount).ToList()))
            .OrderByDescending(x => x.WeightChange)
            .ThenBy(x => x.ExerciseName, StringComparer.CurrentCultureIgnoreCase)
            .Take(OverviewExerciseLimit)
            .ToList();
    }

    private static async Task<List<WorkoutScoreChange>> GetWorkoutScoreChangesAsync(
        WorkoutDbContext db,
        string userId,
        CancellationToken cancellationToken)
    {
        var plans = await db.TrainingPlans.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsFreeDraft)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.WorkoutName })
            .ToListAsync(cancellationToken);
        var workoutNames = plans.Select(x => x.WorkoutName).ToList();
        var snapshots = await db.ProgressSnapshots.AsNoTracking()
            .Where(x => x.UserId == userId && workoutNames.Contains(x.WorkoutName))
            .OrderBy(x => x.Date)
            .Select(x => new { x.WorkoutName, x.Score })
            .ToListAsync(cancellationToken);
        var scoresByWorkout = snapshots
            .GroupBy(x => x.WorkoutName)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.Score).TakeLast(OverviewPointCount).ToList());

        return plans
            .DistinctBy(x => x.WorkoutName)
            .Where(x => scoresByWorkout.TryGetValue(x.WorkoutName, out var scores) && scores.Count >= 2)
            .Select(x =>
            {
                var scores = scoresByWorkout[x.WorkoutName];
                return new WorkoutScoreChange(
                    x.Id,
                    x.WorkoutName,
                    TrainingMetrics.CalculatePercentageChange(scores[^2], scores[^1]),
                    scores);
            })
            .OrderByDescending(x => x.ChangePercent)
            .ThenBy(x => x.WorkoutName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<Models.WorkoutHistoryExercise> ReadHistoryExercises(string details)
    {
        try
        {
            return JsonSerializer.Deserialize<Models.WorkoutHistoryDetails>(details)?.Exercises ?? [];
        }
        catch (JsonException)
        {
            // Одна повреждённая запись истории не должна прятать прогресс остальных.
            return [];
        }
    }

    // Самый тяжёлый рабочий подход; если отмечены выполненные, берём только их.
    private static double? GetWorkingWeight(Models.WorkoutHistoryExercise exercise)
    {
        var workingSets = exercise.Sets.Where(x => !x.IsWarmup && x.Weight > 0).ToList();
        if (workingSets.Count == 0)
            return null;

        var completedSets = workingSets.Where(x => x.Completed).ToList();
        return (completedSets.Count > 0 ? completedSets : workingSets).Max(x => x.Weight);
    }

    private async Task<List<DataExercise>> GetWorkoutExercisesForProgressAsync(string workoutName, CancellationToken cancellationToken)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await LoadWorkoutExercisesAsync(db, userId, workoutName, cancellationToken);
    }

    private static Task<List<DataExercise>> LoadWorkoutExercisesAsync(WorkoutDbContext db, string userId, string workoutName, CancellationToken cancellationToken) =>
        db.Exercises.AsNoTracking()
            .Include(x => x.ExerciseDefinition).ThenInclude(x => x!.SecondaryMuscles)
            .Include(x => x.Sets)
            .Where(x => x.WorkoutName == workoutName && x.UserId == userId)
            .ToListAsync(cancellationToken);

    private static List<ProgressChartPoint> BuildChartPoints(
        IReadOnlyList<ScorePoint> snapshots,
        double baselineScore)
    {
        if (snapshots.Count == 0)
            return [];

        return snapshots.Select(snapshot => new ProgressChartPoint
        {
            Date = snapshot.Date,
            Percent = TrainingMetrics.CalculatePercentageChange(
                baselineScore,
                snapshot.Score)
        }).ToList();
    }

    private sealed record ScorePoint(DateTime Date, double Score);
}
