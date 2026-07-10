using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(SeasonId), "seasonId")]
public partial class GameSetupViewModel : ObservableObject
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IGameRepository _gameRepository;

    [ObservableProperty]
    public partial int SeasonId { get; set; }

    [ObservableProperty]
    public partial Team? SelectedHomeTeam { get; set; }

    [ObservableProperty]
    public partial Team? SelectedAwayTeam { get; set; }

    [ObservableProperty]
    public partial string Location { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime GameDate { get; set; } = DateTime.Today;

    public ObservableCollection<Team> Teams { get; } = new();
    public ObservableCollection<Player> HomeActivePlayers { get; } = new();
    public ObservableCollection<Player> HomeBenchPlayers { get; } = new();
    public ObservableCollection<Player> AwayActivePlayers { get; } = new();
    public ObservableCollection<Player> AwayBenchPlayers { get; } = new();

    public GameSetupViewModel(
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        IGameRepository gameRepository)
    {
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _gameRepository = gameRepository;
    }

    partial void OnSeasonIdChanged(int value)
    {
        if (value > 0)
            _ = LoadTeamsAsync();
    }

    private async Task LoadTeamsAsync()
    {
        var teams = await _teamRepository.GetBySeasonIdAsync(SeasonId);
        Teams.Clear();
        foreach (var team in teams)
            Teams.Add(team);
    }

    partial void OnSelectedHomeTeamChanged(Team? value)
    {
        HomeActivePlayers.Clear();
        HomeBenchPlayers.Clear();
        if (value?.Players is null) return;

        foreach (var p in value.Players.Where(p => p.IsActive).OrderBy(p => p.JerseyNumber))
            HomeActivePlayers.Add(p);
        foreach (var p in value.Players.Where(p => !p.IsActive).OrderBy(p => p.JerseyNumber))
            HomeBenchPlayers.Add(p);
    }

    partial void OnSelectedAwayTeamChanged(Team? value)
    {
        AwayActivePlayers.Clear();
        AwayBenchPlayers.Clear();
        if (value?.Players is null) return;

        foreach (var p in value.Players.Where(p => p.IsActive).OrderBy(p => p.JerseyNumber))
            AwayActivePlayers.Add(p);
        foreach (var p in value.Players.Where(p => !p.IsActive).OrderBy(p => p.JerseyNumber))
            AwayBenchPlayers.Add(p);
    }

    [RelayCommand]
    private void ToggleHomePlayer(Player player)
    {
        if (HomeActivePlayers.Contains(player))
        {
            HomeActivePlayers.Remove(player);
            HomeBenchPlayers.Add(player);
        }
        else
        {
            if (HomeActivePlayers.Count >= 5)
            {
                Shell.Current.DisplayAlertAsync("Lineup Full", "Remove a starter first (tap to bench them).", "OK");
                return;
            }
            HomeBenchPlayers.Remove(player);
            HomeActivePlayers.Add(player);
        }
    }

    [RelayCommand]
    private void ToggleAwayPlayer(Player player)
    {
        if (AwayActivePlayers.Contains(player))
        {
            AwayActivePlayers.Remove(player);
            AwayBenchPlayers.Add(player);
        }
        else
        {
            if (AwayActivePlayers.Count >= 5)
            {
                Shell.Current.DisplayAlertAsync("Lineup Full", "Remove a starter first (tap to bench them).", "OK");
                return;
            }
            AwayBenchPlayers.Remove(player);
            AwayActivePlayers.Add(player);
        }
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (SelectedHomeTeam is null || SelectedAwayTeam is null)
        {
            await Shell.Current.DisplayAlertAsync("Select Teams", "Pick both a home and away team.", "OK");
            return;
        }

        if (SelectedHomeTeam.Id == SelectedAwayTeam.Id)
        {
            await Shell.Current.DisplayAlertAsync("Invalid", "Home and away team must be different.", "OK");
            return;
        }

        if (HomeActivePlayers.Count < 1 || AwayActivePlayers.Count < 1)
        {
            await Shell.Current.DisplayAlertAsync("Lineup", "Each team needs at least 1 active player.", "OK");
            return;
        }

        // Snapshot the season's game format onto the game (US-21), so later season edits
        // never change this game's clock or minutes math.
        var season = await _seasonRepository.GetByIdAsync(SeasonId);
        int periodMinutes = season is { PeriodLengthMinutes: > 0 } ? season.PeriodLengthMinutes : 10;
        int periods = season is { PeriodCount: > 0 } ? season.PeriodCount : 4;
        int otMinutes = season is { OvertimeLengthMinutes: > 0 } ? season.OvertimeLengthMinutes : 5;

        var game = new Game
        {
            SeasonId = SeasonId,
            HomeTeamId = SelectedHomeTeam.Id,
            AwayTeamId = SelectedAwayTeam.Id,
            GameDate = GameDate,
            Location = Location.Trim(),
            ExportGuid = Guid.NewGuid().ToString(), // stable identity for export/duplicate detection (US-19)
            PeriodLengthSeconds = periodMinutes * 60,
            OvertimeLengthSeconds = otMinutes * 60,
            RegulationPeriods = periods
        };

        var created = await _gameRepository.AddAsync(game);

        // Pass active player IDs as comma-separated strings
        var homeIds = string.Join(",", HomeActivePlayers.Select(p => p.Id));
        var awayIds = string.Join(",", AwayActivePlayers.Select(p => p.Id));

        await Shell.Current.GoToAsync(
            $"{nameof(Views.GameScoringPage)}?gameId={created.Id}&homeActiveIds={homeIds}&awayActiveIds={awayIds}");
    }
}
