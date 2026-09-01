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
