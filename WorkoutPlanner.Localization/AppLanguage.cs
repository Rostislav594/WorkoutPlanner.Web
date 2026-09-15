using System.Globalization;

namespace WorkoutPlanner.Localization;

/// <summary>
/// Язык интерфейса, поддерживаемый приложением.
/// Код — это то, что хранится в профиле пользователя и уходит в заголовке Accept-Language.
/// </summary>
public sealed record AppLanguage(string Code, string NativeName, string EnglishName)
{
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Code);
}

public static class AppLanguages
{
    public const string Russian = "ru";
    public const string Ukrainian = "uk";
    public const string English = "en";

    /// <summary>Язык, на котором написано приложение: запасной вариант для всего.</summary>
    public const string Default = Russian;

    public static IReadOnlyList<AppLanguage> All { get; } =
    [
        new(Russian, "Русский", "Russian"),
        new(Ukrainian, "Українська", "Ukrainian"),
        new(English, "English", "English")
    ];

    public static IReadOnlyList<string> Codes { get; } = [.. All.Select(language => language.Code)];

    public static bool IsSupported(string? code) =>
        code is not null && Codes.Contains(Normalize(code), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Приводит любой тег культуры к поддерживаемому коду языка.
    /// "ru-RU" -> "ru", "uk-UA" -> "uk", "en-GB" -> "en", всё остальное -> язык по умолчанию.
    /// </summary>
    public static string Resolve(string? cultureTag)
    {
        var normalized = Normalize(cultureTag);
        return Codes.FirstOrDefault(
            code => string.Equals(code, normalized, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }

    /// <summary>
    /// Выбирает язык из списка культур устройства, беря первую поддерживаемую.
    /// </summary>
    public static string ResolveFromPreferences(IEnumerable<string?> cultureTags)
    {
        foreach (var tag in cultureTags)
        {
            var normalized = Normalize(tag);
            if (normalized.Length == 0)
                continue;

            var match = Codes.FirstOrDefault(
                code => string.Equals(code, normalized, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return Default;
    }

    public static AppLanguage Get(string? code) =>
        All.First(language => language.Code == Resolve(code));

    private static string Normalize(string? cultureTag)
    {
        if (string.IsNullOrWhiteSpace(cultureTag))
            return string.Empty;

        var trimmed = cultureTag.Trim();

        // Отрезаем регион и вес из Accept-Language: "uk-UA;q=0.8" -> "uk".
        var separator = trimmed.IndexOfAny(['-', '_', ';']);
        return separator < 0 ? trimmed : trimmed[..separator];
    }
}
