namespace WorkoutPlanner.Web.Models;

public class Muscle
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public ICollection<ExerciseSecondaryMuscle> SecondaryMuscleLinks { get; set; } = [];
}