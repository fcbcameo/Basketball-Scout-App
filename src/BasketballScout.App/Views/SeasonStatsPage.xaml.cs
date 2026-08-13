using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class SeasonStatsPage : ContentPage
{
    private readonly SeasonStatsViewModel _vm;

    public SeasonStatsPage(SeasonStatsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = viewModel;
    }

    // US-33: use OnNavigatedTo (fires on every navigation, incl. returning from a pushed page)
    // rather than OnAppearing, which doesn't reliably fire on back-navigation under Shell.
    // Refresh when returning from scoring (a game may have been finished or advanced).
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _vm.ReloadAsync();
    }
}
