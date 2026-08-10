namespace GymPlanner.Mobile;

public partial class App : Application
{
	private readonly MainPage _mainPage;
	private readonly Lifecycle.MobileLifecycleService _lifecycle;

	public App(
		MainPage mainPage,
		Lifecycle.MobileLifecycleService lifecycle)
	{
		InitializeComponent();
		_mainPage = mainPage;
		_lifecycle = lifecycle;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_mainPage) { Title = "GymPlanner" };
		window.Resumed += OnWindowResumed;
		return window;
	}

	private void OnWindowResumed(object? sender, EventArgs args) =>
		_lifecycle.NotifyResumed();
}
