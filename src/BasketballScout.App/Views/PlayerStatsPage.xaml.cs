using BasketballScout.App.ViewModels;
using BasketballScout.Services;

namespace BasketballScout.App.Views;

public partial class PlayerStatsPage : ContentPage
{
    private readonly PlayerStatsViewModel _vm;

    public PlayerStatsPage(PlayerStatsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = _vm;

        _vm.ShotChart.CollectionChanged += (_, _) => RenderShotDots();
    }

    private void RenderShotDots()
    {
        ShotChartOverlay.Children.Clear();

        foreach (var shot in _vm.ShotChart)
        {
            var madeColor = Color.FromArgb("#4ade80");
            var missColor = Color.FromArgb("#f87171");
            var color = shot.IsMade ? madeColor : missColor;

            var dot = new Border
            {
                WidthRequest = shot.IsMade ? 14 : 12,
                HeightRequest = shot.IsMade ? 14 : 12,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
                BackgroundColor = shot.IsMade ? Color.FromArgb("#4ade8033") : Colors.Transparent,
                Stroke = color,
                StrokeThickness = 2,
                InputTransparent = true,
                Content = new Label
                {
                    Text = shot.IsMade ? "\u2713" : "\u2717",
                    FontSize = 7,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = color,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            AbsoluteLayout.SetLayoutBounds(dot, new Rect(shot.X, shot.Y,
                AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(dot, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional);
            ShotChartOverlay.Children.Add(dot);
        }
    }
}
