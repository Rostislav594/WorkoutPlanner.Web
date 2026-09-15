using System.Globalization;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.Localization;

public sealed class MauiAppLanguageService : IAppLanguageService
{
    private const string PreferenceKey = "app.language";

    private string _current = AppLanguages.Default;

    public string Current => _current;

    public bool IsExplicitlyChosen { get; private set; }

    public void Initialize()
    {
        var stored = ReadStoredLanguage();
        if (stored is not null)
        {
            IsExplicitlyChosen = true;
            SetCulture(stored);
            return;
        }

        // Выбора нет — берём язык устройства. Если он не поддерживается, будет русский.
        SetCulture(AppLanguages.ResolveFromPreferences(DeviceLanguageTags()));
    }

    public bool Apply(string languageCode, bool remember = true)
    {
        if (!AppLanguages.IsSupported(languageCode))
            return false;

        var resolved = AppLanguages.Resolve(languageCode);
        SetCulture(resolved);

        if (remember)
        {
            IsExplicitlyChosen = true;
            TryWritePreference(resolved);
        }

        return true;
    }

    public void ApplyFromProfile(string? languageCode)
    {
        if (IsExplicitlyChosen || !AppLanguages.IsSupported(languageCode))
            return;

        SetCulture(AppLanguages.Resolve(languageCode));
    }

    private void SetCulture(string languageCode)
    {
        _current = languageCode;
        var culture = CultureInfo.GetCultureInfo(languageCode);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static string? ReadStoredLanguage()
    {
        try
        {
            var stored = Preferences.Default.Get<string?>(PreferenceKey, null);
            return AppLanguages.IsSupported(stored) ? AppLanguages.Resolve(stored) : null;
        }
        catch (Exception)
        {
            // Хранилище настроек недоступно — работаем с языком устройства.
            return null;
        }
    }

    private static void TryWritePreference(string languageCode)
    {
        try
        {
            Preferences.Default.Set(PreferenceKey, languageCode);
        }
        catch (Exception)
        {
            // Не сохранилось — язык всё равно применён к текущему запуску.
        }
    }

    private static IEnumerable<string?> DeviceLanguageTags()
    {
        yield return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        yield return CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        yield return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
    }
}
