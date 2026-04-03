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

    public ObservableCollection<GameSummary> Games { get; } = [];
    public ObservableCollection<PlayerSeasonStats> PlayerStats { get; } = [];

    public SeasonStatsViewModel(
        GameStatsService statsService,
        IGameRepository gameRepository,
        ITeamRepository teamRepository)
    {
        _statsService = statsService;
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
    }

    partial void OnSeasonIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync(value);
    }

    private async Task LoadAsync(int seasonId)
    {
        // Load games list
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);
        var teamLookup = teams.ToDictionary(t => t.Id);

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
        var stats = await _statsService.GetSeasonStatsAsync(seasonId);
        PlayerStats.Clear();
        foreach (var s in stats) PlayerStats.Add(s);
    }

    [RelayCommand]
    private async Task ViewGameAsync(GameSummary game)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.GameBoxScorePage)}?gameId={game.GameId}");
    }

    [RelayCommand]
    private async Task ViewPlayerAsync(PlayerSeasonStats player)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.PlayerStatsPage)}?playerId={player.PlayerId}&seasonId={SeasonId}");
    }
}

public class GameSummary
{
    public int GameId { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string ScoreDisplay { get; set; } = string.Empty;
}
