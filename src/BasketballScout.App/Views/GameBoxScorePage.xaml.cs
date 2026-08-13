using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class GameBoxScorePage : ContentPage
{
    private readonly GameBoxScoreViewModel _vm;
    private bool _appearedBefore;

    public GameBoxScorePage(GameBoxScoreViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = viewModel;
    }

    // US-33: use OnNavigatedTo (fires on every navigation, incl. returning from a pushed page)
    // rather than OnAppearing, which doesn't reliably fire on back-navigation under Shell.
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // First navigation is loaded by the gameId query property; refresh on
        // subsequent navigations (e.g. returning from the stat editor).
        if (_appearedBefore)
            await _vm.ReloadAsync();
        _appearedBefore = true;
    }
}
