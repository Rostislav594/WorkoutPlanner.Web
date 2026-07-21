using System.ComponentModel.DataAnnotations.Schema;
using WorkoutPlanner.Web.Services;

namespace WorkoutPlanner.Web.Models;

public class Exercise
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public string WorkoutName { get; set; } = "";

    public string BaseReps { get; set; } = "8,7,6";

    public int SetsCount { get; set; } = 3;

    public bool Set1Completed { get; set; }

    public bool Set2Completed { get; set; }

    public bool Set3Completed { get; set; }

    public ExerciseStatus Status { get; set; }

    public int TrainingPlanId { get; set; }

    public TrainingPlan? TrainingPlan { get; set; }

    public int? ExerciseDefinitionId { get; set; }

    public ExerciseDefinition? ExerciseDefinition { get; set; }

    public List<ExerciseTemplateSet> Sets { get; set; } = new();

    public double CurrentWeight { get; set; }

    public double WeightStep { get; set; }

    public int CurrentLevel { get; set; }

    [NotMapped]
    public string Reps =>
    ProgressionService.GetReps(BaseReps, CurrentLevel);

    [NotMapped]
    public string[] RepsArray =>
        Reps.Split(',');

    [NotMapped]
    public bool IsCompleted =>
    Sets.Count > 0 &&
    Sets.All(x => x.Completed);

    [NotMapped]
    public string DisplayWeightStep =>
        $"+{WeightStep} кг";

    [NotMapped]
    public bool IsLastLevel =>
    CurrentLevel >= 4;

    [NotMapped]
    public double NextWeight =>
        CurrentWeight + WeightStep;

    [NotMapped]
    public IEnumerable<int> Repetitions =>
    Sets
        .OrderBy(x => x.SetNumber)
        .Select(x => x.Repetitions);

    [NotMapped]
    public string StatusText =>
        Status switch
        {
            ExerciseStatus.Easy => "Легко",
            ExerciseStatus.Medium => "Средне",
            ExerciseStatus.Hard => "Тяжело",
            ExerciseStatus.Max => "На пределе",
            ExerciseStatus.NotCompleted => "Не выбрано",
            _ => ""
        };
    [NotMapped]
    public string StatusColor =>
    Status switch
    {
        ExerciseStatus.Easy => "#28a745",
        ExerciseStatus.Medium => "#ffc107",
        ExerciseStatus.Hard => "#fd7e14",
        ExerciseStatus.Max => "#dc3545",
        _ => "#999999"
    };

}
