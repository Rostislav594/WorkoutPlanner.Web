namespace GymPlanner.Mobile.Notifications;

public sealed class UnavailableRemotePushTokenProvider : IRemotePushTokenProvider
{
    public event Action<string>? TokenChanged
    {
        add { }
        remove { }
    }
    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
