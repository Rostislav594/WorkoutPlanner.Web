using System.Globalization;
using System.Resources;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.Localization;

/// <summary>
/// Переводит коды ошибок API в текст на языке пользователя.
///
/// Язык берётся явно, через <see cref="UseLanguage"/>, а не из
/// CurrentUICulture: в BlazorWebView культура потока фиксируется при старте
/// процесса и смену языка не отражает.
///
/// Класс статический, потому что <see cref="MobileApiErrorReader"/> вызывается
/// из всех API-клиентов без DI.
/// </summary>
internal static class ApiErrorMessages
{
    public const string ExtensionName = ApiErrorCodes.ExtensionName;

    private static readonly ResourceManager Resources = new(
        $"{typeof(ApiErrors).Namespace}.{nameof(ApiErrors)}",
        typeof(ApiErrors).Assembly);

    private static Func<string>? _languageProvider;

    /// <summary>Подключает источник текущего языка. Вызывается один раз при старте.</summary>
    public static void UseLanguage(Func<string> languageProvider) =>
        _languageProvider = languageProvider;

    private static CultureInfo Culture =>
        CultureInfo.GetCultureInfo(AppLanguages.Resolve(_languageProvider?.Invoke()));

    /// <summary>
    /// Текст для кода ошибки или <c>null</c>, если код неизвестен этой сборке.
    /// Null — важный случай: сервер новее клиента и прислал код,
    /// про который приложение ещё не знает. Тогда вызывающий код
    /// откатывается на текст из поля "errors".
    /// </summary>
    public static string? Describe(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return null;

        try
        {
            var value = Resources.GetString(errorCode, Culture);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }

    public static string ServerRejected(int statusCode)
    {
        var template = Describe(ApiErrorCodes.ServerRejected);
        return template is null
            ? $"Server rejected the request ({statusCode})."
            : string.Format(Culture, template, statusCode);
    }

    public static string Unknown() =>
        Describe(ApiErrorCodes.Unknown) ?? "Something went wrong.";

    public static string NetworkUnavailable() =>
        Describe(ApiErrorCodes.NetworkUnavailable) ?? "No connection to the server.";
}
