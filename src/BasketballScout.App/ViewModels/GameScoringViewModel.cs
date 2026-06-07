using System.Collections.ObjectModel;
using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(GameId), "gameId")]
[QueryProperty(nameof(HomeActiveIdsString), "homeActiveIds")]
[QueryProperty(nameof(AwayActiveIdsString), "awayActiveIds")]
public partial class GameScoringViewModel : ObservableObject
{
    private readonly IGameRepository _gameRepository;
    private readonly IStatEventRepository _statEventRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    // ── Game state ──
    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial int HomeScore { get; set; }

    [ObservableProperty]
    public partial int AwayScore { get; set; }

    [ObservableProperty]
    public partial int Quarter { get; set; } = 1;

    [ObservableProperty]
    public partial int HomeFouls { get; set; }

    [ObservableProperty]
    public partial int AwayFouls { get; set; }

    [ObservableProperty]
    public partial string GameClock { get; set; } = "10:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClockColor))]
    public partial bool IsClockRunning { get; set; }

    public string ClockColor => IsClockRunning ? "#4ade80" : "#ddd";

    private const int QuarterLengthSeconds = 600;
    private int _clockSeconds = QuarterLengthSeconds;
    private readonly System.Timers.Timer _clockTimer = new(1000) { AutoReset = true };

    // ── Teams ──
    [ObservableProperty]
    public partial string HomeTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HomeTeamAbbr { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HomeTeamColor { get; set; } = "#e85d26";

    [ObservableProperty]
    public partial string AwayTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AwayTeamAbbr { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AwayTeamColor { get; set; } = "#2d7dd2";

    private int _homeTeamId;
    private int _awayTeamId;

    // ── Selection state ──
    [ObservableProperty]
    public partial Player? SelectedPlayer { get; set; }

    [ObservableProperty]
    public partial bool IsHomeSelected { get; set; } = true;

    [ObservableProperty]
    public partial ShotPending? PendingShot { get; set; }

    [ObservableProperty]
    public partial FollowUpState? FollowUp { get; set; }

    // ── Collections ──
    public ObservableCollection<Player> HomeOnCourt { get; } = new();
    public ObservableCollection<Player> HomeBench { get; } = new();
    public ObservableCollection<Player> AwayOnCourt { get; } = new();
    public ObservableCollection<Player> AwayBench { get; } = new();
    public ObservableCollection<ShotDot> ShotChartDots { get; } = new();
    public ObservableCollection<PlayLogEntry> PlayLog { get; } = new();
    public ObservableCollection<Player> FollowUpCandidates { get; } = new();

    // ── Query property helpers ──
    // All three query properties must be applied before we can hydrate the rosters,
    // because the home/away active-id strings arrive *after* GameId. Guard with flags
    // and only call LoadGameAsync once everything is in place.
    public string HomeActiveIdsString
    {
        set
        {
            _homeActiveIdsParsed = value;
            _homeIdsSet = true;
            TryLoad();
        }
    }

    public string AwayActiveIdsString
    {
        set
        {
            _awayActiveIdsParsed = value;
            _awayIdsSet = true;
            TryLoad();
        }
    }

    private string _homeActiveIdsParsed = string.Empty;
    private string _awayActiveIdsParsed = string.Empty;
    private bool _gameIdSet;
    private bool _homeIdsSet;
    private bool _awayIdsSet;
    private bool _loaded;

    private List<Player> _allHomePlayers = new();
    private List<Player> _allAwayPlayers = new();

    public GameScoringViewModel(
        IGameRepository gameRepository,
        IStatEventRepository statEventRepository,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository)
    {
        _gameRepository = gameRepository;
        _statEventRepository = statEventRepository;
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;

        _clockTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(OnClockTick);
    }

    partial void OnGameIdChanged(int value)
    {
        _gameIdSet = value > 0;
        TryLoad();
    }

    private void TryLoad()
    {
        if (_loaded) return;
        if (!_gameIdSet || !_homeIdsSet || !_awayIdsSet) return;
        _loaded = true;
        _ = LoadGameAsync(GameId);
    }

    private async Task LoadGameAsync(int id)
    {
        var game = await _gameRepository.GetByIdAsync(id);
        if (game is null) return;

        _homeTeamId = game.HomeTeamId;
        _awayTeamId = game.AwayTeamId;

        var homeTeam = await _teamRepository.GetByIdAsync(game.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(game.AwayTeamId);

        HomeTeamName = homeTeam?.Name ?? "Home";
        HomeTeamAbbr = homeTeam?.Abbreviation ?? "HOM";
        HomeTeamColor = homeTeam?.Color ?? "#e85d26";
        AwayTeamName = awayTeam?.Name ?? "Away";
        AwayTeamAbbr = awayTeam?.Abbreviation ?? "AWY";
        AwayTeamColor = awayTeam?.Color ?? "#2d7dd2";

        // Parse active IDs
        var homeActiveIds = _homeActiveIdsParsed.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse).ToHashSet();
        var awayActiveIds = _awayActiveIdsParsed.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse).ToHashSet();

        // Load players
        _allHomePlayers = (await _playerRepository.GetByTeamIdAsync(game.HomeTeamId)).ToList();
        _allAwayPlayers = (await _playerRepository.GetByTeamIdAsync(game.AwayTeamId)).ToList();

        HomeOnCourt.Clear();
        HomeBench.Clear();
        foreach (var p in _allHomePlayers)
        {
            if (homeActiveIds.Contains(p.Id)) HomeOnCourt.Add(p);
            else HomeBench.Add(p);
        }

        AwayOnCourt.Clear();
        AwayBench.Clear();
        foreach (var p in _allAwayPlayers)
        {
            if (awayActiveIds.Contains(p.Id)) AwayOnCourt.Add(p);
            else AwayBench.Add(p);
        }

        // Persist starting lineup as SubIn events if this game has no sub events yet.
        // This lets GameStatsService reconstruct minutes and +/- later.
        var existing = await _statEventRepository.GetByGameIdAsync(id);
        bool anySubEvents = existing.Any(e => e.StatType == StatType.SubIn || e.StatType == StatType.SubOut);
        if (!anySubEvents)
        {
            foreach (var p in HomeOnCourt.Concat(AwayOnCourt))
            {
                await _statEventRepository.AddAsync(new StatEvent
                {
                    GameId = GameId,
                    PlayerId = p.Id,
                    StatType = StatType.SubIn,
                    Quarter = 1,
                    GameClock = "10:00",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    // ── Player selection ──
    [RelayCommand]
    private void SelectPlayer(Player player)
    {
        if (FollowUp is not null) return;

        if (SelectedPlayer?.Id == player.Id)
        {
            SelectedPlayer = null;
        }
        else
        {
            SelectedPlayer = player;
            IsHomeSelected = _allHomePlayers.Any(p => p.Id == player.Id);
        }
        PendingShot = null;
    }

    // ── Court tap ──
    [RelayCommand]
    private void CourtTapped(ShotPending shot)
    {
        if (SelectedPlayer is null || FollowUp is not null) return;
        PendingShot = shot;
    }

    // ── Confirm shot ──
    [RelayCommand]
    private async Task ConfirmShotAsync(ShotConfirmation confirmation)
    {
        if (PendingShot is null || SelectedPlayer is null) return;

        var statType = confirmation.Is3Pt ? StatType.Points3 : StatType.Points2;
        var shotResult = confirmation.IsMade ? ShotResult.Made : ShotResult.Missed;
        var pts = confirmation.Is3Pt ? 3 : 2;
        bool isHome = IsHomeSelected;

        var statEvent = new StatEvent
        {
            GameId = GameId,
            PlayerId = SelectedPlayer.Id,
            StatType = statType,
            ShotResult = shotResult,
            CourtX = PendingShot.X,
            CourtY = PendingShot.Y,
            Quarter = Quarter,
            GameClock = GameClock,
            Timestamp = DateTime.UtcNow
        };
        await _statEventRepository.AddAsync(statEvent);

        // Shot chart dot
        ShotChartDots.Add(new ShotDot
        {
            X = PendingShot.X,
            Y = PendingShot.Y,
            IsMade = confirmation.IsMade,
            Label = confirmation.Is3Pt ? "3" : "2"
        });

        if (confirmation.IsMade)
        {
            if (isHome) HomeScore += pts; else AwayScore += pts;
            AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — {pts}PT Made", isHome);
            SetFollowUp("assist", statEvent.Id);
        }
        else
        {
            AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — {pts}PT Miss", isHome);
            SetFollowUp("rebound", statEvent.Id);
        }

        PendingShot = null;
    }

    // ── Follow-up (assist/rebound) ──
    private void SetFollowUp(string type, int linkedEventId)
    {
        FollowUpCandidates.Clear();

        if (type == "assist")
        {
            // Teammates on court, excluding the scorer
            var teammates = IsHomeSelected ? HomeOnCourt : AwayOnCourt;
            foreach (var p in teammates)
            {
                if (p.Id != SelectedPlayer?.Id)
                    FollowUpCandidates.Add(p);
            }
        }
        else // rebound
        {
            // ALL on-court players from both teams
            foreach (var p in HomeOnCourt)
                FollowUpCandidates.Add(p);
            foreach (var p in AwayOnCourt)
                FollowUpCandidates.Add(p);
        }

        FollowUp = new FollowUpState(type, linkedEventId);
    }

    private void ClearFollowUp()
    {
        FollowUp = null;
        FollowUpCandidates.Clear();
    }

    [RelayCommand]
    private async Task HandleFollowUpAsync(Player? player)
    {
        if (FollowUp is null) return;

        if (player is not null)
        {
            var isAssist = FollowUp.Type == "assist";
            var statType = isAssist ? StatType.Assist
                : _allHomePlayers.Any(p => p.Id == player.Id)
                    ? StatType.DefensiveRebound : StatType.OffensiveRebound;

            var ev = new StatEvent
            {
                GameId = GameId,
                PlayerId = player.Id,
                StatType = statType,
                Quarter = Quarter,
                GameClock = GameClock,
                Timestamp = DateTime.UtcNow,
                LinkedEventId = FollowUp.LinkedEventId
            };
            await _statEventRepository.AddAsync(ev);

            bool isHome = _allHomePlayers.Any(p => p.Id == player.Id);
            AddLog($"  > #{player.JerseyNumber} {player.Name} — {(isAssist ? "Assist" : "Rebound")}", isHome);
        }

        ClearFollowUp();
    }

    [RelayCommand]
    private void SkipFollowUp()
    {
        ClearFollowUp();
    }

    // ── Quick stats ──
    [RelayCommand]
    private async Task RecordFreeThrowAsync(bool made)
    {
        if (SelectedPlayer is null) return;
        bool isHome = IsHomeSelected;

        var ev = new StatEvent
        {
            GameId = GameId,
            PlayerId = SelectedPlayer.Id,
            StatType = StatType.FreeThrow,
            ShotResult = made ? ShotResult.Made : ShotResult.Missed,
            Quarter = Quarter,
            GameClock = GameClock,
            Timestamp = DateTime.UtcNow
        };
        await _statEventRepository.AddAsync(ev);

        if (made)
        {
            if (isHome) HomeScore += 1; else AwayScore += 1;
        }

        AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — FT {(made ? "Made" : "Miss")}", isHome);
    }

    [RelayCommand]
    private async Task RecordStatAsync(string statId)
    {
        if (SelectedPlayer is null) return;
        bool isHome = IsHomeSelected;

        var (statType, label) = statId switch
        {
            "ast" => (StatType.Assist, "Assist"),
            "stl" => (StatType.Steal, "Steal"),
            "blk" => (StatType.Block, "Block"),
            "to" => (StatType.Turnover, "Turnover"),
            "oreb" => (StatType.OffensiveRebound, "OFF Rebound"),
            "dreb" => (StatType.DefensiveRebound, "DEF Rebound"),
            "pf" => (StatType.PersonalFoul, "Personal Foul"),
            "tech" => (StatType.TechnicalFoul, "Technical Foul"),
            _ => (StatType.Turnover, statId)
        };

        var ev = new StatEvent
        {
            GameId = GameId,
            PlayerId = SelectedPlayer.Id,
            StatType = statType,
            Quarter = Quarter,
            GameClock = GameClock,
            Timestamp = DateTime.UtcNow
        };
        await _statEventRepository.AddAsync(ev);

        if (statId is "pf" or "tech")
        {
            if (isHome) HomeFouls++; else AwayFouls++;
        }

        AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — {label}", isHome);
    }

    // ── Undo ──
    /// <summary>Raised after an undo, with a human-readable description of what was undone
    /// (or "Nothing to undo"). The view surfaces this as a toast.</summary>
    public event Action<string>? ActionUndone;

    [RelayCommand]
    private async Task UndoAsync()
    {
        var events = await _statEventRepository.GetByGameIdAsync(GameId);
        // Never undo sub events (starting lineup / substitutions) — find the most
        // recent real action of ANY type.
        var last = events
            .Where(e => e.StatType != StatType.SubIn && e.StatType != StatType.SubOut)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        if (last is null)
        {
            ActionUndone?.Invoke("Nothing to undo");
            return;
        }

        bool isHome = _allHomePlayers.Any(p => p.Id == last.PlayerId);

        // Reverse score impact
        if (last.ShotResult == ShotResult.Made)
        {
            int pts = last.StatType switch
            {
                StatType.Points2 => 2,
                StatType.Points3 => 3,
                StatType.FreeThrow => 1,
                _ => 0
            };
            if (isHome) HomeScore = Math.Max(0, HomeScore - pts);
            else AwayScore = Math.Max(0, AwayScore - pts);
        }

        // Reverse foul impact
        if (last.StatType is StatType.PersonalFoul or StatType.TechnicalFoul)
        {
            if (isHome) HomeFouls = Math.Max(0, HomeFouls - 1);
            else AwayFouls = Math.Max(0, AwayFouls - 1);
        }

        // Remove the matching shot-chart dot if it was a field goal
        if (last.StatType is StatType.Points2 or StatType.Points3 && ShotChartDots.Count > 0)
        {
            ShotChartDots.RemoveAt(ShotChartDots.Count - 1);
        }

        string description = DescribeEvent(last);

        await _statEventRepository.DeleteAsync(last.Id);

        if (PlayLog.Count > 0)
            PlayLog.RemoveAt(0);

        ClearFollowUp();
        PendingShot = null;

        ActionUndone?.Invoke($"Undid: {description}");
    }

    /// <summary>Builds a short label like "#1 Wemby — 2PT Made" for play log / undo feedback.</summary>
    private string DescribeEvent(StatEvent e)
    {
        var player = _allHomePlayers.Concat(_allAwayPlayers)
            .FirstOrDefault(p => p.Id == e.PlayerId);
        string who = player is not null ? $"#{player.JerseyNumber} {player.Name}" : "Player";

        string what = e.StatType switch
        {
            StatType.Points2 => $"2PT {ShotResultText(e)}",
            StatType.Points3 => $"3PT {ShotResultText(e)}",
            StatType.FreeThrow => $"FT {ShotResultText(e)}",
            StatType.Assist => "Assist",
            StatType.Steal => "Steal",
            StatType.Block => "Block",
            StatType.Turnover => "Turnover",
            StatType.OffensiveRebound => "OFF Rebound",
            StatType.DefensiveRebound => "DEF Rebound",
            StatType.PersonalFoul => "Personal Foul",
            StatType.TechnicalFoul => "Technical Foul",
            _ => e.StatType.ToString()
        };

        return $"{who} — {what}";
    }

    private static string ShotResultText(StatEvent e) =>
        e.ShotResult == ShotResult.Made ? "Made" : "Miss";

    // ── Quarter management ──
    [RelayCommand]
    private void NextQuarter()
    {
        if (Quarter < 6) // up to 2 OT
        {
            Quarter++;
            StopClock();
            _clockSeconds = QuarterLengthSeconds;
            GameClock = FormatClock(_clockSeconds);
        }
    }

    // ── Game clock ──
    [RelayCommand]
    private void ToggleClock()
    {
        if (IsClockRunning) StopClock();
        else StartClock();
    }

    private void StartClock()
    {
        if (_clockSeconds <= 0) return;
        _clockTimer.Start();
        IsClockRunning = true;
    }

    private void StopClock()
    {
        _clockTimer.Stop();
        IsClockRunning = false;
    }

    private void OnClockTick()
    {
        if (_clockSeconds <= 0)
        {
            StopClock();
            return;
        }
        _clockSeconds--;
        GameClock = FormatClock(_clockSeconds);
        if (_clockSeconds <= 0) StopClock();
    }

    private static string FormatClock(int seconds) =>
        $"{seconds / 60}:{seconds % 60:D2}";

    // ── Substitution ──
    [RelayCommand]
    private async Task SubstituteAsync(SubstitutionRequest sub)
    {
        var onCourt = sub.IsHome ? HomeOnCourt : AwayOnCourt;
        var bench = sub.IsHome ? HomeBench : AwayBench;

        if (onCourt.Contains(sub.PlayerOut) && bench.Contains(sub.PlayerIn))
        {
            onCourt.Remove(sub.PlayerOut);
            bench.Add(sub.PlayerOut);
            bench.Remove(sub.PlayerIn);
            onCourt.Add(sub.PlayerIn);

            // Persist both events so minutes and +/- can be reconstructed later.
            var now = DateTime.UtcNow;
            await _statEventRepository.AddAsync(new StatEvent
            {
                GameId = GameId,
                PlayerId = sub.PlayerOut.Id,
                StatType = StatType.SubOut,
                Quarter = Quarter,
                GameClock = GameClock,
                Timestamp = now
            });
            await _statEventRepository.AddAsync(new StatEvent
            {
                GameId = GameId,
                PlayerId = sub.PlayerIn.Id,
                StatType = StatType.SubIn,
                Quarter = Quarter,
                GameClock = GameClock,
                Timestamp = now.AddTicks(1)
            });

            AddLog($"SUB: #{sub.PlayerIn.JerseyNumber} in, #{sub.PlayerOut.JerseyNumber} out", sub.IsHome);
        }
    }

    // ── Team switch (portrait mode) ──
    [RelayCommand]
    private void SwitchTeam()
    {
        IsHomeSelected = !IsHomeSelected;
        SelectedPlayer = null;
        PendingShot = null;
        FollowUp = null;
    }

    // ── Helpers ──
    private void AddLog(string message, bool isHome)
    {
        PlayLog.Insert(0, new PlayLogEntry
        {
            Message = message,
            Quarter = Quarter,
            GameClock = GameClock,
            IsHome = isHome,
            Timestamp = DateTime.UtcNow
        });

        if (PlayLog.Count > 100)
            PlayLog.RemoveAt(PlayLog.Count - 1);
    }

    public ObservableCollection<Player> CurrentOnCourt =>
        IsHomeSelected ? HomeOnCourt : AwayOnCourt;
}

// ── Supporting types ──
public class ShotPending
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsSuggested3Pt { get; set; }
}

public class ShotConfirmation
{
    public bool Is3Pt { get; set; }
    public bool IsMade { get; set; }
}

public class ShotDot
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsMade { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class FollowUpState
{
    public string Type { get; set; }
    public int LinkedEventId { get; set; }
    public FollowUpState(string type, int linkedEventId)
    {
        Type = type;
        LinkedEventId = linkedEventId;
    }
}

public class PlayLogEntry
{
    public string Message { get; set; } = string.Empty;
    public int Quarter { get; set; }
    public string GameClock { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SubstitutionRequest
{
    public Player PlayerIn { get; set; } = null!;
    public Player PlayerOut { get; set; } = null!;
    public bool IsHome { get; set; }
}
