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
		builder.Services.AddSingleton(Infrastructure.MobileApiOptions.CreateDefault());
		builder.Services.AddSingleton(TimeProvider.System);
		builder.Services.AddSingleton<Authentication.IMobileTokenStore,
			Authentication.SecureMobileTokenStore>();
		builder.Services.AddSingleton<Authentication.MobileAuthenticationService>();
		builder.Services.AddSingleton<Authentication.MobileAuthenticationStateProvider>();
		builder.Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
			serviceProvider => serviceProvider.GetRequiredService<Authentication.MobileAuthenticationStateProvider>());
		builder.Services.AddAuthorizationCore();
		builder.Services.AddSingleton<Api.IProfileApiClient, Api.ProfileApiClient>();
		builder.Services.AddSingleton(serviceProvider =>
		{
			var options = serviceProvider.GetRequiredService<Infrastructure.MobileApiOptions>();
			var handler = new Authentication.AuthenticatedHttpMessageHandler(
				serviceProvider.GetRequiredService<Authentication.MobileAuthenticationService>())
			{
				InnerHandler = new HttpClientHandler()
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
