namespace WorkoutPlanner.Web.Models;

public class WorkoutHistory
{
    public int Id { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}