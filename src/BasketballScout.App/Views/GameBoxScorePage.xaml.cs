using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class GameBoxScorePage : ContentPage
{
    public GameBoxScorePage(GameBoxScoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
