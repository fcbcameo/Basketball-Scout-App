using BasketballScout.App.ViewModels;
using BasketballScout.Core.Models;

namespace BasketballScout.App.Views;

public partial class GameScoringPage : ContentPage
{
    private readonly GameScoringViewModel _vm;

    public GameScoringPage(GameScoringViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = _vm;

        // Wire up court tap
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnCourtTapped;
        CourtArea.GestureRecognizers.Add(tapGesture);

        // Subscribe to shot chart changes to render dots
        _vm.ShotChartDots.CollectionChanged += (_, _) => RenderShotDots();

        // Update team indicator label
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameScoringViewModel.IsHomeSelected))
            {
                TeamIndicatorLabel.Text = _vm.IsHomeSelected ? "HOME" : "AWAY";
            }
        };
        TeamIndicatorLabel.Text = "HOME";
    }

    private void OnCourtTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.SelectedPlayer is null || _vm.FollowUp is not null) return;

        var position = e.GetPosition(CourtArea);
        if (position is null) return;

        var courtWidth = CourtArea.Width;
        var courtHeight = CourtArea.Height;

        if (courtWidth <= 0 || courtHeight <= 0) return;

        // Normalize to 0-1 range
        float x = (float)(position.Value.X / courtWidth);
        float y = (float)(position.Value.Y / courtHeight);

        // Clamp
        x = Math.Clamp(x, 0f, 1f);
        y = Math.Clamp(y, 0f, 1f);

        // Auto-suggest 3PT based on distance from basket
        // Basket is roughly at (0.5, 0.06)
        double dx = x - 0.5;
        double dy = y - 0.06;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        bool suggest3 = dist > 0.38; // Approximate 3pt line distance

        _vm.CourtTappedCommand.Execute(new ShotPending
        {
            X = x,
            Y = y,
            IsSuggested3Pt = suggest3
        });
    }

    private void RenderShotDots()
    {
        // Remove existing shot dot labels (keep the court markings)
        var dotsToRemove = CourtArea.Children
            .OfType<Border>()
            .Where(b => b.ClassId == "ShotDot")
            .ToList();

        foreach (var dot in dotsToRemove)
            CourtArea.Children.Remove(dot);

        // Add current dots
        foreach (var shot in _vm.ShotChartDots)
        {
            var dot = new Border
            {
                ClassId = "ShotDot",
                WidthRequest = 20,
                HeightRequest = 20,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                BackgroundColor = shot.IsMade ? Color.FromArgb("#4caf50") : Color.FromArgb("#f44336"),
                Stroke = Colors.White,
                StrokeThickness = 1.5,
                Content = new Label
                {
                    Text = shot.IsMade ? "\u2713" : "\u2717",
                    FontSize = 10,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            AbsoluteLayout.SetLayoutBounds(dot, new Rect(shot.X, shot.Y, 20, 20));
            AbsoluteLayout.SetLayoutFlags(dot, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional);
            CourtArea.Children.Add(dot);
        }
    }

    // ── Shot confirmation handlers ──
    private void On2PtMadeClicked(object? sender, EventArgs e)
        => _vm.ConfirmShotCommand.Execute(new ShotConfirmation { Is3Pt = false, IsMade = true });

    private void On2PtMissClicked(object? sender, EventArgs e)
        => _vm.ConfirmShotCommand.Execute(new ShotConfirmation { Is3Pt = false, IsMade = false });

    private void On3PtMadeClicked(object? sender, EventArgs e)
        => _vm.ConfirmShotCommand.Execute(new ShotConfirmation { Is3Pt = true, IsMade = true });

    private void On3PtMissClicked(object? sender, EventArgs e)
        => _vm.ConfirmShotCommand.Execute(new ShotConfirmation { Is3Pt = true, IsMade = false });

    private void OnShotCancelClicked(object? sender, EventArgs e)
        => _vm.PendingShot = null;

    // ── Quick stat handlers ──
    private void OnFTMadeClicked(object? sender, TappedEventArgs e)
        => _vm.RecordFreeThrowCommand.Execute(true);

    private void OnFTMissClicked(object? sender, TappedEventArgs e)
        => _vm.RecordFreeThrowCommand.Execute(false);

    private void OnAstClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("ast");

    private void OnStlClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("stl");

    private void OnBlkClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("blk");

    private void OnToClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("to");

    private void OnOrebClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("oreb");

    private void OnDrebClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("dreb");

    private void OnPfClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("pf");

    private void OnTechClicked(object? sender, TappedEventArgs e)
        => _vm.RecordStatCommand.Execute("tech");
}
