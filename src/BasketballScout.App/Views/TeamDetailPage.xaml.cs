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

    // US-33: refresh in OnNavigatedTo, not OnAppearing — under Shell, OnAppearing doesn't reliably
    // fire when returning after a pushed route is popped, so a player added on the player page
    // didn't show in the roster until relaunch. OnNavigatedTo fires on every navigation.
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.RefreshPlayersCommand.ExecuteAsync(null);
    }
}
