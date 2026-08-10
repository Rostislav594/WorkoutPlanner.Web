using Foundation;

using GymPlanner.Mobile.Notifications;
using UIKit;
using UserNotifications;

namespace GymPlanner.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	private readonly WorkoutNotificationDelegate _notificationDelegate = new();

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(
		UIApplication application,
		NSDictionary? launchOptions)
	{
		var launched = base.FinishedLaunching(application, launchOptions);
		UNUserNotificationCenter.Current.Delegate = _notificationDelegate;
		return launched;
	}
}
