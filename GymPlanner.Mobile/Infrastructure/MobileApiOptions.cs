namespace GymPlanner.Mobile.Infrastructure;

public sealed record MobileApiOptions(Uri BaseAddress)
{
    public static MobileApiOptions CreateDefault()
    {
#if ANDROID
        return new(new Uri("https://10.0.2.2:7196/", UriKind.Absolute));
#else
        return new(new Uri("https://localhost:7196/", UriKind.Absolute));
#endif
    }
}
