using WorkoutPlanner.Localization;

namespace WorkoutPlanner.Web.Api;

/// <summary>
/// Построение ответов об ошибке, несущих машиночитаемый код.
///
/// Контракт с клиентами:
///   • "errorCodes" — массив кодов из <see cref="ApiErrorCodes"/>. Это источник истины,
///     клиент показывает свой перевод и не зависит от языка сервера.
///   • "errors" — прежний словарь с текстом. Остаётся ради старых сборок приложения,
///     которые про коды ещё не знают, и ради читаемости ответа в отладке.
///
/// Текст здесь намеренно английский и технический: пользователь его не увидит,
/// если клиент понимает коды. Переводы живут в ApiErrors.resx.
/// </summary>
public static class ApiProblems
{
    public static IResult ValidationProblem(
        string field,
        string errorCode,
        string developerMessage) =>
        ValidationProblem(new Dictionary<string, (string Code, string Message)>
        {
            [field] = (errorCode, developerMessage)
        });

    public static IResult ValidationProblem(
        IReadOnlyDictionary<string, (string Code, string Message)> failures)
    {
        var errors = failures.ToDictionary(
            pair => pair.Key,
            pair => new[] { pair.Value.Message },
            StringComparer.Ordinal);

        var codes = failures.Values
            .Select(failure => failure.Code)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Results.ValidationProblem(
            errors,
            extensions: new Dictionary<string, object?>
            {
                [ApiErrorCodes.ExtensionName] = codes
            });
    }

    /// <summary>Ошибка уровня запроса, не привязанная к конкретному полю.</summary>
    public static IResult Problem(
        string errorCode,
        string developerMessage,
        int statusCode) =>
        Results.Problem(
            title: developerMessage,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                [ApiErrorCodes.ExtensionName] = new[] { errorCode }
            });
}
