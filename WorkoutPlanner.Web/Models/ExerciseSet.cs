namespace WorkoutPlanner.Web.Models;

public class ExerciseSet
{
    public int Id { get; set; }

    public int TrainingSessionExerciseId { get; set; }

    public TrainingSessionExercise? TrainingSessionExercise
    {
        get;
        set;
    }

    public int SetNumber { get; set; }

    public double Weight { get; set; }

    public int Repetitions { get; set; }

    public bool Completed { get; set; }

    public bool IsWarmup { get; set; }
}
