namespace WorkoutPlanner.Web.Models;

/// <summary>
/// Крупная часть тела: по ней телефон делит библиотеку упражнений на разделы.
/// Восемнадцать анатомических мышц для этого слишком дробные.
/// </summary>
public enum MuscleBodyPart
{
    Other = 0,
    Chest = 1,
    Back = 2,
    Shoulders = 3,
    Arms = 4,
    Core = 5,
    Legs = 6,
    Glutes = 7
}
