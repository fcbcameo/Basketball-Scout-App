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
		Routing.RegisterRoute(nameof(GameBoxScorePage), typeof(GameBoxScorePage));
		Routing.RegisterRoute(nameof(SeasonStatsPage), typeof(SeasonStatsPage));
		Routing.RegisterRoute(nameof(PlayerStatsPage), typeof(PlayerStatsPage));
		Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
	}
}
