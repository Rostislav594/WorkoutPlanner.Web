namespace GymPlanner.Mobile.Lifecycle;

public sealed class MobileLifecycleService
{
    public event Action? Resumed;

    public void NotifyResumed() => Resumed?.Invoke();
}
