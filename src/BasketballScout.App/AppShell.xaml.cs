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
		Routing.RegisterRoute(nameof(GameSetupPage), typeof(GameSetupPage));
		Routing.RegisterRoute(nameof(GameScoringPage), typeof(GameScoringPage));
	}
}
