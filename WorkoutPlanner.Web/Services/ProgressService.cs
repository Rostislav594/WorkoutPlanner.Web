using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Services;

public class ProgressService
{
    private readonly ExerciseIndexService _exerciseIndexService;
    private readonly WorkoutDbContext _db;
    private readonly CurrentUserService _currentUser;

    public ProgressService(
        WorkoutDbContext db,
        ExerciseIndexService exerciseIndexService,
        CurrentUserService currentUser)
    {
        _db = db;
        _exerciseIndexService = exerciseIndexService;
        _currentUser = currentUser;
    }

    public async Task<double> CalculateWorkoutScoreAsync(
        string workoutName)
    {
        var exercises = await GetWorkoutExercisesForProgressAsync(workoutName);

        var score = exercises.Sum(_exerciseIndexService.Calculate);

        return Math.Round(score, 2);
    }

    public async Task SaveProgressAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();
        var now = DateTime.Now;
        var today = DateTime.Today;
        var exercises = await GetWorkoutExercisesForProgressAsync(workoutName);
        var score = Math.Round(
            exercises.Sum(_exerciseIndexService.Calculate),
            2);

        var workoutSnapshot = await _db.ProgressSnapshots
            .FirstOrDefaultAsync(x =>
                x.WorkoutName == workoutName &&
                x.Date.Date == today &&
                x.UserId == userId);

        if (workoutSnapshot == null)
        {
            _db.ProgressSnapshots.Add(
                new ProgressSnapshot
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
                _exerciseIndexService.Calculate(exercise),
                2);

            var exerciseSnapshot = await _db.ExerciseProgressSnapshots
                .FirstOrDefaultAsync(x =>
                    x.WorkoutName == workoutName &&
                    x.ExerciseName == exercise.Name &&
                    x.Date.Date == today &&
                    x.UserId == userId);

            if (exerciseSnapshot == null)
            {
                _db.ExerciseProgressSnapshots.Add(
                    new ExerciseProgressSnapshot
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

        await _db.SaveChangesAsync();
    }

    public async Task<List<ProgressSnapshot>> GetWorkoutProgressAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var snapshots = await _db.ProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.UserId == userId)
            .OrderBy(x => x.Date)
            .ToListAsync();

        return snapshots;
    }

    public async Task<ProgressSnapshot?> GetLastProgressAsync(
        string workoutName)
    {
        return (await GetWorkoutProgressAsync(workoutName))
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();
    }

    public async Task<List<ProgressChartPoint>> GetWorkoutChartAsync(
        string workoutName)
    {
        var snapshots = await GetWorkoutProgressAsync(workoutName);

        return BuildChartPoints(snapshots);
    }

    public async Task ClearAllProgressAsync()
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var workoutSnapshots = await _db.ProgressSnapshots
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var exerciseSnapshots = await _db.ExerciseProgressSnapshots
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _db.ProgressSnapshots.RemoveRange(workoutSnapshots);
        _db.ExerciseProgressSnapshots.RemoveRange(exerciseSnapshots);

        await _db.SaveChangesAsync();
    }

    public async Task ClearWorkoutProgressAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var workoutSnapshots = await _db.ProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.UserId == userId)
            .ToListAsync();

        var exerciseSnapshots = await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.UserId == userId)
            .ToListAsync();

        if (!workoutSnapshots.Any() && !exerciseSnapshots.Any())
            return;

        _db.ProgressSnapshots.RemoveRange(workoutSnapshots);
        _db.ExerciseProgressSnapshots.RemoveRange(exerciseSnapshots);

        await _db.SaveChangesAsync();
    }

    public async Task ClearExerciseProgressAsync(
        string workoutName,
        string exerciseName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var snapshots = await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.ExerciseName == exerciseName &&
                x.UserId == userId)
            .ToListAsync();

        _db.ExerciseProgressSnapshots.RemoveRange(snapshots);

        await _db.SaveChangesAsync();
    }

    public async Task<List<ExerciseProgressSnapshot>> GetExerciseProgressAsync(
        string workoutName,
        string exerciseName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var snapshots = await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.ExerciseName == exerciseName &&
                x.UserId == userId)
            .OrderBy(x => x.Date)
            .ToListAsync();

        return snapshots;
    }

    public async Task<List<ProgressChartPoint>> GetExerciseChartAsync(
        string workoutName,
        string exerciseName)
    {
        var snapshots = await GetExerciseProgressAsync(
            workoutName,
            exerciseName);

        return BuildChartPoints(snapshots);
    }

    public async Task<List<string>> GetWorkoutExercisesAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        return await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.UserId == userId)
            .Select(x => x.ExerciseName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    private async Task<List<Exercise>> GetWorkoutExercisesForProgressAsync(
        string workoutName)
    {
        var userId = await _currentUser.GetRequiredUserIdAsync();

        var exercises = await _db.Exercises
            .Include(x => x.ExerciseDefinition)
            .ThenInclude(x => x!.SecondaryMuscles)
            .ThenInclude(x => x.Muscle)
            .Include(x => x.Sets)
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.UserId == userId)
            .ToListAsync();

        return exercises;
    }

    private static List<ProgressChartPoint> BuildChartPoints<TSnapshot>(
        IReadOnlyList<TSnapshot> snapshots)
        where TSnapshot : class
    {
        if (!snapshots.Any())
            return new();

        var firstScore = GetScore(snapshots[0]);
        var result = new List<ProgressChartPoint>();

        foreach (var snapshot in snapshots)
        {
            var score = GetScore(snapshot);
            var percent = firstScore <= 0
                ? 0
                : ((score - firstScore) / firstScore) * 100;

            result.Add(
                new ProgressChartPoint
                {
                    Label = GetDate(snapshot).ToString("dd.MM"),
                    Percent = Math.Round((decimal)percent, 2)
                });
        }

        return result;
    }

    private static double GetScore<TSnapshot>(TSnapshot snapshot)
        where TSnapshot : class
    {
        return snapshot switch
        {
            ProgressSnapshot workoutSnapshot => workoutSnapshot.Score,
            ExerciseProgressSnapshot exerciseSnapshot => exerciseSnapshot.Score,
            _ => 0
        };
    }

    private static DateTime GetDate<TSnapshot>(TSnapshot snapshot)
        where TSnapshot : class
    {
        return snapshot switch
        {
            ProgressSnapshot workoutSnapshot => workoutSnapshot.Date,
            ExerciseProgressSnapshot exerciseSnapshot => exerciseSnapshot.Date,
            _ => DateTime.MinValue
        };
    }
}
