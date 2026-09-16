namespace GymPlanner.Mobile.Localization;

/// <summary>
/// Оценка сложности упражнения — в ключ ресурса.
/// </summary>
/// <remarks>
/// Значение хранится на сервере строкой ("Easy", "Medium", ...) и переводится
/// на клиенте. Неизвестное значение показывается как «не оценено».
/// </remarks>
public static class WorkoutDifficulties
{
    public static string KeyFor(string? status) => status switch
    {
        "Easy" => "Difficulty_Easy",
        "Medium" => "Difficulty_Medium",
        "Hard" => "Difficulty_Hard",
        "Max" => "Difficulty_Max",
        _ => "Difficulty_NotRated"
    };
}
