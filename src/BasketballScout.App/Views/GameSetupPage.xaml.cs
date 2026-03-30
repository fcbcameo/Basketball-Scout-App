using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class GameSetupPage : ContentPage
{
    public GameSetupPage(GameSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
