using System.Collections.ObjectModel;
using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

/// <summary>
/// US-11 — simple stat editor for a finished game. Pick a player, adjust any stat
/// counter with +/−. Changes are staged in memory and only persisted on Save, so
/// Cancel discards everything. Shot locations are never editable: removing a shot
/// removes its event (and chart dot); added shots are recorded without a location.
/// The game's Finished status is never touched.
/// </summary>
[QueryProperty(nameof(GameId), "gameId")]
public partial class GameEditViewModel : ObservableObject
{
    private readonly IGameRepository _gameRepository;
    private readonly IStatEventRepository _statEventRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;

    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial string ScoreDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HomeTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AwayTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingSummary))]
    public partial int PendingCount { get; set; }

    public string PendingSummary => PendingCount == 0
        ? "No changes"
        : $"{PendingCount} unsaved change{(PendingCount == 1 ? "" : "s")}";

    [ObservableProperty]
    public partial PlayerChip? SelectedChip { get; set; }

    public ObservableCollection<PlayerChip> HomeChips { get; } = [];
    public ObservableCollection<PlayerChip> AwayChips { get; } = [];
    public ObservableCollection<StatCounterRow> StatRows { get; } = [];

    private Game? _game;
    private List<Player> _allHomePlayers = [];
    private List<Player> _allAwayPlayers = [];
    private List<StatEvent> _events = [];

    // Staged edits — applied to the database only on Save.
    private readonly List<StatEvent> _pendingAdds = [];
    private readonly HashSet<int> _pendingRemoveIds = [];

    public GameEditViewModel(
        IGameRepository gameRepository,
        IStatEventRepository statEventRepository,
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository)
    {
        _gameRepository = gameRepository;
        _statEventRepository = statEventRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
    }

    partial void OnGameIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync(value);
    }

    private async Task LoadAsync(int id)
    {
        _game = await _gameRepository.GetByIdAsync(id);
        if (_game is null) return;

        var homeTeam = await _teamRepository.GetByIdAsync(_game.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(_game.AwayTeamId);
        HomeTeamName = homeTeam?.Name ?? "Home";
        AwayTeamName = awayTeam?.Name ?? "Away";
        _homeAbbr = homeTeam?.Abbreviation ?? "HOM";
        _awayAbbr = awayTeam?.Abbreviation ?? "AWY";

        _allHomePlayers = (await _playerRepository.GetByTeamIdAsync(_game.HomeTeamId)).ToList();
        _allAwayPlayers = (await _playerRepository.GetByTeamIdAsync(_game.AwayTeamId)).ToList();

        _events = (await _statEventRepository.GetByGameIdAsync(id)).ToList();

        HomeChips.Clear();
        foreach (var p in _allHomePlayers.OrderBy(p => p.JerseyNumber))
            HomeChips.Add(new PlayerChip { Player = p, Label = $"#{p.JerseyNumber} {p.Name}", TeamColor = homeTeam?.Color ?? "#e85d26" });

        AwayChips.Clear();
        foreach (var p in _allAwayPlayers.OrderBy(p => p.JerseyNumber))
            AwayChips.Add(new PlayerChip { Player = p, Label = $"#{p.JerseyNumber} {p.Name}", TeamColor = awayTeam?.Color ?? "#2d7dd2" });

        RecomputeScore();

        // Pre-select the first home player so the editor never opens empty.
        var first = HomeChips.FirstOrDefault() ?? AwayChips.FirstOrDefault();
        if (first is not null) SelectPlayer(first);
    }

    private string _homeAbbr = "HOM";
    private string _awayAbbr = "AWY";

    // ── Player selection ──
    [RelayCommand]
    private void SelectPlayer(PlayerChip chip)
    {
        if (SelectedChip is not null) SelectedChip.IsSelected = false;
        SelectedChip = chip;
        chip.IsSelected = true;
        BuildStatRows(chip.Player.Id);
    }

    /// <summary>The 14 editable stat categories, in scoring-screen order.</summary>
    private static readonly (StatType Type, ShotResult? Result, string Label)[] Categories =
    [
        (StatType.Points2, ShotResult.Made, "2PT Made"),
        (StatType.Points2, ShotResult.Missed, "2PT Miss"),
        (StatType.Points3, ShotResult.Made, "3PT Made"),
        (StatType.Points3, ShotResult.Missed, "3PT Miss"),
        (StatType.FreeThrow, ShotResult.Made, "FT Made"),
        (StatType.FreeThrow, ShotResult.Missed, "FT Miss"),
        (StatType.OffensiveRebound, null, "OFF Rebound"),
        (StatType.DefensiveRebound, null, "DEF Rebound"),
        (StatType.Assist, null, "Assist"),
        (StatType.Steal, null, "Steal"),
        (StatType.Block, null, "Block"),
        (StatType.Turnover, null, "Turnover"),
        (StatType.PersonalFoul, null, "Personal Foul"),
        (StatType.TechnicalFoul, null, "Technical Foul"),
    ];

    private void BuildStatRows(int playerId)
    {
        StatRows.Clear();
        foreach (var (type, result, label) in Categories)
        {
            StatRows.Add(new StatCounterRow
            {
                StatType = type,
                ShotResult = result,
                Label = label,
                Count = EffectiveCount(playerId, type, result)
            });
        }
    }

    /// <summary>Persisted count, net of staged removals and additions.</summary>
    private int EffectiveCount(int playerId, StatType type, ShotResult? result) =>
        _events.Count(e => e.PlayerId == playerId && Matches(e, type, result) && !_pendingRemoveIds.Contains(e.Id))
        + _pendingAdds.Count(e => e.PlayerId == playerId && Matches(e, type, result));

    private static bool Matches(StatEvent e, StatType type, ShotResult? result) =>
        e.StatType == type && e.ShotResult == result;

    // ── Adjust counters (staged) ──
    [RelayCommand]
    private void Increment(StatCounterRow row)
    {
        if (SelectedChip is null || _game is null) return;

        // Added events carry no court location (shot positions are not editable) and
        // are attributed to the game's final period so reports stay consistent.
        _pendingAdds.Add(new StatEvent
        {
            GameId = GameId,
            PlayerId = SelectedChip.Player.Id,
            StatType = row.StatType,
            ShotResult = row.ShotResult,
            Quarter = Math.Max(1, _game.CurrentPeriod),
            GameClock = "0:00",
            Timestamp = DateTime.UtcNow
        });

        row.Count++;
        AfterStagedChange();
    }

    [RelayCommand]
    private void Decrement(StatCounterRow row)
    {
        if (SelectedChip is null) return;
        int playerId = SelectedChip.Player.Id;

        // Prefer undoing a staged addition first; otherwise stage the removal of the
        // most recent matching persisted event.
        var pendingAdd = _pendingAdds.LastOrDefault(e => e.PlayerId == playerId && Matches(e, row.StatType, row.ShotResult));
        if (pendingAdd is not null)
        {
            _pendingAdds.Remove(pendingAdd);
        }
        else
        {
            var target = _events
                .Where(e => e.PlayerId == playerId && Matches(e, row.StatType, row.ShotResult) && !_pendingRemoveIds.Contains(e.Id))
                .OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id)
                .FirstOrDefault();
            if (target is null) return; // nothing left to remove
            _pendingRemoveIds.Add(target.Id);
        }

        row.Count--;
        AfterStagedChange();
    }

    private void AfterStagedChange()
    {
        PendingCount = _pendingAdds.Count + _pendingRemoveIds.Count;
        RecomputeScore();
    }

    private void RecomputeScore()
    {
        int home = 0, away = 0;
        var homeIds = _allHomePlayers.Select(p => p.Id).ToHashSet();

        void Tally(StatEvent e, int sign)
        {
            if (e.ShotResult != ShotResult.Made) return;
            int pts = e.StatType switch
            {
                StatType.Points2 => 2,
                StatType.Points3 => 3,
                StatType.FreeThrow => 1,
                _ => 0
            };
            if (pts == 0) return;
            if (homeIds.Contains(e.PlayerId)) home += sign * pts; else away += sign * pts;
        }

        foreach (var e in _events)
            if (!_pendingRemoveIds.Contains(e.Id)) Tally(e, 1);
        foreach (var e in _pendingAdds)
            Tally(e, 1);

        ScoreDisplay = $"{_homeAbbr} {home} — {away} {_awayAbbr}";
    }

    // ── Save / Cancel ──
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (PendingCount == 0)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Save Changes",
            $"Apply {PendingSummary.ToLowerInvariant()} to this game? Box score, season stats and the PDF will update.",
            "Save", "Cancel");
        if (!confirm) return;

        // Also remove any follow-up (assist/rebound) whose linked shot/miss is being
        // removed, so no phantom assist survives a basket that no longer exists.
        foreach (var dep in _events)
            if (dep.LinkedEventId is int linkedId && _pendingRemoveIds.Contains(linkedId))
                _pendingRemoveIds.Add(dep.Id);

        foreach (var id in _pendingRemoveIds)
            await _statEventRepository.DeleteAsync(id);
        foreach (var e in _pendingAdds)
            await _statEventRepository.AddAsync(e);

        _pendingAdds.Clear();
        _pendingRemoveIds.Clear();
        PendingCount = 0;

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (PendingCount > 0)
        {
            bool discard = await Shell.Current.DisplayAlertAsync(
                "Discard Changes",
                $"Discard {PendingSummary.ToLowerInvariant()}?",
                "Discard", "Keep Editing");
            if (!discard) return;
        }
        await Shell.Current.GoToAsync("..");
    }
}

// ── Supporting types ──

public partial class PlayerChip : ObservableObject
{
    public Player Player { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public string TeamColor { get; set; } = "#888";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChipBackground))]
    [NotifyPropertyChangedFor(nameof(ChipTextColor))]
    public partial bool IsSelected { get; set; }

    public string ChipBackground => IsSelected ? TeamColor : "#141414";
    public string ChipTextColor => IsSelected ? "#ffffff" : "#999999";
}

public partial class StatCounterRow : ObservableObject
{
    public StatType StatType { get; set; }
    public ShotResult? ShotResult { get; set; }
    public string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Count { get; set; }
}
