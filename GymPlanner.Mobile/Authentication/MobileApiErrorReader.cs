using System.Text.Json;

namespace GymPlanner.Mobile.Authentication;

internal static class MobileApiErrorReader
{
    public static async Task<IReadOnlyList<string>> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var messages = new List<string>();

            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in errors.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                        continue;

                    messages.AddRange(property.Value.EnumerateArray()
                        .Where(value => value.ValueKind == JsonValueKind.String)
                        .Select(value => value.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))!);
                }
            }

            if (messages.Count == 0 &&
                document.RootElement.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(title.GetString()))
            {
                messages.Add(title.GetString()!);
            }

            if (messages.Count > 0)
                return messages.Distinct(StringComparer.Ordinal).ToArray();
        }
        catch (JsonException)
        {
            // A non-ProblemDetails response falls back to the HTTP status below.
        }

        return [$"Сервер отклонил запрос ({(int)response.StatusCode})."];
    }
}
