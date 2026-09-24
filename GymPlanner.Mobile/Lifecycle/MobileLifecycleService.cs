namespace GymPlanner.Mobile.Lifecycle;

public sealed class MobileLifecycleService
{
    public event Action? Resumed;

    public event Action? Suspended;

    public void NotifyResumed() => Resumed?.Invoke();

    public void NotifySuspended() => Suspended?.Invoke();
}
