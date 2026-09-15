using System.Globalization;
using WorkoutPlanner.Api.Contracts;

namespace GymPlanner.Mobile.Notifications;

public sealed class NotificationNavigationService
{
    private readonly object _sync = new();
    private string? _pendingRoute;
    private bool _isReady;

    public event Action<string>? RouteRequested;

    public void Open(string route)
    {
        if (!IsSafeRoute(route))
            return;

        Action<string>? handler;
        lock (_sync)
        {
            handler = RouteRequested;
            if (!_isReady || handler is null)
            {
                _pendingRoute = route;
                handler = null;
            }
            else
            {
                _pendingRoute = null;
            }
        }

        handler?.Invoke(route);
    }

    public bool OpenPush(string? type, string? inboxMessageId)
    {
        if (!PushNotificationTypeSerializer.TryParsePayloadValue(type, out _) ||
            !long.TryParse(
                inboxMessageId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var messageId) ||
            messageId <= 0)
        {
            return false;
        }

        Open($"/notifications/message/{messageId}");
        return true;
    }

    /// <summary>
    /// Переход на экран подтверждения часов по ссылке, открытой с часов.
    /// Идентификатор заявки — просто непрозрачный секрет, поэтому проверяем
    /// только форму, а решение о валидности принимает сервер.
    /// </summary>
    public bool OpenWatchApproval(string? requestId)
    {
        if (!IsPairingRequestId(requestId))
            return false;

        Open($"/watch/approve/{requestId}");
        return true;
    }

    public void MarkReady()
    {
        Action<string>? handler;
        string? route;
        lock (_sync)
        {
            _isReady = true;
            handler = RouteRequested;
            route = handler is null ? null : _pendingRoute;
            if (route is not null)
                _pendingRoute = null;
        }

        if (route is not null)
            handler?.Invoke(route);
    }

    public string? ConsumePendingRoute()
    {
        lock (_sync)
        {
            var route = _pendingRoute;
            _pendingRoute = null;
            return route;
        }
    }

    private static bool IsPairingRequestId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 64 &&
        value.All(x => char.IsAsciiLetterOrDigit(x) || x == '-' || x == '_');

    private static bool IsSafeRoute(string route)
    {
        if (!Uri.TryCreate(route, UriKind.Relative, out _))
            return false;

        if (route.StartsWith("/workouts/", StringComparison.Ordinal) &&
            int.TryParse(route["/workouts/".Length..], out var workoutId))
        {
            return workoutId > 0;
        }

        if (route.StartsWith("/watch/approve/", StringComparison.Ordinal))
            return IsPairingRequestId(route["/watch/approve/".Length..]);

        return route.StartsWith("/notifications/message/", StringComparison.Ordinal) &&
            long.TryParse(
                route["/notifications/message/".Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var messageId) &&
            messageId > 0;
    }
}
