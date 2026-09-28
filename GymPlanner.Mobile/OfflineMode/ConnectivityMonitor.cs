using GymPlanner.Mobile.Infrastructure;
using GymPlanner.Mobile.Lifecycle;
using GymPlanner.Mobile.Offline;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;

namespace GymPlanner.Mobile.OfflineMode;

/// <summary>
/// Следит, вернулась ли связь с сервером, пока приложение работает офлайн.
/// </summary>
/// <remarks>
/// Без связи экраны берут сохранённую копию и к серверу не ходят, поэтому
/// кто-то должен сам проверять, не появился ли он. Проба — лёгкий анонимный
/// запрос; её запускают смена сети, возврат приложения на экран, просьба
/// офлайн-слоя и редкий таймер, пока приложение видно.
/// </remarks>
public sealed class ConnectivityMonitor : IDisposable
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MinimumProbeSpacing = TimeSpan.FromSeconds(4);

    private readonly ServerReachability _reachability;
    private readonly MobileLifecycleService _lifecycle;
    private readonly OfflineRuntime _runtime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConnectivityMonitor> _logger;
    private readonly HttpClient _probeClient;
    private readonly CancellationTokenSource _stopping = new();
    private int _probing;
    private DateTimeOffset _lastProbeUtc = DateTimeOffset.MinValue;
    private bool _suspended;
    private bool _started;

    public ConnectivityMonitor(
        ServerReachability reachability,
        MobileLifecycleService lifecycle,
        OfflineRuntime runtime,
        MobileApiOptions options,
        TimeProvider timeProvider,
        ILogger<ConnectivityMonitor> logger)
    {
        _reachability = reachability;
        _lifecycle = lifecycle;
        _runtime = runtime;
        _timeProvider = timeProvider;
        _logger = logger;
        _probeClient = new HttpClient(new ReachabilityHttpHandler(reachability)
        {
            InnerHandler = MobileHttpMessageHandlerFactory.Create()
        })
        {
            BaseAddress = options.BaseAddress
        };
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        _reachability.SetNetworkAvailable(HasNetwork(Connectivity.Current.NetworkAccess));
        Connectivity.Current.ConnectivityChanged += NetworkChanged;
        _lifecycle.Resumed += Resumed;
        _lifecycle.Suspended += Suspended;
        _runtime.ProbeRequested += RequestProbe;
        _ = RunPeriodicProbeAsync(_stopping.Token);
    }

    public void RequestProbe() => _ = ProbeAsync(force: false);

    private void NetworkChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        var available = HasNetwork(args.NetworkAccess);
        _reachability.SetNetworkAvailable(available);
        if (available)
            _ = ProbeAsync(force: true);
    }

    private void Resumed()
    {
        _suspended = false;
        _reachability.SetNetworkAvailable(HasNetwork(Connectivity.Current.NetworkAccess));
        _ = ProbeAsync(force: true);
    }

    private void Suspended() => _suspended = true;

    private async Task RunPeriodicProbeAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ProbeInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!_suspended && _reachability.IsOffline)
                    await ProbeAsync(force: false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProbeAsync(bool force)
    {
        if (!force && _timeProvider.GetUtcNow() - _lastProbeUtc < MinimumProbeSpacing)
            return;
        if (Interlocked.Exchange(ref _probing, 1) == 1)
            return;

        try
        {
            _lastProbeUtc = _timeProvider.GetUtcNow();
            using var response = await _probeClient.GetAsync("api/v1/ping", _stopping.Token);
        }
        catch (HttpRequestException)
        {
            // Обработчик уже отметил, что сервер недоступен.
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Server reachability probe failed unexpectedly.");
        }
        finally
        {
            Volatile.Write(ref _probing, 0);
        }
    }

    private static bool HasNetwork(NetworkAccess access) =>
        access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet or NetworkAccess.Local;

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= NetworkChanged;
        _lifecycle.Resumed -= Resumed;
        _lifecycle.Suspended -= Suspended;
        _runtime.ProbeRequested -= RequestProbe;
        _stopping.Cancel();
        _stopping.Dispose();
        _probeClient.Dispose();
    }
}
