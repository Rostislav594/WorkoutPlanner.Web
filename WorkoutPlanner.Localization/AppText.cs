using System.Globalization;
using System.Resources;

namespace WorkoutPlanner.Localization;

/// <summary>
/// Доступ к строкам интерфейса с ЯВНЫМ указанием языка.
/// </summary>
/// <remarks>
/// Почему не <c>IStringLocalizer</c>: он разрешает строки по
/// <see cref="CultureInfo.CurrentUICulture"/>, а в MAUI BlazorWebView культура
/// потока рендерера фиксируется при старте процесса. Проверено на устройстве:
/// после смены языка ни перерисовка, ни переход между страницами, ни полная
/// перезагрузка WebView не меняли язык — только перезапуск приложения.
/// Поэтому язык берётся из сервиса, а не из окружения потока.
/// </remarks>
public interface IAppText
{
    /// <summary>Текущий язык: "ru", "uk" или "en".</summary>
    string Language { get; }

    string this[string key] { get; }

    string Format(string key, params object[] args);

    /// <summary>Строка в нужной форме множественного числа; число идёт в <c>{0}</c>.</summary>
    string Plural(string key, int count);
}

public sealed class AppText(Func<string> languageProvider) : IAppText
{
    private static readonly ResourceManager Resources = new(
        $"{typeof(AppStrings).Namespace}.{nameof(AppStrings)}",
        typeof(AppStrings).Assembly);

    public string Language => AppLanguages.Resolve(languageProvider());

    public string this[string key] => Lookup(key) ?? key;

    public string Format(string key, params object[] args)
    {
        var template = Lookup(key);
        return template is null
            ? key
            : string.Format(CultureInfo.GetCultureInfo(Language), template, args);
    }

    public string Plural(string key, int count)
    {
        var language = Language;
        var category = PluralRules.Select(language, count);

        var template = Lookup($"{key}_{category}")
            // В английском нет формы Few — падаем на Many ("other").
            ?? (category == PluralCategory.Few ? Lookup($"{key}_{PluralCategory.Many}") : null);

        return template is null
            ? $"{count} {key}"
            : string.Format(CultureInfo.GetCultureInfo(language), template, count);
    }

    private string? Lookup(string key)
    {
        try
        {
            var value = Resources.GetString(key, CultureInfo.GetCultureInfo(Language));
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
