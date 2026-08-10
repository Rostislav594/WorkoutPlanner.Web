namespace GymPlanner.Mobile.Navigation;

public sealed class MobileBackNavigationService
{
    private readonly object _sync = new();
    private bool _canNavigateBack;

    public event Action? BackRequested;

    public void SetCanNavigateBack(bool canNavigateBack)
    {
        lock (_sync)
            _canNavigateBack = canNavigateBack;
    }

    public bool TryRequestBack()
    {
        Action? handler;
        lock (_sync)
        {
            handler = BackRequested;
            if (!_canNavigateBack || handler is null)
                return false;

            // Block duplicate native Back events until Razor records the new route.
            _canNavigateBack = false;
        }

        handler.Invoke();
        return true;
    }
}
