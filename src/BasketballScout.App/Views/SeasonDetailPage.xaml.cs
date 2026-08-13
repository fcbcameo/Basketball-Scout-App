using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class SeasonDetailPage : ContentPage
{
    private readonly SeasonDetailViewModel _viewModel;

    public SeasonDetailPage(SeasonDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // US-33: refresh in OnNavigatedTo, not OnAppearing — under Shell, OnAppearing doesn't reliably
    // fire when returning after a pushed route is popped, so a team added on the team page didn't
    // show until relaunch. OnNavigatedTo fires on every navigation (initial and back).
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.RefreshTeamsCommand.ExecuteAsync(null);
    }
}
