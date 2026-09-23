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
    public static string EffortKeyFor(string? status) => status switch
    {
        "Easy" => "Effort_Easy",
        "Medium" => "Effort_Medium",
        "Hard" => "Effort_Hard",
        "Max" => "Effort_Max",
        _ => "Difficulty_NotRated"
    };

    public static string ToneFor(string? status) => status switch
    {
        "Easy" => "easy",
        "Medium" => "medium",
        "Hard" => "hard",
        "Max" => "max",
        _ => "not-rated"
    };

    public static string KeyFor(string? status) => status switch
    {
        "Easy" => "Difficulty_Easy",
        "Medium" => "Difficulty_Medium",
        "Hard" => "Difficulty_Hard",
        "Max" => "Difficulty_Max",
        _ => "Difficulty_NotRated"
    };
}
