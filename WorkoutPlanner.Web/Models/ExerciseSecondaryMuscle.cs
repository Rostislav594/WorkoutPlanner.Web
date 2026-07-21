using WorkoutPlanner.Web.Models;

public class ExerciseSecondaryMuscle
{
    public int Id { get; set; }

    public int ExerciseDefinitionId { get; set; }

    public ExerciseDefinition? ExerciseDefinition { get; set; }

    public int MuscleId { get; set; }

    public Muscle? Muscle { get; set; }

    public double Coefficient { get; set; }
}
