namespace WorkoutPlanner.Web.Models;

public class WorkoutDay
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public int TrainingPlanId { get; set; }

    public bool IsCompleted { get; set; }
}