namespace WorkoutPlanner.Web.Models;

public class ProgressSnapshot
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public DateTime Date { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public double Score { get; set; }
}