namespace WorkoutPlanner.Web.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class TrainingPlan
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    /// <summary>
    /// План-черновик свободной тренировки, которая идёт прямо сейчас.
    /// </summary>
    /// <remarks>
    /// Свободная тренировка живёт на сервере с момента начала, иначе часы её не
    /// видят: они спрашивают активную тренировку у сервера и о локальном
    /// черновике телефона ничего не знают. Черновик намеренно хранится обычным
    /// планом — так к нему применимы и правка упражнений, и выдача на часы без
    /// второго набора эндпоинтов. От шаблонов он отличается этим флагом и
    /// поэтому не показывается в списке планов.
    /// </remarks>
    public bool IsFreeDraft { get; set; }

    public DateTime Date { get; set; }

    public List<Exercise> Exercises { get; set; } = new();

    [NotMapped]
    public string DisplayDate =>
     Date.ToString("dd.MM.yyyy");

    public List<TrainingSession> TrainingSessions
    {
        get;
        set;
    }
=
new();
}