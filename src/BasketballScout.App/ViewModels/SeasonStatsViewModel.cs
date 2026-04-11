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
    private readonly IGameRepository _gameRepository;
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

    private readonly PdfReportService _pdfService;

    public SeasonStatsViewModel(
        GameStatsService statsService,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        PdfReportService pdfService)
    {
        _statsService = statsService;
        _gameRepository = gameRepository;
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
    }

    private async Task LoadAsync(int seasonId)
    {
        // Load games list
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);
        var teamLookup = teams.ToDictionary(t => t.Id);

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

        Games.Clear();
        foreach (var game in games.OrderByDescending(g => g.GameDate))
        {
            var home = teamLookup.GetValueOrDefault(game.HomeTeamId);
            var away = teamLookup.GetValueOrDefault(game.AwayTeamId);
            Games.Add(new GameSummary
            {
                GameId = game.Id,
                DateDisplay = game.GameDate.ToString("MMM d"),
                ScoreDisplay = $"{home?.Abbreviation ?? "?"} vs {away?.Abbreviation ?? "?"}"
            });
        }

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
    private async Task SharePdfAsync()
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateSeasonReportAsync(SeasonId);
            var fileName = $"SeasonReport_{SeasonId}.pdf";
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
}
