using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IProgressService
{
    Task<double> CalculateWorkoutScoreAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task SaveProgressAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task<List<ProgressSnapshot>> GetWorkoutProgressAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task<List<ProgressChartPoint>> GetWorkoutChartAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task ClearAllProgressAsync(
        CancellationToken cancellationToken = default);
    Task ClearWorkoutProgressAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
    Task ClearExerciseProgressAsync(
        string workoutName,
        string exerciseName,
        CancellationToken cancellationToken = default);
    Task<List<ProgressChartPoint>> GetExerciseChartAsync(
        string workoutName,
        string exerciseName,
        CancellationToken cancellationToken = default);
    Task<List<string>> GetWorkoutExercisesAsync(
        string workoutName,
        CancellationToken cancellationToken = default);
}
