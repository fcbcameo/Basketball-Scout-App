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
[QueryProperty(nameof(ResumeString), "resume")]
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
    [NotifyPropertyChangedFor(nameof(PeriodLabel))]
    public partial int Quarter { get; set; } = 1;

    /// <summary>"Q1".."Q4" for regulation, then "OT1", "OT2", … with no cap.</summary>
    public string PeriodLabel => Quarter <= 4 ? $"Q{Quarter}" : $"OT{Quarter - 4}";

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

    private const int QuarterLengthSeconds = 600;   // 10:00 regulation
    private const int OvertimeLengthSeconds = 300;   // 5:00 overtime
    private int _clockSeconds = QuarterLengthSeconds;

    private static int PeriodLengthSeconds(int quarter) =>
        quarter >= 5 ? OvertimeLengthSeconds : QuarterLengthSeconds;
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

    // Rebound/assist prompt is split per team (US-13) so home and away are visually
    // distinct: each group is tinted with its team color and separated by a divider.
    [ObservableProperty]
    public partial bool HasHomeFollowUpCandidates { get; set; }

    [ObservableProperty]
    public partial bool HasAwayFollowUpCandidates { get; set; }

    [ObservableProperty]
    public partial bool ShowFollowUpDivider { get; set; }

    [ObservableProperty]
    public partial bool IsCorrectionsOpen { get; set; }

    // ── Collections ──
    public ObservableCollection<Player> HomeOnCourt { get; } = new();
    public ObservableCollection<Player> HomeBench { get; } = new();
    public ObservableCollection<Player> AwayOnCourt { get; } = new();
    public ObservableCollection<Player> AwayBench { get; } = new();
    public ObservableCollection<ShotDot> ShotChartDots { get; } = new();
    public ObservableCollection<PlayLogEntry> PlayLog { get; } = new();
    public ObservableCollection<FollowUpCandidate> HomeFollowUpCandidates { get; } = new();
    public ObservableCollection<FollowUpCandidate> AwayFollowUpCandidates { get; } = new();
    public ObservableCollection<CorrectionEntry> RecentEvents { get; } = new();
    public ObservableCollection<PlayerFoulRow> FoulRows { get; } = new();

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

    /// <summary>Set to "1" when navigating in to resume an existing in-progress game (US-10).
    /// Resume nav carries no active-id lists — the on-court five is reconstructed from
    /// the game's substitution history instead.</summary>
    public string ResumeString
    {
        set
        {
            _resume = value is "1" or "true";
            TryLoad();
        }
    }

    private string _homeActiveIdsParsed = string.Empty;
    private string _awayActiveIdsParsed = string.Empty;
    private bool _gameIdSet;
    private bool _homeIdsSet;
    private bool _awayIdsSet;
    private bool _resume;
    private bool _loaded;

    /// <summary>Intended persisted status; flips to Finished only via Finish Game.</summary>
    private GameStatus _status = GameStatus.InProgress;

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
        if (!_gameIdSet) return;
        // New game: wait for both active-id lists. Resume: those lists aren't passed,
        // so the game id alone (plus the resume flag) is enough.
        if (!_resume && (!_homeIdsSet || !_awayIdsSet)) return;
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

        _status = game.Status;

        // Load players
        _allHomePlayers = (await _playerRepository.GetByTeamIdAsync(game.HomeTeamId)).ToList();
        _allAwayPlayers = (await _playerRepository.GetByTeamIdAsync(game.AwayTeamId)).ToList();

        var existing = await _statEventRepository.GetByGameIdAsync(id);
        bool anySubEvents = existing.Any(e => e.StatType == StatType.SubIn || e.StatType == StatType.SubOut);

        // On-court five: reconstruct from substitution history when resuming a game
        // that has already been entered; otherwise use the lineup chosen at setup.
        HashSet<int> homeActiveIds, awayActiveIds;
        if (anySubEvents)
            (homeActiveIds, awayActiveIds) = ReconstructOnCourt(existing);
        else
        {
            homeActiveIds = ParseIds(_homeActiveIdsParsed);
            awayActiveIds = ParseIds(_awayActiveIdsParsed);
        }

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

        if (!anySubEvents)
        {
            // First entry into a new game: persist the starting lineup as SubIn events
            // so GameStatsService can reconstruct minutes and +/- later.
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
        else
        {
            // Resume: rebuild score / fouls / shot dots / play log from the events,
            // and restore the exact clock and period the scorer left off at.
            RebuildFromEvents(existing);
            Quarter = Math.Max(1, game.CurrentPeriod);
            _clockSeconds = Math.Clamp(game.ClockSecondsRemaining, 0, PeriodLengthSeconds(Quarter));
            GameClock = FormatClock(_clockSeconds);
            IsClockRunning = false; // always resume paused — never tick while away
        }
    }

    private static HashSet<int> ParseIds(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();

    /// <summary>
    /// Replays the substitution history to determine who is currently on court for each
    /// team (net of SubIn/SubOut), so a resumed game shows the correct active five.
    /// </summary>
    private (HashSet<int> Home, HashSet<int> Away) ReconstructOnCourt(IReadOnlyList<StatEvent> events)
    {
        var onCourt = new HashSet<int>();
        foreach (var e in events
            .Where(e => e.StatType is StatType.SubIn or StatType.SubOut)
            .OrderBy(e => e.Timestamp).ThenBy(e => e.Id))
        {
            if (e.StatType == StatType.SubIn) onCourt.Add(e.PlayerId);
            else onCourt.Remove(e.PlayerId);
        }

        var home = _allHomePlayers.Where(p => onCourt.Contains(p.Id)).Select(p => p.Id).ToHashSet();
        var away = _allAwayPlayers.Where(p => onCourt.Contains(p.Id)).Select(p => p.Id).ToHashSet();
        return (home, away);
    }

    /// <summary>
    /// Rebuilds in-memory game state (score, team fouls, shot-chart dots, play log) by
    /// replaying the persisted stat events. Used when resuming an in-progress game.
    /// </summary>
    private void RebuildFromEvents(IReadOnlyList<StatEvent> events)
    {
        HomeScore = 0;
        AwayScore = 0;
        HomeFouls = 0;
        AwayFouls = 0;
        ShotChartDots.Clear();
        PlayLog.Clear();

        var ordered = events
            .Where(e => e.StatType is not StatType.SubIn and not StatType.SubOut)
            .OrderBy(e => e.Timestamp).ThenBy(e => e.Id)
            .ToList();

        foreach (var e in ordered)
        {
            bool isHome = _allHomePlayers.Any(p => p.Id == e.PlayerId);

            if (e.ShotResult == ShotResult.Made)
            {
                int pts = e.StatType switch
                {
                    StatType.Points2 => 2,
                    StatType.Points3 => 3,
                    StatType.FreeThrow => 1,
                    _ => 0
                };
                if (isHome) HomeScore += pts; else AwayScore += pts;
            }

            if (e.StatType is StatType.PersonalFoul or StatType.TechnicalFoul)
            {
                if (isHome) HomeFouls++; else AwayFouls++;
            }

            if (e.StatType is StatType.Points2 or StatType.Points3 && e.CourtX.HasValue && e.CourtY.HasValue)
            {
                ShotChartDots.Add(new ShotDot
                {
                    EventId = e.Id,
                    PlayerId = e.PlayerId,
                    X = e.CourtX.Value,
                    Y = e.CourtY.Value,
                    IsMade = e.ShotResult == ShotResult.Made,
                    Label = e.StatType == StatType.Points3 ? "3" : "2"
                });
            }

            // Newest-first log; preserve each event's own period/clock rather than the
            // current ones (inserting at 0 while iterating oldest→newest yields that order).
            PlayLog.Insert(0, new PlayLogEntry
            {
                Message = DescribeEvent(e),
                Quarter = e.Quarter,
                GameClock = e.GameClock,
                IsHome = isHome,
                Timestamp = e.Timestamp,
                EventId = e.Id
            });
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

        // Shot chart dot (tagged with the event id so it can be removed precisely
        // on undo or via the corrections drawer)
        ShotChartDots.Add(new ShotDot
        {
            EventId = statEvent.Id,
            PlayerId = SelectedPlayer.Id,
            X = PendingShot.X,
            Y = PendingShot.Y,
            IsMade = confirmation.IsMade,
            Label = confirmation.Is3Pt ? "3" : "2"
        });

        if (confirmation.IsMade)
        {
            if (isHome) HomeScore += pts; else AwayScore += pts;
            AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — {pts}PT Made", isHome, statEvent.Id);
            SetFollowUp("assist", statEvent.Id);
        }
        else
        {
            AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — {pts}PT Miss", isHome, statEvent.Id);
            SetFollowUp("rebound", statEvent.Id);
        }

        PendingShot = null;
    }

    // ── Follow-up (assist/rebound) ──
    private void SetFollowUp(string type, int linkedEventId)
    {
        HomeFollowUpCandidates.Clear();
        AwayFollowUpCandidates.Clear();

        var homeColor = Color.FromArgb(HomeTeamColor);
        var awayColor = Color.FromArgb(AwayTeamColor);

        if (type == "assist")
        {
            // Teammates on court (the scorer's team only), excluding the scorer.
            var teammates = IsHomeSelected ? HomeOnCourt : AwayOnCourt;
            var target = IsHomeSelected ? HomeFollowUpCandidates : AwayFollowUpCandidates;
            var color = IsHomeSelected ? homeColor : awayColor;
            foreach (var p in teammates)
            {
                if (p.Id != SelectedPlayer?.Id)
                    target.Add(new FollowUpCandidate(p, color));
            }
        }
        else // rebound — on-court players from both teams, kept in separate groups
        {
            foreach (var p in HomeOnCourt)
                HomeFollowUpCandidates.Add(new FollowUpCandidate(p, homeColor));
            foreach (var p in AwayOnCourt)
                AwayFollowUpCandidates.Add(new FollowUpCandidate(p, awayColor));
        }

        HasHomeFollowUpCandidates = HomeFollowUpCandidates.Count > 0;
        HasAwayFollowUpCandidates = AwayFollowUpCandidates.Count > 0;
        ShowFollowUpDivider = HasHomeFollowUpCandidates && HasAwayFollowUpCandidates;

        FollowUp = new FollowUpState(type, linkedEventId);
    }

    private void ClearFollowUp()
    {
        FollowUp = null;
        HomeFollowUpCandidates.Clear();
        AwayFollowUpCandidates.Clear();
        HasHomeFollowUpCandidates = false;
        HasAwayFollowUpCandidates = false;
        ShowFollowUpDivider = false;
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
            AddLog($"  > #{player.JerseyNumber} {player.Name} — {(isAssist ? "Assist" : "Rebound")}", isHome, ev.Id);
        }

        ClearFollowUp();
    }

    [RelayCommand]
    private void SkipFollowUp()
    {
        ClearFollowUp();
    }

    // ── Quick stats ──
    /// <summary>Raised after a free throw or non-shot stat is recorded, with a short
    /// description for the view to flash as a confirmation toast (US-8). Field goals
    /// are intentionally excluded — they get a court marker dot instead.</summary>
    public event Action<string>? ActionRecorded;

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

        AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — FT {(made ? "Made" : "Miss")}", isHome, ev.Id);
        ActionRecorded?.Invoke(DescribeEvent(ev));
    }

    [RelayCommand]
    private async Task RecordStatAsync(string statId)
    {
        if (SelectedPlayer is null) return;
        bool isHome = IsHomeSelected;

        (StatType Type, string Label)? mapped = statId switch
        {
            "ast" => (StatType.Assist, "Assist"),
            "stl" => (StatType.Steal, "Steal"),
            "blk" => (StatType.Block, "Block"),
            "to" => (StatType.Turnover, "Turnover"),
            "oreb" => (StatType.OffensiveRebound, "OFF Rebound"),
            "dreb" => (StatType.DefensiveRebound, "DEF Rebound"),
            "pf" => (StatType.PersonalFoul, "Personal Foul"),
            "tech" => (StatType.TechnicalFoul, "Technical Foul"),
            _ => null
        };

        // Ignore an unrecognized id rather than silently logging a Turnover.
        if (mapped is null)
        {
            System.Diagnostics.Debug.WriteLine($"Ignoring unknown quick-stat id '{statId}'.");
            return;
        }
        var (statType, label) = mapped.Value;

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

        AddLog($"#{SelectedPlayer.JerseyNumber} {SelectedPlayer.Name} — {label}", isHome, ev.Id);
        ActionRecorded?.Invoke(DescribeEvent(ev));
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
            .ThenByDescending(e => e.Id) // deterministic tie-break under rapid entry
            .FirstOrDefault();

        if (last is null)
        {
            ActionUndone?.Invoke("Nothing to undo");
            return;
        }

        ApplyReversal(last);
        string description = DescribeEvent(last);

        await _statEventRepository.DeleteAsync(last.Id);

        // Remove exactly this event's log row (not blindly the newest — a substitution
        // may sit on top and must stay).
        var row = PlayLog.FirstOrDefault(p => p.EventId == last.Id);
        if (row is not null) PlayLog.Remove(row);

        ClearFollowUp();
        PendingShot = null;

        ActionUndone?.Invoke($"Undid: {description}");
    }

    /// <summary>Builds a short label like "#1 Wemby — 2PT Made" for play log / undo feedback.</summary>
    private string DescribeEvent(StatEvent e) => $"{PlayerLabel(e.PlayerId)} — {DescribeStat(e)}";

    /// <summary>"#1 Wemby" for a player id (or "Player" if not found).</summary>
    private string PlayerLabel(int playerId)
    {
        var p = _allHomePlayers.Concat(_allAwayPlayers).FirstOrDefault(x => x.Id == playerId);
        return p is not null ? $"#{p.JerseyNumber} {p.Name}" : "Player";
    }

    /// <summary>"2PT Made", "Steal", "Personal Foul"… — the action without the player.</summary>
    private static string DescribeStat(StatEvent e) => e.StatType switch
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

    private static string ShotResultText(StatEvent e) =>
        e.ShotResult == ShotResult.Made ? "Made" : "Miss";

    /// <summary>
    /// Reverses the score / team-foul / shot-dot impact of a single event.
    /// Shared by undo (US-4) and the corrections drawer (US-5).
    /// </summary>
    private void ApplyReversal(StatEvent e)
    {
        bool isHome = _allHomePlayers.Any(p => p.Id == e.PlayerId);

        if (e.ShotResult == ShotResult.Made)
        {
            int pts = e.StatType switch
            {
                StatType.Points2 => 2,
                StatType.Points3 => 3,
                StatType.FreeThrow => 1,
                _ => 0
            };
            if (isHome) HomeScore = Math.Max(0, HomeScore - pts);
            else AwayScore = Math.Max(0, AwayScore - pts);
        }

        if (e.StatType is StatType.PersonalFoul or StatType.TechnicalFoul)
        {
            if (isHome) HomeFouls = Math.Max(0, HomeFouls - 1);
            else AwayFouls = Math.Max(0, AwayFouls - 1);
        }

        if (e.StatType is StatType.Points2 or StatType.Points3)
        {
            var dot = ShotChartDots.FirstOrDefault(d => d.EventId == e.Id);
            if (dot is not null) ShotChartDots.Remove(dot);
        }
    }

    // ── In-match corrections (US-5) ──
    [RelayCommand]
    private async Task ToggleCorrectionsAsync()
    {
        IsCorrectionsOpen = !IsCorrectionsOpen;
        if (IsCorrectionsOpen)
        {
            // Avoid clashing with shot placement while the drawer is open.
            SelectedPlayer = null;
            PendingShot = null;
            await RefreshCorrectionsAsync();
        }
    }

    [RelayCommand]
    private void CloseCorrections() => IsCorrectionsOpen = false;

    [RelayCommand]
    private async Task DeleteEventAsync(CorrectionEntry? entry)
    {
        if (entry is null) return;
        var events = await _statEventRepository.GetByGameIdAsync(GameId);
        var e = events.FirstOrDefault(x => x.Id == entry.EventId);
        if (e is null) return;

        // Remove any follow-up linked to this event first (an assist off a made shot, a
        // rebound off a miss), so deleting the shot never leaves a phantom assist behind.
        foreach (var dependent in events.Where(x => x.LinkedEventId == e.Id))
        {
            ApplyReversal(dependent);
            await _statEventRepository.DeleteAsync(dependent.Id);
        }

        ApplyReversal(e);
        await _statEventRepository.DeleteAsync(e.Id);
        await RefreshCorrectionsAsync();
    }

    [RelayCommand]
    private async Task AddFoulAsync(Player? player)
    {
        if (player is null) return;
        bool isHome = _allHomePlayers.Any(p => p.Id == player.Id);

        await _statEventRepository.AddAsync(new StatEvent
        {
            GameId = GameId,
            PlayerId = player.Id,
            StatType = StatType.PersonalFoul,
            Quarter = Quarter,
            GameClock = GameClock,
            Timestamp = DateTime.UtcNow
        });

        if (isHome) HomeFouls++; else AwayFouls++;
        await RefreshCorrectionsAsync();
    }

    [RelayCommand]
    private async Task RemoveFoulAsync(Player? player)
    {
        if (player is null) return;
        var events = await _statEventRepository.GetByGameIdAsync(GameId);
        var lastFoul = events
            .Where(x => x.PlayerId == player.Id
                && (x.StatType == StatType.PersonalFoul || x.StatType == StatType.TechnicalFoul))
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefault();
        if (lastFoul is null) return;

        bool isHome = _allHomePlayers.Any(p => p.Id == player.Id);
        if (isHome) HomeFouls = Math.Max(0, HomeFouls - 1);
        else AwayFouls = Math.Max(0, AwayFouls - 1);

        await _statEventRepository.DeleteAsync(lastFoul.Id);
        await RefreshCorrectionsAsync();
    }

    private async Task RefreshCorrectionsAsync()
    {
        var events = await _statEventRepository.GetByGameIdAsync(GameId);
        var realEvents = events
            .Where(e => e.StatType != StatType.SubIn && e.StatType != StatType.SubOut)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        // Recent events (deletable)
        RecentEvents.Clear();
        foreach (var e in realEvents.Take(25))
        {
            bool isHome = _allHomePlayers.Any(p => p.Id == e.PlayerId);
            RecentEvents.Add(new CorrectionEntry
            {
                EventId = e.Id,
                PlayerLabel = PlayerLabel(e.PlayerId),
                Description = DescribeStat(e),
                Meta = $"Q{e.Quarter} {e.GameClock}",
                Color = isHome ? HomeTeamColor : AwayTeamColor
            });
        }

        // Per-player foul counters for on-court players (both teams)
        FoulRows.Clear();
        foreach (var p in HomeOnCourt.Concat(AwayOnCourt))
        {
            bool isHome = _allHomePlayers.Any(x => x.Id == p.Id);
            int fouls = realEvents.Count(e => e.PlayerId == p.Id
                && (e.StatType == StatType.PersonalFoul || e.StatType == StatType.TechnicalFoul));
            FoulRows.Add(new PlayerFoulRow
            {
                Player = p,
                Label = $"#{p.JerseyNumber} {p.Name}",
                Fouls = fouls,
                Color = isHome ? HomeTeamColor : AwayTeamColor
            });
        }
    }

    // ── Quarter management ──
    [RelayCommand]
    private void NextQuarter()
    {
        // No cap — periods 5+ are overtime (OT1, OT2, …) with a 5:00 clock.
        Quarter++;
        StopClock();
        _clockSeconds = PeriodLengthSeconds(Quarter);
        GameClock = FormatClock(_clockSeconds);
        _ = SaveStateAsync();
    }

    // ── Resume / finish (US-10) ──
    /// <summary>
    /// Persists the live lifecycle state (status + exact clock + period) so the game can
    /// be left and resumed exactly where it was. Called on pause, period change, and when
    /// the scoring page disappears. Cheap, targeted write — never touches the event graph.
    /// </summary>
    public async Task SaveStateAsync()
    {
        if (!_loaded || GameId <= 0) return;
        await _gameRepository.UpdateGameStateAsync(GameId, _status, _clockSeconds, Quarter);
    }

    [RelayCommand]
    private async Task FinishGameAsync()
    {
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Finish Game",
            $"End this game and mark it complete?\n\n{HomeTeamAbbr} {HomeScore} — {AwayScore} {AwayTeamAbbr}",
            "Finish", "Cancel");
        if (!confirm) return;

        StopClock();
        _status = GameStatus.Finished;
        await SaveStateAsync();
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>US-16: leave an in-progress game without finishing it. The clock is
    /// stopped and the exact state saved (status stays <see cref="GameStatus.InProgress"/>),
    /// so the game still shows as Resume in the matches list. Distinct from Finish,
    /// which marks the game complete. Needed because the scoring page hides the nav bar,
    /// so there is otherwise no way off the screen except ending the game.</summary>
    [RelayCommand]
    private async Task LeaveGameAsync()
    {
        StopClock();                  // saved paused — never keeps ticking while away
        await SaveStateAsync();       // _status is unchanged (InProgress)
        await Shell.Current.GoToAsync("..");
    }

    // ── Game clock ──
    [RelayCommand]
    private void ToggleClock()
    {
        if (IsClockRunning)
        {
            // Capture the exact remaining time the moment the scorer pauses.
            StopClock();
            _ = SaveStateAsync();
        }
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
        if (_clockSeconds <= 0)
        {
            // Period expired while running — persist so a resume shows 0:00, not the
            // last saved value (matches a manual pause).
            StopClock();
            _ = SaveStateAsync();
        }
    }

    /// <summary>
    /// Called when the scoring page disappears — including Android hardware/gesture back,
    /// which (unlike the LEAVE/END buttons) does not otherwise stop the clock. Stops the
    /// timer so an abandoned ViewModel can't keep ticking (battery + leak) and persists
    /// the exact state for resume.
    /// </summary>
    public async Task OnPageDisappearingAsync()
    {
        StopClock();
        await SaveStateAsync();
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
    private void AddLog(string message, bool isHome, int? eventId = null)
    {
        PlayLog.Insert(0, new PlayLogEntry
        {
            Message = message,
            Quarter = Quarter,
            GameClock = GameClock,
            IsHome = isHome,
            Timestamp = DateTime.UtcNow,
            EventId = eventId
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
    public int EventId { get; set; }
    public int PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsMade { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class CorrectionEntry
{
    public int EventId { get; set; }
    public string PlayerLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string Color { get; set; } = "#888";
}

public class PlayerFoulRow
{
    public Player Player { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public int Fouls { get; set; }
    public string Color { get; set; } = "#888";
}

/// <summary>A player offered in the assist/rebound prompt, carrying the team color used
/// to tint its chip so home and away are visually distinct (US-13).</summary>
public class FollowUpCandidate
{
    public FollowUpCandidate(Player player, Color chipColor)
    {
        Player = player;
        ChipColor = chipColor;
    }

    public Player Player { get; }
    public Color ChipColor { get; }
    public string Display => $"#{Player.JerseyNumber}";
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
    /// <summary>The stat event this row represents, so undo can remove exactly this row
    /// (not blindly the newest, which may be a substitution). Null for non-event lines.</summary>
    public int? EventId { get; set; }
}

public class SubstitutionRequest
{
    public Player PlayerIn { get; set; } = null!;
    public Player PlayerOut { get; set; } = null!;
    public bool IsHome { get; set; }
}
