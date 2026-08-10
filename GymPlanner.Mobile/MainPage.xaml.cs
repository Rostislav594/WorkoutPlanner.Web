namespace GymPlanner.Mobile;

public partial class MainPage : ContentPage
{
	private readonly Navigation.MobileBackNavigationService _backNavigation;

	public MainPage(Navigation.MobileBackNavigationService backNavigation)
	{
		InitializeComponent();
		_backNavigation = backNavigation;
	}

	protected override bool OnBackButtonPressed() =>
		_backNavigation.TryRequestBack() || base.OnBackButtonPressed();
}
