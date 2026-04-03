using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class SeasonStatsPage : ContentPage
{
    public SeasonStatsPage(SeasonStatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
