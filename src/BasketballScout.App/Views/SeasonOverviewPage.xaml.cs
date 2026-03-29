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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadSeasonsCommand.ExecuteAsync(null);
    }
}
