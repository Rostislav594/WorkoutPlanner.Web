using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public class ProgressService
{
    private readonly ExerciseIndexService _exerciseIndexService;
    private readonly WorkoutDbContext _db;

    public ProgressService(
        WorkoutDbContext db,
        ExerciseIndexService exerciseIndexService)
    {
        _db = db;
        _exerciseIndexService = exerciseIndexService;
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
        var now = DateTime.Now;
        var today = DateTime.Today;
        var exercises = await GetWorkoutExercisesForProgressAsync(workoutName);
        var score = Math.Round(
            exercises.Sum(_exerciseIndexService.Calculate),
            2);

        var workoutSnapshot = await _db.ProgressSnapshots
            .FirstOrDefaultAsync(x =>
                x.WorkoutName == workoutName &&
                x.Date.Date == today);

        if (workoutSnapshot == null)
        {
            _db.ProgressSnapshots.Add(
                new ProgressSnapshot
                {
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
                    x.Date.Date == today);

            if (exerciseSnapshot == null)
            {
                _db.ExerciseProgressSnapshots.Add(
                    new ExerciseProgressSnapshot
                    {
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
        return await _db.ProgressSnapshots
            .Where(x => x.WorkoutName == workoutName)
            .OrderBy(x => x.Date)
            .ToListAsync();
    }

    public async Task<ProgressSnapshot?> GetLastProgressAsync(
        string workoutName)
    {
        return await _db.ProgressSnapshots
            .Where(x => x.WorkoutName == workoutName)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ProgressChartPoint>> GetWorkoutChartAsync(
        string workoutName)
    {
        var snapshots = await GetWorkoutProgressAsync(workoutName);

        return BuildChartPoints(snapshots);
    }

    public async Task ClearAllProgressAsync()
    {
        var workoutSnapshots = await _db.ProgressSnapshots
            .ToListAsync();

        var exerciseSnapshots = await _db.ExerciseProgressSnapshots
            .ToListAsync();

        _db.ProgressSnapshots.RemoveRange(workoutSnapshots);
        _db.ExerciseProgressSnapshots.RemoveRange(exerciseSnapshots);

        await _db.SaveChangesAsync();
    }

    public async Task ClearWorkoutProgressAsync(
        string workoutName)
    {
        var workoutSnapshots = await _db.ProgressSnapshots
            .Where(x => x.WorkoutName == workoutName)
            .ToListAsync();

        var exerciseSnapshots = await _db.ExerciseProgressSnapshots
            .Where(x => x.WorkoutName == workoutName)
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
        var snapshots = await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.ExerciseName == exerciseName)
            .ToListAsync();

        _db.ExerciseProgressSnapshots.RemoveRange(snapshots);

        await _db.SaveChangesAsync();
    }

    public async Task<List<ExerciseProgressSnapshot>> GetExerciseProgressAsync(
        string workoutName,
        string exerciseName)
    {
        return await _db.ExerciseProgressSnapshots
            .Where(x =>
                x.WorkoutName == workoutName &&
                x.ExerciseName == exerciseName)
            .OrderBy(x => x.Date)
            .ToListAsync();
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
        return await _db.ExerciseProgressSnapshots
            .Where(x => x.WorkoutName == workoutName)
            .Select(x => x.ExerciseName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    private async Task<List<Exercise>> GetWorkoutExercisesForProgressAsync(
        string workoutName)
    {
        return await _db.Exercises
            .Include(x => x.ExerciseDefinition)
                .ThenInclude(x => x!.SecondaryMuscles)
            .Include(x => x.Sets)
            .Where(x => x.WorkoutName == workoutName)
            .ToListAsync();
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