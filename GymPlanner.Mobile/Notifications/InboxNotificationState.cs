using GymPlanner.Mobile.Api;

namespace GymPlanner.Mobile.Notifications;

public sealed class InboxNotificationState(IInboxApiClient inbox)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event Action? Changed;

    public int UnreadCount { get; private set; }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var result = await inbox.GetUnreadCountAsync(cancellationToken);
            if (result.Value is not null)
                SetUnreadCount(result.Value.UnreadCount);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void SetUnreadCount(int value)
    {
        var normalized = Math.Max(0, value);
        if (UnreadCount == normalized)
            return;
        UnreadCount = normalized;
        Changed?.Invoke();
    }
}
