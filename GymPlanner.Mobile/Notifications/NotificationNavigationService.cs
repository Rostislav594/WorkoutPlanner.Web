namespace GymPlanner.Mobile.Notifications;

public sealed class NotificationNavigationService
{
    private readonly object _sync = new();
    private string? _pendingRoute;

    public event Action<string>? RouteRequested;

    public void Open(string route)
    {
        if (!IsSafeRoute(route))
            return;

        Action<string>? handler;
        lock (_sync)
        {
            handler = RouteRequested;
            _pendingRoute = handler is null ? route : null;
        }

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

    private static bool IsSafeRoute(string route) =>
        Uri.TryCreate(route, UriKind.Relative, out _) &&
        route.StartsWith("/workouts/", StringComparison.Ordinal) &&
        int.TryParse(route["/workouts/".Length..], out var id) &&
        id > 0;
}
