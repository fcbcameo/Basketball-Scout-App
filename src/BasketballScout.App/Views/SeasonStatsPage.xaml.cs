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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Refresh when returning from scoring (a game may have been finished or advanced).
        await _vm.ReloadAsync();
    }
}
