using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(SeasonId), "seasonId")]
public partial class SeasonStatsViewModel : ObservableObject
{
    private readonly GameStatsService _statsService;
    private readonly ITeamRepository _teamRepository;

    [ObservableProperty]
    public partial int SeasonId { get; set; }

    [ObservableProperty]
    public partial TeamFilter? SelectedTeamFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBasic))]
    public partial bool ShowAdvanced { get; set; }

    public bool ShowBasic => !ShowAdvanced;

    public ObservableCollection<TeamFilter> TeamFilters { get; } = [];
    public ObservableCollection<GameSummary> Games { get; } = [];
    public ObservableCollection<PlayerSeasonStats> PlayerStats { get; } = [];

    private List<PlayerSeasonStats> _allStats = [];
    private List<SeasonGameSummary> _allGames = [];

    private readonly PdfReportService _pdfService;

    public SeasonStatsViewModel(
        GameStatsService statsService,
        ITeamRepository teamRepository,
        PdfReportService pdfService)
    {
        _statsService = statsService;
        _teamRepository = teamRepository;
        _pdfService = pdfService;
    }

    partial void OnSeasonIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync(value);
    }

    partial void OnSelectedTeamFilterChanged(TeamFilter? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = SelectedTeamFilter is null || SelectedTeamFilter.TeamId == 0
            ? _allStats
            : _allStats.Where(s => s.TeamName == SelectedTeamFilter.TeamName).ToList();

        PlayerStats.Clear();
        foreach (var s in filtered) PlayerStats.Add(s);

        RebuildGamesList();
    }

    /// <summary>
    /// Matches overview (US-6): completed matches only, newest first. When a
    /// specific team is selected in the filter, only that team's matches are
    /// shown, each with a W/L/T badge from that team's perspective.
    /// </summary>
    private void RebuildGamesList()
    {
        int filterTeamId = SelectedTeamFilter?.TeamId ?? 0;

        var games = _allGames.Where(g => g.IsPlayed);
        if (filterTeamId > 0)
            games = games.Where(g => g.HomeTeamId == filterTeamId || g.AwayTeamId == filterTeamId);

        Games.Clear();
        foreach (var g in games)
        {
            string badge = string.Empty, badgeColor = "#888";
            if (filterTeamId > 0)
            {
                int us = g.HomeTeamId == filterTeamId ? g.HomeScore : g.AwayScore;
                int them = g.HomeTeamId == filterTeamId ? g.AwayScore : g.HomeScore;
                (badge, badgeColor) = us > them ? ("W", "#4ade80")
                    : us < them ? ("L", "#f87171")
                    : ("T", "#aaaaaa");
            }

            Games.Add(new GameSummary
            {
                GameId = g.GameId,
                DateDisplay = g.GameDate.ToString("MMM d, yyyy"),
                ScoreDisplay = $"{g.HomeTeamAbbr} {g.HomeScore} — {g.AwayScore} {g.AwayTeamAbbr}",
                ResultBadge = badge,
                ResultColor = badgeColor,
                HasBadge = badge.Length > 0
            });
        }
    }

    private async Task LoadAsync(int seasonId)
    {
        // Per-game summaries (final scores + played state) for the matches overview.
        // Must be set before SelectedTeamFilter, whose setter triggers ApplyFilter.
        _allGames = await _statsService.GetSeasonGameSummariesAsync(seasonId);

        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);

        // Build team filter list
        TeamFilters.Clear();
        TeamFilters.Add(new TeamFilter { TeamId = 0, DisplayName = "All Teams" });
        foreach (var team in teams)
        {
            TeamFilters.Add(new TeamFilter
            {
                TeamId = team.Id,
                TeamName = team.Name,
                DisplayName = team.Name
            });
        }
        SelectedTeamFilter = TeamFilters[0];

        // Load season stats
        _allStats = await _statsService.GetSeasonStatsAsync(seasonId);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ViewGameAsync(GameSummary game)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.GameBoxScorePage)}?gameId={game.GameId}");
    }

    [RelayCommand]
    private async Task ShareGamePdfAsync(GameSummary game)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateGameReportAsync(game.GameId);
            var safeScore = MakeFileSafe(game.ScoreDisplay);
            var safeDate = MakeFileSafe(game.DateDisplay);
            var fileName = $"GameReport_{safeScore}_{safeDate}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, pdfBytes);

            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = $"Game Report — {game.ScoreDisplay}",
                File = new ReadOnlyFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to generate PDF: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task SharePdfAsync()
    {
        try
        {
            // If a specific team is selected (TeamId > 0), the PDF is scoped to that team.
            int? filterTeamId = SelectedTeamFilter is { TeamId: > 0 } tf ? tf.TeamId : null;

            var pdfBytes = await _pdfService.GenerateSeasonReportAsync(SeasonId, filterTeamId);

            var teamSuffix = filterTeamId is not null && SelectedTeamFilter is not null
                ? $"_{MakeFileSafe(SelectedTeamFilter.TeamName)}"
                : string.Empty;
            var fileName = $"SeasonReport_{SeasonId}{teamSuffix}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, pdfBytes);

            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = "Season Report",
                File = new ReadOnlyFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to generate PDF: {ex.Message}", "OK");
        }
    }

    private static string MakeFileSafe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    [RelayCommand]
    private async Task ViewPlayerAsync(PlayerSeasonStats player)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.PlayerStatsPage)}?playerId={player.PlayerId}&seasonId={SeasonId}");
    }
}

public class TeamFilter
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}

public class GameSummary
{
    public int GameId { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string ScoreDisplay { get; set; } = string.Empty;
    /// <summary>"W"/"L"/"T" from the filtered team's perspective; empty when no team filter.</summary>
    public string ResultBadge { get; set; } = string.Empty;
    public string ResultColor { get; set; } = "#888";
    public bool HasBadge { get; set; }
}
