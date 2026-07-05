using System.Collections.ObjectModel;
using BasketballScout.Core.Enums;
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
    private readonly IGameRepository _gameRepository;

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
    private readonly ImportExportService _importExportService;

    public SeasonStatsViewModel(
        GameStatsService statsService,
        ITeamRepository teamRepository,
        IGameRepository gameRepository,
        PdfReportService pdfService,
        ImportExportService importExportService)
    {
        _statsService = statsService;
        _teamRepository = teamRepository;
        _gameRepository = gameRepository;
        _pdfService = pdfService;
        _importExportService = importExportService;
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
    /// Matches overview (US-6 + US-10). In-progress games are listed first with a
    /// Resume affordance, then completed matches (newest first) with a W/L/T badge
    /// from the filtered team's perspective. The team filter scopes both groups.
    /// </summary>
    private void RebuildGamesList()
    {
        int filterTeamId = SelectedTeamFilter?.TeamId ?? 0;

        bool InFilter(SeasonGameSummary g) =>
            filterTeamId == 0 || g.HomeTeamId == filterTeamId || g.AwayTeamId == filterTeamId;

        // In-progress (resumable): started but not finished. _allGames is already newest-first.
        var inProgress = _allGames.Where(g => g.Status == GameStatus.InProgress && g.HasEvents && InFilter(g));
        var finished = _allGames.Where(g => g.IsPlayed && InFilter(g));

        Games.Clear();

        foreach (var g in inProgress)
        {
            Games.Add(new GameSummary
            {
                GameId = g.GameId,
                DateDisplay = g.GameDate.ToString("MMM d, yyyy"),
                ScoreDisplay = $"{g.HomeTeamAbbr} {g.HomeScore} — {g.AwayScore} {g.AwayTeamAbbr}",
                IsInProgress = true
            });
        }

        foreach (var g in finished)
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

    private bool _isLoading;

    /// <summary>Reloads when returning to the page (e.g. after finishing or resuming a game),
    /// so the matches list reflects the latest status. No-op until first loaded.</summary>
    public Task ReloadAsync() => SeasonId > 0 ? LoadAsync(SeasonId) : Task.CompletedTask;

    private async Task LoadAsync(int seasonId)
    {
        if (_isLoading) return; // guard against the query-property + OnAppearing double-trigger
        _isLoading = true;
        try
        {
            // Per-game summaries (final scores + status) for the matches overview.
            // Must be set before SelectedTeamFilter, whose setter triggers ApplyFilter.
            _allGames = await _statsService.GetSeasonGameSummariesAsync(seasonId);

            var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);

            // Build team filter list, preserving the current selection across reloads.
            int previousFilterId = SelectedTeamFilter?.TeamId ?? 0;
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
            SelectedTeamFilter = TeamFilters.FirstOrDefault(f => f.TeamId == previousFilterId) ?? TeamFilters[0];

            // Load season stats
            _allStats = await _statsService.GetSeasonStatsAsync(seasonId);
            ApplyFilter();
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task ViewGameAsync(GameSummary game)
    {
        // Resume an in-progress game in the scoring screen; otherwise open the box score.
        if (game.IsInProgress)
            await Shell.Current.GoToAsync($"{nameof(Views.GameScoringPage)}?gameId={game.GameId}&resume=1");
        else
            await Shell.Current.GoToAsync($"{nameof(Views.GameBoxScorePage)}?gameId={game.GameId}");
    }

    /// <summary>US-12: delete a single match after a confirmation naming it. The delete
    /// removes the game with its stat events and quarter scores; season, teams and
    /// players are untouched. In-progress games get an extra warning line.</summary>
    [RelayCommand]
    private async Task DeleteGameAsync(GameSummary game)
    {
        string progressNote = game.IsInProgress ? "This match is still IN PROGRESS.\n\n" : string.Empty;
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Match",
            $"{progressNote}Permanently delete {game.ScoreDisplay} ({game.DateDisplay}) and all its stats?",
            "Delete", "Cancel");
        if (!confirm) return;

        await _gameRepository.DeleteAsync(game.GameId);
        await ReloadAsync();
    }

    /// <summary>US-14: import a single-game JSON bundle into THIS season (the one being
    /// viewed). Teams and players are matched to existing ones by name or created, so an
    /// import never duplicates a roster or fails on an already-present team/player.</summary>
    [RelayCommand]
    private async Task ImportGameAsync()
    {
        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a game file (.json)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.macOS, new[] { "public.json" } },
                })
            });
            if (pick is null) return; // cancelled

            var json = await File.ReadAllTextAsync(pick.FullPath);

            // Preview before committing, and flag a re-import of the same game.
            var preview = await _importExportService.AnalyzeGameImportAsync(json, SeasonId);
            string dupLine = preview.IsDuplicate
                ? "\n\n⚠ This game was already imported into this season."
                : string.Empty;
            bool proceed = await Shell.Current.DisplayAlertAsync(
                preview.IsDuplicate ? "Already Imported" : "Import Game?",
                $"This will add 1 game to this season.\n\n" +
                $"Teams: {preview.TeamsMatched} matched, {preview.TeamsToCreate} new\n" +
                $"Players: {preview.PlayersMatched} matched, {preview.PlayersToCreate} new\n" +
                $"Stat events: {preview.EventCount}{dupLine}",
                preview.IsDuplicate ? "Import Anyway" : "Import", "Cancel");
            if (!proceed) return;

            var r = await _importExportService.ImportGameAsync(json, SeasonId);

            await ReloadAsync();
            await Shell.Current.DisplayAlertAsync(
                "Game Imported",
                $"The game was added to this season.\n\n" +
                $"Teams: {r.TeamsMatched} matched, {r.TeamsCreated} new\n" +
                $"Players: {r.PlayersMatched} matched, {r.PlayersCreated} new\n" +
                $"Stat events: {r.EventsImported}",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
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

    /// <summary>True for a started-but-unfinished game: tapping it resumes scoring (US-10).</summary>
    public bool IsInProgress { get; set; }
    /// <summary>Completed games offer a PDF export; in-progress ones don't.</summary>
    public bool ShowPdf => !IsInProgress;
}
