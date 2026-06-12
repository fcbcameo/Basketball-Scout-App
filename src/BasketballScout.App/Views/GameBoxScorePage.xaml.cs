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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // First appearance is loaded by the gameId query property; refresh on
        // subsequent appearances (e.g. returning from the stat editor).
        if (_appearedBefore)
            await _vm.ReloadAsync();
        _appearedBefore = true;
    }
}
