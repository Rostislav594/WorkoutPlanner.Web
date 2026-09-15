using System.Diagnostics.CodeAnalysis;

namespace GymPlanner.Mobile.Notifications;

/// <summary>
/// Разбор ссылки подтверждения часов.
///
/// Часы открывают её на телефоне через Wear OS; здесь мы вытаскиваем
/// идентификатор заявки. Сам идентификатор — непрозрачный секрет, его
/// проверяет сервер, поэтому нас интересует только структура ссылки.
/// </summary>
public static class WatchPairingLink
{
    public const string Scheme = "gymplanner";
    public const string Host = "watch";
    public const string ApprovePath = "/approve";
    public const string RequestQueryName = "request";

    public static bool TryReadRequestId(
        string? uri,
        [NotNullWhen(true)] out string? requestId)
    {
        requestId = null;
        if (string.IsNullOrWhiteSpace(uri) ||
            !Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parsed.Host, Host, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                parsed.AbsolutePath.TrimEnd('/'),
                ApprovePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = parsed.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
                continue;

            var name = pair[..separator];
            if (!string.Equals(name, RequestQueryName, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            requestId = value;
            return true;
        }

        return false;
    }
}
