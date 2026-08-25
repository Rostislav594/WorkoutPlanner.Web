namespace WorkoutPlanner.Web.Application.Contracts;

public sealed class WorkoutHistory
{
    public int Id { get; set; }
    public string WorkoutName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public sealed class WorkoutHistoryDetails
{
    public List<WorkoutHistoryExercise> Exercises { get; set; } = [];
}

public sealed class WorkoutHistoryExercise
{
    public string Name { get; set; } = string.Empty;
    public ExerciseStatus Status { get; set; }
    public int? SupersetGroupId { get; set; }
    public List<WorkoutHistorySet> Sets { get; set; } = [];
    public List<string> Photos { get; set; } = [];
    public string StatusText => Status switch
    {
        ExerciseStatus.Easy => "😊 Легко",
        ExerciseStatus.Medium => "😐 Средне",
        ExerciseStatus.Hard => "🥵 Тяжело",
        ExerciseStatus.Max => "🤯 На пределе",
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

public sealed class WorkoutHistorySet
{
    public int SetNumber { get; set; }
    public double Weight { get; set; }
    public int Repetitions { get; set; }
    public bool Completed { get; set; }
    public bool IsWarmup { get; set; }
}

public enum WorkoutCompletionFailure
{
    None,
    NoScheduledWorkout,
    NoExercises,
    ExerciseStatusMissing
}

public sealed record WorkoutCompletionResult(
    bool Succeeded,
    WorkoutCompletionFailure Failure,
    WorkoutHistory? History);

public sealed class FreeWorkoutCompletion
{
    public bool SaveAsTemplate { get; set; }
    public string? TemplateName { get; set; }
    public List<Exercise> Exercises { get; set; } = [];
}

public enum FreeWorkoutCompletionFailure
{
    None,
    InvalidWorkout,
    TemplateNameRequired,
    TemplateNameConflict,
    ExerciseDefinitionMissing
}

public sealed record FreeWorkoutCompletionResult(
    bool Succeeded,
    FreeWorkoutCompletionFailure Failure,
    string? Error,
    WorkoutHistory? History,
    int? TrainingPlanId);
