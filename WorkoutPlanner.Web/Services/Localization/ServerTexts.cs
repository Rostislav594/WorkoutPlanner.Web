using System.Globalization;
using WorkoutPlanner.Localization;

namespace WorkoutPlanner.Web.Services.Localization;

/// <summary>
/// Строки для серверного кода: сервисов, эндпоинтов и фоновых задач.
/// </summary>
/// <remarks>
/// На сервере культуру запроса выставляет <c>UseRequestLocalization</c> по
/// Accept-Language, поэтому <see cref="Current"/> берёт язык из окружения
/// запроса — в отличие от мобильного клиента, где культура потока не меняется
/// и язык приходится передавать явно.
///
/// <see cref="For"/> нужен там, где язык известен не из запроса, а из профиля
/// получателя: например, push-уведомление формируется на сервере, а прочитает
/// его пользователь на своём языке.
/// </remarks>
public static class ServerTexts
{
    public static IAppText Current { get; } =
        new AppText(() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static IAppText For(string? language) =>
        new AppText(() => AppLanguages.Resolve(language));
}
