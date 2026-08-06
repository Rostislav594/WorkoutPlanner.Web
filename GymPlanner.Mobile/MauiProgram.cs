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
		builder.Services.AddSingleton(serviceProvider =>
		{
			var options = serviceProvider.GetRequiredService<Infrastructure.MobileApiOptions>();
			return new HttpClient { BaseAddress = options.BaseAddress };
		});

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
