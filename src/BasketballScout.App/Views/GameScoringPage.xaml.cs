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

        // Wire court tap on the transparent overlay
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnCourtTapped;
        CourtOverlay.GestureRecognizers.Add(tapGesture);

        // Render shot dots when collection changes
        _vm.ShotChartDots.CollectionChanged += (_, _) => RenderShotDots();

        // React to VM property changes
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GameScoringViewModel.FollowUp):
                RebuildFollowUpButtons();
                if (_vm.FollowUp is not null)
                {
                    FollowUpLabel.Text = _vm.FollowUp.Type == "assist" ? "ASSISTED BY?" : "REBOUND BY?";
                    FollowUpPopup.Stroke = _vm.FollowUp.Type == "assist"
                        ? Color.FromArgb("#4ade8033") : Color.FromArgb("#fbbf2433");
                    FollowUpPopup.BackgroundColor = _vm.FollowUp.Type == "assist"
                        ? Color.FromArgb("#121a12") : Color.FromArgb("#1a1a12");
                }
                UpdateOverlayInputTransparent();
                break;

            case nameof(GameScoringViewModel.PendingShot):
                if (_vm.PendingShot is not null)
                {
                    ZoneLabel.Text = _vm.PendingShot.IsSuggested3Pt ? "3PT ZONE" : "2PT ZONE";
                    ZoneLabel.TextColor = _vm.PendingShot.IsSuggested3Pt
                        ? Color.FromArgb("#fbbf24") : Color.FromArgb("#60a5fa");
                }
                UpdateOverlayInputTransparent();
                break;

            case nameof(GameScoringViewModel.SelectedPlayer):
                UpdatePlayerHighlighting();
                UpdateCourtHint();
                break;
        }
    }

    /// <summary>
    /// When a popup is showing, make the court overlay pass-through so
    /// the popup buttons (and stat bar) can receive taps.
    /// </summary>
    private void UpdateOverlayInputTransparent()
    {
        CourtOverlay.InputTransparent = _vm.PendingShot is not null || _vm.FollowUp is not null;
    }

    /// <summary>
    /// Walk every player card Border in the 4 panels and highlight
    /// the one matching SelectedPlayer with the team color.
    /// </summary>
    private void UpdatePlayerHighlighting()
    {
        var selectedId = _vm.SelectedPlayer?.Id;
        bool isHome = _vm.IsHomeSelected;

        HighlightPanel(HomeOnCourtPanel, selectedId, _vm.HomeTeamColor, isActive: true);
        HighlightPanel(HomeBenchPanel, selectedId, _vm.HomeTeamColor, isActive: false);
        HighlightPanel(AwayOnCourtPanel, selectedId, _vm.AwayTeamColor, isActive: true);
        HighlightPanel(AwayBenchPanel, selectedId, _vm.AwayTeamColor, isActive: false);
    }

    private static void HighlightPanel(Layout panel, int? selectedId, string teamColorHex, bool isActive)
    {
        var teamColor = Color.FromArgb(teamColorHex);

        foreach (var child in panel.Children)
        {
            if (child is not Border border) continue;
            var player = border.BindingContext as Player;
            bool isSelected = player is not null && player.Id == selectedId;

            if (isSelected)
            {
                border.BackgroundColor = teamColor;
                border.Stroke = teamColor;

                // Set text white for selected card
                SetCardTextColors(border, Colors.White, Colors.White, Color.FromArgb("#ffffffaa"));
            }
            else
            {
                border.BackgroundColor = Color.FromArgb("#141414");
                border.Stroke = Color.FromArgb("#1a1a1a");

                // Restore default colors based on active/bench
                var numColor = Color.FromArgb(isActive ? "#bbb" : "#444");
                var nameColor = Color.FromArgb(isActive ? "#bbb" : "#555");
                var posColor = Color.FromArgb(isActive ? "#444" : "#333");
                SetCardTextColors(border, numColor, nameColor, posColor);
            }
        }
    }

    private static void SetCardTextColors(Border border, Color numColor, Color nameColor, Color posColor)
    {
        if (border.Content is not HorizontalStackLayout hsl) return;

        foreach (var item in hsl.Children)
        {
            if (item is Label numLabel && numLabel.FontSize >= 18)
            {
                numLabel.TextColor = numColor;
            }
            else if (item is VerticalStackLayout vsl)
            {
                foreach (var sub in vsl.Children)
                {
                    if (sub is Label lbl)
                    {
                        lbl.TextColor = lbl.FontSize >= 9 ? nameColor : posColor;
                    }
                }
            }
        }
    }

    private void UpdateCourtHint()
    {
        CourtHint.IsVisible = _vm.SelectedPlayer is null
            && _vm.PendingShot is null
            && _vm.ShotChartDots.Count == 0;
    }

    private void OnCourtTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.SelectedPlayer is null || _vm.FollowUp is not null || _vm.PendingShot is not null)
            return;

        var position = e.GetPosition(CourtOverlay);
        if (position is null) return;

        var courtWidth = CourtOverlay.Width;
        var courtHeight = CourtOverlay.Height;
        if (courtWidth <= 0 || courtHeight <= 0) return;

        float x = Math.Clamp((float)(position.Value.X / courtWidth), 0f, 1f);
        float y = Math.Clamp((float)(position.Value.Y / courtHeight), 0f, 1f);

        // 3PT detection matching the JSX mockup logic
        double xPct = x * 100;
        double yPct = y * 100;
        double dx = xPct - 50;
        double dy = yPct - 90;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        bool is3pt = dist > 42 || ((xPct < 12 || xPct > 88) && yPct > 58);

        _vm.CourtTappedCommand.Execute(new ShotPending
        {
            X = x,
            Y = y,
            IsSuggested3Pt = is3pt
        });
    }

    private void RenderShotDots()
    {
        var dotsToRemove = CourtOverlay.Children
            .OfType<Border>()
            .Where(b => b.ClassId == "ShotDot")
            .ToList();
        foreach (var dot in dotsToRemove)
            CourtOverlay.Children.Remove(dot);

        foreach (var shot in _vm.ShotChartDots)
        {
            var madeColor = Color.FromArgb("#4ade80");
            var missColor = Color.FromArgb("#f87171");
            var color = shot.IsMade ? madeColor : missColor;

            var dot = new Border
            {
                ClassId = "ShotDot",
                WidthRequest = shot.IsMade ? 16 : 14,
                HeightRequest = shot.IsMade ? 16 : 14,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                BackgroundColor = shot.IsMade ? Color.FromArgb("#4ade8033") : Colors.Transparent,
                Stroke = color,
                StrokeThickness = 2,
                InputTransparent = true,
                Content = new Label
                {
                    Text = shot.IsMade ? "\u2713" : "\u2717",
                    FontSize = 8,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = color,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            AbsoluteLayout.SetLayoutBounds(dot, new Rect(shot.X, shot.Y,
                AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(dot, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional);
            CourtOverlay.Children.Add(dot);
        }

        UpdateCourtHint();
    }

    private void RebuildFollowUpButtons()
    {
        FollowUpPlayersContainer.Children.Clear();
        if (_vm.FollowUp is null) return;

        var onCourt = _vm.CurrentOnCourt;
        foreach (var p in onCourt)
        {
            if (p.Id == _vm.SelectedPlayer?.Id) continue;

            var player = p;
            var btn = new Button
            {
                Text = $"#{p.JerseyNumber}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#1e1e1e"),
                TextColor = Color.FromArgb("#ccc"),
                BorderColor = Color.FromArgb("#333"),
                BorderWidth = 1,
                CornerRadius = 5,
                Padding = new Thickness(10, 6),
                Margin = new Thickness(0, 0, 3, 3),
                HeightRequest = 34,
                MinimumHeightRequest = 34,
            };
            btn.Clicked += (_, _) => _vm.HandleFollowUpCommand.Execute(player);
            FollowUpPlayersContainer.Children.Add(btn);
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

    private void OnShotCancelClicked(object? sender, TappedEventArgs e)
        => _vm.PendingShot = null;

    // ── Quick stat handlers ──
    private void OnFTMadeClicked(object? sender, EventArgs e)
        => _vm.RecordFreeThrowCommand.Execute(true);

    private void OnFTMissClicked(object? sender, EventArgs e)
        => _vm.RecordFreeThrowCommand.Execute(false);

    private void OnAstClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("ast");

    private void OnStlClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("stl");

    private void OnBlkClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("blk");

    private void OnToClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("to");

    private void OnOrebClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("oreb");

    private void OnDrebClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("dreb");

    private void OnPfClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("pf");

    private void OnTechClicked(object? sender, EventArgs e)
        => _vm.RecordStatCommand.Execute("tech");
}
