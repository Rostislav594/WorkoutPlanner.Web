using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.Localization;

/// <summary>
/// Доступ к строкам интерфейса из классов, которые создаёт не DI.
/// </summary>
/// <remarks>
/// Платформенные сервисы — уведомления, выбор фотографии, системный список —
/// создаются каркасом Android/iOS, поэтому <see cref="IAppText"/> в них не
/// внедрить конструктором. Экземпляр подключается один раз при старте
/// (<c>MauiProgram</c>), как и <see cref="ApiErrorMessages.UseLanguage"/>.
///
/// Неизвестный ключ возвращается как есть — то же поведение, что у AppText.
/// </remarks>
internal static class AppTexts
{
    private static IAppText? _text;

    public static void Use(IAppText text) => _text = text;

    public static string Get(string key) => _text is null ? key : _text[key];

    public static string Format(string key, params object[] args) =>
        _text is null ? key : _text.Format(key, args);
}
