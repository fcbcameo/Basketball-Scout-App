using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class PlayerDetailPage : ContentPage
{
    public PlayerDetailPage(PlayerDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
