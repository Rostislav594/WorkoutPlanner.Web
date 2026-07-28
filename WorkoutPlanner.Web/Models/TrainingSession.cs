namespace WorkoutPlanner.Web.Models;

public class TrainingSession
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public DateTime Date { get; set; }

    public int TrainingPlanId { get; set; }

    public TrainingPlan? TrainingPlan { get; set; }

    public List<TrainingSessionExercise> Exercises { get; set; }
        = new();
}
