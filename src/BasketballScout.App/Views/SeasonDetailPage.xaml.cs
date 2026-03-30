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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshTeamsCommand.ExecuteAsync(null);
    }
}
