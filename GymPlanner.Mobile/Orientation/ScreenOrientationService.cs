using Microsoft.Maui.ApplicationModel;

#if ANDROID
using Android.Content.PM;
#endif

namespace GymPlanner.Mobile.Orientation;

public sealed class ScreenOrientationService : IScreenOrientationService
{
    public void LockLandscape() => Apply(landscape: true);

    public void RestoreAutomaticOrientation() => Apply(landscape: false);

    private static void Apply(bool landscape)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var activity = Platform.CurrentActivity;
            if (activity is not null)
            {
                activity.RequestedOrientation = landscape
                    ? ScreenOrientation.SensorLandscape
                    : ScreenOrientation.Unspecified;
            }
        });
#endif
    }
}
