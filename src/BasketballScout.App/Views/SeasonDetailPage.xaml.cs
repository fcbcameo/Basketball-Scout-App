using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class SeasonDetailPage : ContentPage
{
    public SeasonDetailPage(SeasonDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
