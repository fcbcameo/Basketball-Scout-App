using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class SeasonOverviewPage : ContentPage
{
    private readonly SeasonOverviewViewModel _viewModel;

    public SeasonOverviewPage(SeasonOverviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // US-33: refresh in OnNavigatedTo, not OnAppearing. Under Shell, OnAppearing fires on the
    // initial load but not reliably when returning to a page after a pushed route is popped, so
    // seasons added on a child page didn't show until an app relaunch. OnNavigatedTo fires on
    // every navigation — initial and back — so the list is always current.
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.LoadSeasonsCommand.ExecuteAsync(null);
    }
}
