namespace WorkoutPlanner.Web.Models;

public class ExerciseTemplateSet
{
    public int Id { get; set; }

    public int ExerciseId { get; set; }

    public Exercise? Exercise { get; set; }

    public int SetNumber { get; set; }

    public int Repetitions { get; set; }

    public double Weight { get; set; }

    public bool Completed { get; set; }

    public bool IsWarmup { get; set; }

    /// <summary>
    /// Отдых после этого подхода, в секундах; у суперсета — после круга с этим
    /// номером. <c>null</c> — действует отдых упражнения по умолчанию.
    /// </summary>
    public int? RestAfterSeconds { get; set; }

    public long Version { get; set; }
}
