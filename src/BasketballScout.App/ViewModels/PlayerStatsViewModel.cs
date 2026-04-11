using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(PlayerId), "playerId")]
[QueryProperty(nameof(SeasonId), "seasonId")]
public partial class PlayerStatsViewModel : ObservableObject
{
    private readonly GameStatsService _statsService;
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    [ObservableProperty]
    public partial int PlayerId { get; set; }

    [ObservableProperty]
    public partial int SeasonId { get; set; }

    [ObservableProperty]
    public partial string PlayerName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int JerseyNumber { get; set; }

    [ObservableProperty]
    public partial string PositionDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TeamColor { get; set; } = "#555";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBasic))]
    public partial bool ShowAdvanced { get; set; }

    public bool ShowBasic => !ShowAdvanced;

    [ObservableProperty]
    public partial int GamesPlayed { get; set; }

    // ── Basic per-game ──
    [ObservableProperty]
    public partial string Ppg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Rpg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Apg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Spg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Bpg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Topg { get; set; } = "0.0";

    [ObservableProperty]
    public partial double FgPct { get; set; }

    [ObservableProperty]
    public partial double Fg3Pct { get; set; }

    [ObservableProperty]
    public partial double FtPct { get; set; }

    [ObservableProperty]
    public partial string FgDisplay { get; set; } = "0/0";

    [ObservableProperty]
    public partial string Fg3Display { get; set; } = "0/0";

    [ObservableProperty]
    public partial string FtDisplay { get; set; } = "0/0";

    // ── Advanced per-game ──
    [ObservableProperty]
    public partial string Mpg { get; set; } = "—";

    [ObservableProperty]
    public partial string Orpg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Drpg { get; set; } = "0.0";

    [ObservableProperty]
    public partial double Fg2Pct { get; set; }

    [ObservableProperty]
    public partial string FgmPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string FgaPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Fg2MPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Fg2APg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Fg3MPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string Fg3APg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string FtmPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string FtaPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial double EFgPct { get; set; }

    [ObservableProperty]
    public partial double TsPct { get; set; }

    [ObservableProperty]
    public partial string TsaPg { get; set; } = "0.0";

    [ObservableProperty]
    public partial string AtDisplay { get; set; } = "0.00";

    [ObservableProperty]
    public partial string EffDisplay { get; set; } = "0.0";

    [ObservableProperty]
    public partial string GmScDisplay { get; set; } = "0.0";

    [ObservableProperty]
    public partial string PlusMinusDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial string TotalMinutesDisplay { get; set; } = "—";

    public ObservableCollection<ShotChartPoint> ShotChart { get; } = [];

    private bool _playerLoaded;
    private bool _seasonLoaded;

    public PlayerStatsViewModel(
        GameStatsService statsService,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository)
    {
        _statsService = statsService;
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
    }

    partial void OnPlayerIdChanged(int value)
    {
        _playerLoaded = value > 0;
        TryLoad();
    }

    partial void OnSeasonIdChanged(int value)
    {
        _seasonLoaded = value > 0;
        TryLoad();
    }

    private void TryLoad()
    {
        if (_playerLoaded && _seasonLoaded)
            _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var player = await _playerRepository.GetByIdAsync(PlayerId);
        if (player is null) return;

        var team = await _teamRepository.GetByIdAsync(player.TeamId);

        PlayerName = player.Name;
        JerseyNumber = player.JerseyNumber;
        PositionDisplay = player.Position.ToString();
        TeamName = team?.Name ?? "";
        TeamColor = team?.Color ?? "#555";

        // Get season stats for this player
        var allStats = await _statsService.GetSeasonStatsAsync(SeasonId);
        var ps = allStats.FirstOrDefault(s => s.PlayerId == PlayerId);

        if (ps is not null)
        {
            GamesPlayed = ps.GamesPlayed;
            Ppg = ps.Ppg;
            Rpg = ps.Rpg;
            Apg = ps.Apg;
            Spg = ps.Spg;
            Bpg = ps.Bpg;
            Topg = ps.Topg;
            FgPct = ps.FgPct;
            Fg3Pct = ps.Fg3Pct;
            FtPct = ps.FtPct;
            FgDisplay = $"{ps.FgMade}/{ps.FgAttempted}";
            Fg3Display = $"{ps.Fg3Made}/{ps.Fg3Attempted}";
            FtDisplay = $"{ps.FtMade}/{ps.FtAttempted}";

            // Advanced
            Mpg = ps.MpgDisplay;
            Orpg = ps.Orpg;
            Drpg = ps.Drpg;
            Fg2Pct = ps.Fg2Pct;
            FgmPg = ps.FgmPg;
            FgaPg = ps.FgaPg;
            Fg2MPg = ps.Fg2MPg;
            Fg2APg = ps.Fg2APg;
            Fg3MPg = ps.Fg3MPg;
            Fg3APg = ps.Fg3APg;
            FtmPg = ps.FtmPg;
            FtaPg = ps.FtaPg;
            EFgPct = ps.EFgPct;
            TsPct = ps.TsPct;
            TsaPg = ps.TsaPg;
            AtDisplay = ps.AtDisplay;
            EffDisplay = ps.EffDisplay;
            GmScDisplay = ps.GmScDisplay;
            PlusMinusDisplay = ps.PlusMinusDisplay;
            TotalMinutesDisplay = ps.TotalMinutesDisplay;
        }

        // Load shot chart
        var shots = await _statsService.GetPlayerShotChartAsync(PlayerId, SeasonId);
        ShotChart.Clear();
        foreach (var shot in shots) ShotChart.Add(shot);
    }
}
