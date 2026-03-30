using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class TeamDetailPage : ContentPage
{
    public TeamDetailPage(TeamDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
