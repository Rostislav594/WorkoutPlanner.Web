using Microsoft.Extensions.Logging;
using WorkoutPlanner.Api.Contracts;
using GymPlanner.Mobile.Authentication;

namespace GymPlanner.Mobile.Notifications;

public sealed class PushRegistrationCoordinator(
    MobileAuthenticationService authentication,
    IRemotePushTokenProvider tokenProvider,
    IRemotePushRegistrationService registration,
    ILocalNotificationPlatform localNotifications,
    ILogger<PushRegistrationCoordinator> logger) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _lastRegisteredToken;
    private bool _permissionRequested;

    public async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        authentication.AuthenticationChanged += OnAuthenticationChanged;
        tokenProvider.TokenChanged += OnTokenChanged;
        await TryRegisterAsync(cancellationToken);
    }

    private void OnAuthenticationChanged() => _ = TryRegisterAsync();
    private void OnTokenChanged(string token) => _ = TryRegisterAsync();

    private async System.Threading.Tasks.Task TryRegisterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!authentication.IsAuthenticated)
            {
                _lastRegisteredToken = null;
                return;
            }
            if (!_permissionRequested)
            {
                await localNotifications.RequestPermissionAsync(cancellationToken);
                _permissionRequested = true;
            }
            var token = await tokenProvider.GetTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token)) return;
            if (string.Equals(token, _lastRegisteredToken, StringComparison.Ordinal)) return;
            await registration.RegisterAsync(
                new RegisterPushDeviceRequest(InstallationIdStore.Get(), "android", token), cancellationToken);
            _lastRegisteredToken = token;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Push device registration failed; it will be retried after the next auth/token event.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        authentication.AuthenticationChanged -= OnAuthenticationChanged;
        tokenProvider.TokenChanged -= OnTokenChanged;
        _gate.Dispose();
    }
}

internal static class InstallationIdStore
{
    private const string Key = "gymplanner.installation-id";
    public static string Get()
    {
        var value = Preferences.Default.Get(Key, string.Empty);
        if (!Guid.TryParse(value, out _))
        {
            value = Guid.NewGuid().ToString("D");
            Preferences.Default.Set(Key, value);
        }
        return value;
    }
}
