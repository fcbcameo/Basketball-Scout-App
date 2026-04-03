using System.Collections.ObjectModel;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(GameId), "gameId")]
public partial class GameBoxScoreViewModel : ObservableObject
{
    private readonly GameStatsService _statsService;

    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial string HomeTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AwayTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ScoreDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GameDateDisplay { get; set; } = string.Empty;

    public ObservableCollection<PlayerBoxLine> HomeLines { get; } = [];
    public ObservableCollection<PlayerBoxLine> AwayLines { get; } = [];

    public GameBoxScoreViewModel(GameStatsService statsService)
    {
        _statsService = statsService;
    }

    partial void OnGameIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync(value);
    }

    private async Task LoadAsync(int gameId)
    {
        var box = await _statsService.GetGameBoxScoreAsync(gameId);

        HomeTeamName = box.HomeTeamName;
        AwayTeamName = box.AwayTeamName;
        ScoreDisplay = $"{box.HomeTeamAbbr} {box.HomeScore} - {box.AwayScore} {box.AwayTeamAbbr}";
        GameDateDisplay = box.GameDate.ToString("MMM d, yyyy");

        HomeLines.Clear();
        foreach (var line in box.HomeLines) HomeLines.Add(line);

        AwayLines.Clear();
        foreach (var line in box.AwayLines) AwayLines.Add(line);
    }
}
