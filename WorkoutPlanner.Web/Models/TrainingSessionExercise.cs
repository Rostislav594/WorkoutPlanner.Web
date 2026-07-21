namespace WorkoutPlanner.Web.Models;

public class TrainingSessionExercise
{
    public int Id { get; set; }

    public int TrainingSessionId { get; set; }

    public TrainingSession? TrainingSession { get; set; }

    public int ExerciseDefinitionId { get; set; }

    public ExerciseDefinition? ExerciseDefinition { get; set; }

    public ExerciseStatus Status { get; set; }

    public double ExerciseIndex { get; set; }

    public List<ExerciseSet> Sets { get; set; }
        = new();
}
