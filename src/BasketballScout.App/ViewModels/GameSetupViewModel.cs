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

    // US-42: IsActive means "available for matches". Only available players take part, and
    // they all start on the bench — the scorer picks the starters fresh for every match.
    partial void OnSelectedHomeTeamChanged(Team? value) =>
        LoadAvailablePlayers(value, HomeActivePlayers, HomeBenchPlayers);

    partial void OnSelectedAwayTeamChanged(Team? value) =>
        LoadAvailablePlayers(value, AwayActivePlayers, AwayBenchPlayers);

    private static void LoadAvailablePlayers(
        Team? team, ObservableCollection<Player> starters, ObservableCollection<Player> bench)
    {
        starters.Clear();
        bench.Clear();
        if (team?.Players is null) return;

        foreach (var p in team.Players.Where(p => p.IsActive).OrderBy(p => p.JerseyNumber))
            bench.Add(p);
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
            await Shell.Current.DisplayAlertAsync("Lineup",
                "Each team needs at least 1 starter. Tap a bench player to move them to STARTERS.\n\n" +
                "Missing a player? Mark them Available in the team roster.", "OK");
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
            RegulationPeriods = periods,
            // US-42: who is present for this match (starters + bench), fixed for its lifetime so
            // later availability edits never change a game's roster — and resume can rebuild the
            // bench, since bench players who never sub in have no events of their own.
            HomeRosterIds = Game.FormatRosterIds(HomeActivePlayers.Concat(HomeBenchPlayers).Select(p => p.Id)),
            AwayRosterIds = Game.FormatRosterIds(AwayActivePlayers.Concat(AwayBenchPlayers).Select(p => p.Id))
        };

        var created = await _gameRepository.AddAsync(game);

        // Pass active player IDs as comma-separated strings
        var homeIds = string.Join(",", HomeActivePlayers.Select(p => p.Id));
        var awayIds = string.Join(",", AwayActivePlayers.Select(p => p.Id));

        await Shell.Current.GoToAsync(
            $"{nameof(Views.GameScoringPage)}?gameId={created.Id}&homeActiveIds={homeIds}&awayActiveIds={awayIds}");
    }
}
