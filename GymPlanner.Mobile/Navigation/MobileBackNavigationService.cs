namespace GymPlanner.Mobile.Navigation;

public sealed class MobileBackNavigationService
{
    private readonly object _sync = new();
    private bool _canNavigateBack;
    private Action? _backInterceptor;

    public event Action? BackRequested;

    public void SetCanNavigateBack(bool canNavigateBack)
    {
        lock (_sync)
            _canNavigateBack = canNavigateBack;
    }

    public void SetBackInterceptor(Action? backInterceptor)
    {
        lock (_sync)
            _backInterceptor = backInterceptor;
    }

    public bool TryRequestBack()
    {
        Action? interceptor;
        Action? handler;
        lock (_sync)
        {
            interceptor = _backInterceptor;
            if (interceptor is not null)
            {
                handler = null;
            }
            else
            {
                handler = BackRequested;
                if (!_canNavigateBack || handler is null)
                    return false;

                // Block duplicate native Back events until Razor records the new route.
                _canNavigateBack = false;
            }
        }

        (interceptor ?? handler!).Invoke();
        return true;
    }
}
