using System.Text.Json;
using System.Text.RegularExpressions;

namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Какой настоящий ID сервер выдал тому, что было создано на телефоне без связи.
/// </summary>
/// <remarks>
/// План, созданный офлайн, живёт под временным ID, и следующие изменения в очереди
/// (упражнения в нём, назначение в календарь) ссылаются на этот ID. Когда сервер
/// создаёт план, его настоящий ID подставляется во все ещё не отправленные запросы.
/// Значение <c>null</c> — сервер отказался создавать: зависящие изменения теряют смысл.
/// </remarks>
public static partial class LocalIdMap
{
    /// <summary>Сколько соответствий помнить: экраны могут держать старый ID ещё какое-то время.</summary>
    public const int Capacity = 500;

    /// <summary>Временные ID в пути или теле запроса: десятизначные отрицательные числа.</summary>
    [GeneratedRegex(@"(?<![\d.])-[12]\d{9}(?![\d.])")]
    private static partial Regex LocalIdPattern();

    public static IEnumerable<int> FindLocalIds(string? text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        foreach (Match match in LocalIdPattern().Matches(text))
        {
            if (int.TryParse(match.Value, out var id) && OfflineIds.IsLocal(id))
                yield return id;
        }
    }

    /// <summary>Подставляет известные настоящие ID вместо временных.</summary>
    public static string? Rewrite(string? text, IReadOnlyDictionary<int, int?> map) =>
        string.IsNullOrEmpty(text) || map.Count == 0
            ? text
            : LocalIdPattern().Replace(text, match =>
                int.TryParse(match.Value, out var id) && map.TryGetValue(id, out var real) && real is { } realId
                    ? realId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : match.Value);

    /// <summary>Ссылается ли операция на то, что сервер создать отказался.</summary>
    public static bool ReferencesRejected(OutboxOperation operation, IReadOnlyDictionary<int, int?> map) =>
        FindLocalIds(operation.Path)
            .Concat(FindLocalIds(operation.Body))
            .Any(id => map.TryGetValue(id, out var real) && real is null);

    /// <summary>ID созданной записи из ответа сервера.</summary>
    public static int? ReadCreatedId(string kind, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (kind == OutboxKinds.CompleteFreeWorkout &&
                root.TryGetProperty("history", out var history))
            {
                root = history;
            }

            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("id", out var id) &&
                   id.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
