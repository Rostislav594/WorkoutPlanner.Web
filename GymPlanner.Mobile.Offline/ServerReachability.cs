namespace GymPlanner.Mobile.Offline;

/// <summary>
/// Можно ли сейчас достучаться до сервера.
/// </summary>
/// <remarks>
/// Сеть телефона — только половина ответа: Wi-Fi в зале может быть без интернета,
/// а сервер — просто выключен. Поэтому состояние складывается из сигнала системы
/// и исхода последних запросов: после сбоя приложение считает себя офлайн, пока
/// очередной запрос или проба не получат ответ сервера.
/// </remarks>
public sealed class ServerReachability(TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private bool _networkAvailable = true;
    private DateTimeOffset? _lastResponseUtc;
    private DateTimeOffset? _lastFailureUtc;

    /// <summary>Сменилось <see cref="IsOffline"/> или время последней связи.</summary>
    public event Action? Changed;

    public bool IsOffline
    {
        get
        {
            lock (_lock)
                return IsOfflineCore();
        }
    }

    /// <summary>Когда сервер отвечал в последний раз за время работы приложения.</summary>
    public DateTimeOffset? LastResponseUtc
    {
        get
        {
            lock (_lock)
                return _lastResponseUtc;
        }
    }

    public void SetNetworkAvailable(bool available) =>
        Mutate(() => _networkAvailable = available);

    /// <summary>Сервер ответил — неважно, успехом или бизнес-ошибкой.</summary>
    public void ReportServerResponded() =>
        Mutate(() =>
        {
            _networkAvailable = true;
            _lastResponseUtc = timeProvider.GetUtcNow();
        });

    /// <summary>Запрос не дошёл до сервера или сервер не смог ответить.</summary>
    public void ReportServerUnavailable() =>
        Mutate(() => _lastFailureUtc = timeProvider.GetUtcNow());

    private bool IsOfflineCore() =>
        !_networkAvailable ||
        _lastFailureUtc is { } failure && (_lastResponseUtc is not { } response || failure > response);

    private void Mutate(Action change)
    {
        bool raise;
        lock (_lock)
        {
            var wasOffline = IsOfflineCore();
            var previousResponse = _lastResponseUtc;
            change();
            raise = wasOffline != IsOfflineCore() ||
                    (_lastResponseUtc - previousResponse) > TimeSpan.FromMinutes(1);
        }

        if (raise)
            Changed?.Invoke();
    }
}
