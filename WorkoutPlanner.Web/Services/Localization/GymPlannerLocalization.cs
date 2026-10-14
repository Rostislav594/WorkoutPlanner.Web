using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using WorkoutPlanner.Localization;

namespace WorkoutPlanner.Web.Services.Localization;

public static class GymPlannerLocalization
{
    /// <summary>
    /// Настройки культуры запроса.
    ///
    /// Порядок источников важен: сначала явный выбор пользователя
    /// (query-параметр и cookie — ими пользуется веб-версия), затем
    /// Accept-Language, который шлёт мобильный клиент и часы.
    /// Если ничего не подошло — русский.
    /// </summary>
    public static RequestLocalizationOptions CreateOptions()
    {
        var cultures = AppLanguages.Codes
            .Select(code => new CultureInfo(code))
            .ToArray();

        var options = new RequestLocalizationOptions()
            .SetDefaultCulture(AppLanguages.Default)
            .AddSupportedCultures([.. AppLanguages.Codes])
            .AddSupportedUICultures([.. AppLanguages.Codes]);

        options.SupportedCultures = cultures;
        options.SupportedUICultures = cultures;
        options.ApplyCurrentCultureToResponseHeaders = true;

        return options;
    }

    /// <summary>
    /// Настройки культуры для веб-интерфейса (админки): он всегда на русском,
    /// какой бы язык ни стоял в браузере. Источников культуры нет, поэтому
    /// и Accept-Language, и cookie игнорируются.
    /// </summary>
    public static RequestLocalizationOptions CreateWebOptions()
    {
        var russian = new CultureInfo(AppLanguages.Russian);
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(russian),
            SupportedCultures = [russian],
            SupportedUICultures = [russian],
            ApplyCurrentCultureToResponseHeaders = true
        };
        options.RequestCultureProviders.Clear();

        return options;
    }
}
