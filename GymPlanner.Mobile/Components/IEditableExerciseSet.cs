namespace GymPlanner.Mobile.Components;

/// <summary>
/// Подход в окне упражнения: вес и повторения правятся прямо в черновике
/// страницы — свободной тренировки или шаблона.
/// </summary>
public interface IEditableExerciseSet
{
    int SetNumber { get; }

    double Weight { get; set; }

    int Repetitions { get; set; }
}
