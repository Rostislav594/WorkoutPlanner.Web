using Microsoft.Extensions.Logging;

namespace GymPlanner.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton(
			Infrastructure.MobileApiOptions.CreateDefault(builder.Configuration));
		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddSingleton<Navigation.MobileBackNavigationService>();
		builder.Services.AddSingleton<Lifecycle.MobileLifecycleService>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<Authentication.IMobileTokenStore,
			Authentication.SecureMobileTokenStore>();
		builder.Services.AddSingleton<Authentication.MobileAuthenticationService>();
		builder.Services.AddSingleton<Authentication.MobileAuthenticationStateProvider>();
		builder.Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
			serviceProvider => serviceProvider.GetRequiredService<Authentication.MobileAuthenticationStateProvider>());
		builder.Services.AddAuthorizationCore();
		builder.Services.AddSingleton<Api.IProfileApiClient, Api.ProfileApiClient>();
		builder.Services.AddSingleton<Api.IWorkoutApiClient, Api.WorkoutApiClient>();
		builder.Services.AddSingleton<Api.IWorkoutLifecycleApiClient,
			Api.WorkoutLifecycleApiClient>();
		builder.Services.AddSingleton<Api.IProgressApiClient, Api.ProgressApiClient>();
		builder.Services.AddSingleton<Api.IOnboardingApiClient, Api.OnboardingApiClient>();
		builder.Services.AddSingleton<Api.IExercisePhotoApiClient,
			Api.ExercisePhotoApiClient>();
		builder.Services.AddSingleton<Photos.IMobilePhotoPicker, Photos.MauiPhotoPicker>();
		builder.Services.AddSingleton<Notifications.NotificationNavigationService>();
		builder.Services.AddSingleton<Notifications.ILocalNotificationPlatform,
			Notifications.PlatformLocalNotificationService>();
		builder.Services.AddSingleton<Notifications.ILocalWorkoutReminderService,
			Notifications.LocalWorkoutReminderService>();
		builder.Services.AddSingleton(serviceProvider =>
		{
			var options = serviceProvider.GetRequiredService<Infrastructure.MobileApiOptions>();
			var handler = new Authentication.AuthenticatedHttpMessageHandler(
				serviceProvider.GetRequiredService<Authentication.MobileAuthenticationService>())
			{
				InnerHandler = Infrastructure.MobileHttpMessageHandlerFactory.Create()
			};
			return new HttpClient(handler) { BaseAddress = options.BaseAddress };
		});

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
