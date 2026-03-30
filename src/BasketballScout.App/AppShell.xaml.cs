using BasketballScout.App.Views;

namespace BasketballScout.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute(nameof(SeasonDetailPage), typeof(SeasonDetailPage));
		Routing.RegisterRoute(nameof(TeamDetailPage), typeof(TeamDetailPage));
		Routing.RegisterRoute(nameof(PlayerDetailPage), typeof(PlayerDetailPage));
	}
}
