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
		builder.Services.AddSingleton<ExerciseLibrary.ExercisePicker>();
		// Обучение с «Сегодня»: его слой в макете, поэтому одно на всё окно.
		builder.Services.AddScoped<Tutorial.TutorialHost>();
		builder.Services.AddSingleton<Lifecycle.MobileLifecycleService>();
		builder.Services.AddSingleton<Orientation.IScreenOrientationService,
			Orientation.ScreenOrientationService>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<Authentication.IMobileTokenStore,
			Authentication.SecureMobileTokenStore>();
		builder.Services.AddSingleton<Authentication.MobileAuthenticationService>();
		builder.Services.AddSingleton<Authentication.MobileAuthenticationStateProvider>();
		builder.Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
			serviceProvider => serviceProvider.GetRequiredService<Authentication.MobileAuthenticationStateProvider>());
		builder.Services.AddAuthorizationCore();
		// Офлайн-режим: экраны работают с обёртками, которые без связи отдают
		// сохранённую копию; сами HTTP-клиенты остаются внутри них.
		builder.Services.AddSingleton(new Offline.OfflineDocumentStore(
			Path.Combine(FileSystem.AppDataDirectory, "offline")));
		builder.Services.AddSingleton<Offline.ServerReachability>();
		builder.Services.AddSingleton(serviceProvider => new Offline.OutboxSync(
			serviceProvider.GetRequiredService<Offline.OfflineDocumentStore>(),
			serviceProvider.GetRequiredService<HttpClient>(),
			serviceProvider.GetRequiredService<Offline.ServerReachability>(),
			serviceProvider.GetRequiredService<TimeProvider>()));
		builder.Services.AddSingleton<OfflineMode.OutboxResultHandler>();
		builder.Services.AddSingleton<OfflineMode.OfflineRuntime>();
		builder.Services.AddSingleton<OfflineMode.ConnectivityMonitor>();
		builder.Services.AddSingleton<OfflineMode.OfflineStatusService>();
		builder.Services.AddSingleton<OfflineMode.OfflineWarmup>();
		builder.Services.AddSingleton<OfflineMode.ActiveWorkoutSessionStore>();
		builder.Services.AddSingleton<Api.ProfileApiClient>();
		builder.Services.AddSingleton<Api.WorkoutApiClient>();
		builder.Services.AddSingleton<Api.WorkoutLifecycleApiClient>();
		builder.Services.AddSingleton<Api.ProgressApiClient>();
		builder.Services.AddSingleton<Api.WelcomeGuideApiClient>();
		builder.Services.AddSingleton<Api.ExercisePhotoApiClient>();
		builder.Services.AddSingleton<Api.InboxApiClient>();
		builder.Services.AddSingleton<Api.IProfileApiClient, OfflineMode.OfflineProfileApiClient>();
		builder.Services.AddSingleton<Api.IWorkoutApiClient, OfflineMode.OfflineWorkoutApiClient>();
		builder.Services.AddSingleton<Api.IWorkoutLifecycleApiClient,
			OfflineMode.OfflineWorkoutLifecycleApiClient>();
		builder.Services.AddSingleton<Api.IProgressApiClient, OfflineMode.OfflineProgressApiClient>();
		builder.Services.AddSingleton<Api.IWelcomeGuideApiClient, OfflineMode.OfflineWelcomeGuideApiClient>();
		builder.Services.AddSingleton<Api.IExercisePhotoApiClient,
			OfflineMode.OfflineExercisePhotoApiClient>();
		builder.Services.AddSingleton<Api.ISupportApiClient, Api.SupportApiClient>();
		builder.Services.AddSingleton<Api.IInboxApiClient, OfflineMode.OfflineInboxApiClient>();
		builder.Services.AddSingleton<Api.IWatchManagementApiClient,
			Api.WatchManagementApiClient>();
		builder.Services.AddSingleton<Photos.IMobilePhotoPicker, Photos.MauiPhotoPicker>();
		builder.Services.AddSingleton<ExternalLinks.IExternalLinkOpener,
			ExternalLinks.MauiExternalLinkOpener>();
		builder.Services.AddSingleton<Localization.IAppLanguageService,
			Localization.MauiAppLanguageService>();
		// Язык берётся из сервиса, а не из культуры потока: в BlazorWebView
		// культура рендерера фиксируется при старте процесса и на лету не меняется.
		builder.Services.AddSingleton<WorkoutPlanner.Localization.IAppText>(
			serviceProvider =>
			{
				var language = serviceProvider
					.GetRequiredService<Localization.IAppLanguageService>();
				return new WorkoutPlanner.Localization.AppText(() => language.Current);
			});
		builder.Services.AddSingleton<SystemControls.ISystemChoicePicker,
			SystemControls.MauiSystemChoicePicker>();
		builder.Services.AddSingleton<Notifications.NotificationNavigationService>();
		builder.Services.AddSingleton<Notifications.IRemotePushRegistrationService,
			Notifications.RemotePushRegistrationService>();
		builder.Services.AddSingleton<Notifications.IRemotePushTokenProvider,
			Notifications.UnavailableRemotePushTokenProvider>();
#if ANDROID
		builder.Services.AddSingleton<Notifications.IRemotePushTokenProvider,
			Notifications.FirebasePushTokenProvider>();
#endif
		builder.Services.AddSingleton<Diagnostics.UnhandledErrorLogger>();
		builder.Services.AddSingleton<Notifications.PushRegistrationCoordinator>();
		builder.Services.AddSingleton<Notifications.InboxNotificationState>();
		builder.Services.AddSingleton<Notifications.ILocalNotificationPlatform,
			Notifications.PlatformLocalNotificationService>();
		builder.Services.AddSingleton<Notifications.ILocalWorkoutReminderService,
			Notifications.LocalWorkoutReminderService>();
		builder.Services.AddSingleton(serviceProvider =>
		{
			var options = serviceProvider.GetRequiredService<Infrastructure.MobileApiOptions>();
			var languageHandler = new Localization.LanguageHttpMessageHandler(
				serviceProvider.GetRequiredService<Localization.IAppLanguageService>())
			{
				InnerHandler = Infrastructure.MobileHttpMessageHandlerFactory.Create()
			};
			var handler = new Authentication.AuthenticatedHttpMessageHandler(
				serviceProvider.GetRequiredService<Authentication.MobileAuthenticationService>())
			{
				InnerHandler = languageHandler
			};
			// Внешний слой ограничивает время запроса и отмечает, отвечает ли сервер.
			var reachabilityHandler = new Offline.ReachabilityHttpHandler(
				serviceProvider.GetRequiredService<Offline.ServerReachability>())
			{
				InnerHandler = handler
			};
			return new HttpClient(reachabilityHandler)
			{
				BaseAddress = options.BaseAddress,
				Timeout = Timeout.InfiniteTimeSpan
			};
		});

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Подписка ставится до первого рендера: отказ во время запуска тоже
		// должен попасть в журнал, а не исчезнуть молча.
		app.Services.GetRequiredService<Diagnostics.UnhandledErrorLogger>().Attach();

		// Язык надо поднять до первого рендера, иначе первый экран
		// успеет отрисоваться на языке по умолчанию.
		var language = app.Services.GetRequiredService<Localization.IAppLanguageService>();
		language.Initialize();
		Localization.ApiErrorMessages.UseLanguage(() => language.Current);
		// Строки для классов, которые создаёт не DI: платформенные сервисы
		// уведомлений, выбора фото и системных списков.
		Localization.AppTexts.Use(
			app.Services.GetRequiredService<WorkoutPlanner.Localization.IAppText>());
		// Офлайн-режим запускается последним: подкачке данных нужен уже выбранный язык.
		app.Services.GetRequiredService<OfflineMode.ConnectivityMonitor>().Start();
		app.Services.GetRequiredService<Offline.OutboxSync>().ResultHandler =
			app.Services.GetRequiredService<OfflineMode.OutboxResultHandler>();
		app.Services.GetRequiredService<OfflineMode.OfflineWarmup>().Start();

		return app;
	}
}
