using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class TeamDetailPage : ContentPage
{
    private readonly TeamDetailViewModel _viewModel;

    public TeamDetailPage(TeamDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshPlayersCommand.ExecuteAsync(null);
    }
}
