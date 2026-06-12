using BasketballScout.App.ViewModels;

namespace BasketballScout.App.Views;

public partial class GameEditPage : ContentPage
{
    private readonly GameEditViewModel _vm;

    public GameEditPage(GameEditViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = viewModel;
    }

    // Hardware/system back behaves like Cancel, so staged edits aren't silently lost.
    protected override bool OnBackButtonPressed()
    {
        _vm.CancelCommand.Execute(null);
        return true;
    }
}
