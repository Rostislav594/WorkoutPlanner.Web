using System.ComponentModel.DataAnnotations.Schema;

namespace WorkoutPlanner.Web.Models;

public class WorkoutHistoryExercise
{
    public string Name { get; set; } = "";

    public ExerciseStatus Status { get; set; }

    public List<WorkoutHistorySet> Sets { get; set; } = new();

    public List<string> Photos { get; set; } = new();

    [NotMapped]
    /// <summary>Ключ ресурса с подписью оценки: текст подставляет интерфейс.</summary>
    public string StatusTextKey =>
            Status switch
            {
                ExerciseStatus.Easy => "Web_Rate_Easy",
                ExerciseStatus.Medium => "Web_Rate_Medium",
                ExerciseStatus.Hard => "Web_Rate_Hard",
                ExerciseStatus.Max => "Web_Rate_Max",
                ExerciseStatus.NotCompleted => "Difficulty_NotRated",
                _ => ""
            };

    [NotMapped]
    public string StatusColor =>
        Status switch
        {
            ExerciseStatus.Easy => "#78f6c7",
            ExerciseStatus.Medium => "#ffc107",
            ExerciseStatus.Hard => "#fd7e14",
            ExerciseStatus.Max => "#dc3545",
            _ => "#999999"
        };


}

