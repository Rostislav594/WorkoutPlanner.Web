using System.ComponentModel.DataAnnotations.Schema;

namespace WorkoutPlanner.Web.Models;

public class WorkoutHistoryExercise
{
    public string Name { get; set; } = "";

    public ExerciseStatus Status { get; set; }

    public List<WorkoutHistorySet> Sets { get; set; } = new();

    public List<string> Photos { get; set; } = new();

    [NotMapped]
    public string StatusText =>
            Status switch
            {
                ExerciseStatus.Easy => "😊 Легко",
                ExerciseStatus.Medium => "😐 Средне",
                ExerciseStatus.Hard => "🥵 Тяжело",
                ExerciseStatus.Max => "🤯 На пределе",
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

