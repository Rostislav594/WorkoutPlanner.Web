using WorkoutPlanner.Localization;

namespace WorkoutPlanner.Web.Models;

/// <summary>
/// Выбор названия из библиотеки упражнений по языку интерфейса.
/// </summary>
/// <remarks>
/// Русское название есть всегда: на нём написан каталог, и оно служит ключом.
/// Если перевода нет (например, упражнение добавили в базу вручную), показывается
/// русское, а не пустая строка.
/// </remarks>
public static class LibraryNames
{
    public static string For(string? language, string russian, string ukrainian, string english)
    {
        var translated = AppLanguages.Resolve(language) switch
        {
            AppLanguages.Ukrainian => ukrainian,
            AppLanguages.English => english,
            _ => russian
        };

        return string.IsNullOrWhiteSpace(translated) ? russian : translated;
    }
}
