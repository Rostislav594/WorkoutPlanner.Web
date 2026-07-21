namespace WorkoutPlanner.Web.Models;

public class ExerciseTemplateSet
{
    public int Id { get; set; }

    public int ExerciseId { get; set; }

    public Exercise? Exercise { get; set; }

    public int SetNumber { get; set; }

    public int Repetitions { get; set; }

    public bool Completed { get; set; }
}
