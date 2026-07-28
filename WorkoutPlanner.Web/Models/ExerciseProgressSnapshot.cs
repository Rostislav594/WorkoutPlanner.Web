namespace WorkoutPlanner.Web.Models;

public class ExerciseProgressSnapshot
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public string ExerciseName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public double Score { get; set; }
}
