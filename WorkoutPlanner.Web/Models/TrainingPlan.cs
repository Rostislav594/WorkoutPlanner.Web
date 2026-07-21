namespace WorkoutPlanner.Web.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class TrainingPlan
{
    public int Id { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public List<Exercise> Exercises { get; set; } = new();

    [NotMapped]
    public string DisplayDate =>
     Date.ToString("dd.MM.yyyy");

    public List<TrainingSession> TrainingSessions
    {
        get;
        set;
    }
=
new();
}