namespace GymPlanner.Mobile;

public partial class MainPage : ContentPage
{
	private readonly Navigation.MobileBackNavigationService _backNavigation;

	public MainPage(Navigation.MobileBackNavigationService backNavigation)
	{
		InitializeComponent();
		_backNavigation = backNavigation;
		Loaded += ConfigureWebViewZoom;
	}

	private void ConfigureWebViewZoom(object? sender, EventArgs e)
	{
#if ANDROID
		if (blazorWebView.Handler?.PlatformView is Android.Webkit.WebView webView)
		{
			webView.Settings.SetSupportZoom(true);
			webView.Settings.BuiltInZoomControls = true;
			webView.Settings.DisplayZoomControls = false;
		}
#endif
	}

	protected override bool OnBackButtonPressed() =>
		_backNavigation.TryRequestBack() || base.OnBackButtonPressed();
}
