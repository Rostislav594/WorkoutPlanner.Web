using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using GymPlanner.Mobile.Notifications;

namespace GymPlanner.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		OpenNotificationRoute(Intent);
	}

	protected override void OnNewIntent(Android.Content.Intent? intent)
	{
		base.OnNewIntent(intent);
		OpenNotificationRoute(intent);
	}

	private static void OpenNotificationRoute(Android.Content.Intent? intent)
	{
		var route = intent?.GetStringExtra(AndroidNotificationSupport.RouteExtra);
		if (string.IsNullOrWhiteSpace(route))
			return;

		IPlatformApplication.Current?.Services
			.GetService<NotificationNavigationService>()?
			.Open(route);
	}
}
