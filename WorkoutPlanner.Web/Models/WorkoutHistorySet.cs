namespace WorkoutPlanner.Web.Models;

public class WorkoutHistorySet
{
    public int SetNumber { get; set; }

    public double Weight { get; set; }

    public int Repetitions { get; set; }

    public bool Completed { get; set; }
}