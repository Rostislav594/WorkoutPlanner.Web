namespace GymPlanner.Mobile.Notifications;

public interface IRemotePushTokenProvider
{
    event Action<string>? TokenChanged;
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}
