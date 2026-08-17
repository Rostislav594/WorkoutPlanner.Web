using Android.App;
using Android.Animation;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.Animations;
using GymPlanner.Mobile.Notifications;
using System.Runtime.Versioning;

namespace GymPlanner.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		if (OperatingSystem.IsAndroidVersionAtLeast(31))
			ConfigureSplashAnimation();

		OpenNotificationRoute(Intent);
	}

	[SupportedOSPlatform("android31.0")]
	private void ConfigureSplashAnimation() =>
		SplashScreen.SetOnExitAnimationListener(new SplashExitAnimationListener());

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

	[SupportedOSPlatform("android31.0")]
	private sealed class SplashExitAnimationListener : Java.Lang.Object,
		Android.Window.ISplashScreenOnExitAnimationListener
	{
		public void OnSplashScreenExit(Android.Window.SplashScreenView splashScreenView)
		{
			var icon = splashScreenView.IconView;
			if (icon is null)
			{
				splashScreenView.Remove();
				return;
			}

			var scaleX = ObjectAnimator.OfFloat(
				icon,
				"scaleX",
				1f,
				1.065f,
				0.99f,
				1.04f,
				1f);
			var scaleY = ObjectAnimator.OfFloat(
				icon,
				"scaleY",
				1f,
				1.065f,
				0.99f,
				1.04f,
				1f);
			var fade = ObjectAnimator.OfFloat(
				splashScreenView,
				"alpha",
				1f,
				1f,
				1f,
				0f);
			if (scaleX is null || scaleY is null || fade is null)
			{
				splashScreenView.Remove();
				return;
			}

			var animation = new AnimatorSet();
			animation.PlayTogether(scaleX, scaleY, fade);
			animation.SetDuration(1050);
			animation.SetInterpolator(new AccelerateDecelerateInterpolator());
			animation.AnimationEnd += (_, _) => splashScreenView.Remove();
			animation.Start();
		}
	}
}
