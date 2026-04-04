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
    public partial int GamesPlayed { get; set; }

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
        }

        // Load shot chart
        var shots = await _statsService.GetPlayerShotChartAsync(PlayerId, SeasonId);
        ShotChart.Clear();
        foreach (var shot in shots) ShotChart.Add(shot);
    }
}
