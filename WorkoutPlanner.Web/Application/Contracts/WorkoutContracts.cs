namespace WorkoutPlanner.Web.Application.Contracts;

public sealed class TrainingPlan
{
    public int Id { get; set; }
    public string WorkoutName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<Exercise> Exercises { get; set; } = [];
    public string DisplayDate => Date.ToString("dd.MM.yyyy");
}

public sealed class Exercise
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public string WorkoutName { get; set; } = string.Empty;
    public int SetsCount { get; set; } = 3;
    public ExerciseStatus Status { get; set; }
    public int TrainingPlanId { get; set; }
    public int? ExerciseDefinitionId { get; set; }
    public int? SupersetGroupId { get; set; }
    public ExerciseDefinition? ExerciseDefinition { get; set; }
    public List<ExerciseTemplateSet> Sets { get; set; } = [];
    public bool IsCompleted => Sets.Count > 0 && Sets.All(x => x.Completed);
    public IEnumerable<int> Repetitions => Sets
        .OrderBy(x => x.SetNumber)
        .Select(x => x.Repetitions);
    public string StatusText => Status switch
    {
        ExerciseStatus.Easy => "Легко",
        ExerciseStatus.Medium => "Средне",
        ExerciseStatus.Hard => "Тяжело",
        ExerciseStatus.Max => "На пределе",
        ExerciseStatus.NotCompleted => "Не выбрано",
        _ => string.Empty
    };
    public string StatusColor => Status switch
    {
        ExerciseStatus.Easy => "#78f6c7",
        ExerciseStatus.Medium => "#ffc107",
        ExerciseStatus.Hard => "#fd7e14",
        ExerciseStatus.Max => "#dc3545",
        _ => "#999999"
    };
}

public sealed class ExerciseTemplateSet
{
    public int Id { get; set; }
    public int SetNumber { get; set; }
    public int Repetitions { get; set; }
    public double Weight { get; set; }
    public bool Completed { get; set; }
    public bool IsWarmup { get; set; }
}

public sealed class ExerciseDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public enum ExerciseStatus
{
    Easy,
    Medium,
    Hard,
    Max,
    NotCompleted
}

public sealed class WorkoutDay
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int TrainingPlanId { get; set; }
    public bool IsCompleted { get; set; }
}
