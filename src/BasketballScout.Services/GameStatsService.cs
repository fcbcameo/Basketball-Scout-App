using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;

namespace BasketballScout.Services;

public class GameStatsService
{
    private readonly IGameRepository _gameRepository;
    private readonly IStatEventRepository _statEventRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;

    public GameStatsService(
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

    public async Task<GameBoxScore> GetGameBoxScoreAsync(int gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game is null) return new GameBoxScore();

        var events = await _statEventRepository.GetByGameIdAsync(gameId);
        var homeTeam = await _teamRepository.GetByIdAsync(game.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(game.AwayTeamId);

        var metrics = ComputeGameMetrics(events, game.HomeTeamId, game.AwayTeamId,
            homeTeam?.Players ?? [], awayTeam?.Players ?? [], GameFormat.FromGame(game));

        var homeLines = BuildBoxLines(events, homeTeam?.Players ?? [], metrics);
        var awayLines = BuildBoxLines(events, awayTeam?.Players ?? [], metrics);

        return new GameBoxScore
        {
            GameId = gameId,
            GameDate = game.GameDate,
            HomeTeamName = homeTeam?.Name ?? "Home",
            AwayTeamName = awayTeam?.Name ?? "Away",
            HomeTeamAbbr = homeTeam?.Abbreviation ?? "HOM",
            AwayTeamAbbr = awayTeam?.Abbreviation ?? "AWY",
            HomeScore = homeLines.Sum(l => l.Points),
            AwayScore = awayLines.Sum(l => l.Points),
            HomeLines = homeLines,
            AwayLines = awayLines
        };
    }

    /// <summary>
    /// Per-game summaries for a season's matches overview: final score computed
    /// from stat events, plus lifecycle status. Completed (Finished) matches and
    /// resumable in-progress games are both returned; the caller decides how to
    /// present each. Newest first.
    /// </summary>
    public async Task<List<SeasonGameSummary>> GetSeasonGameSummariesAsync(int seasonId)
    {
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);
        var teamLookup = teams.ToDictionary(t => t.Id);
        var playerTeam = teams
            .SelectMany(t => t.Players.Select(p => (PlayerId: p.Id, TeamId: t.Id)))
            .ToDictionary(x => x.PlayerId, x => x.TeamId);

        var result = new List<SeasonGameSummary>();
        foreach (var game in games.OrderByDescending(g => g.GameDate).ThenByDescending(g => g.Id))
        {
            var events = await _statEventRepository.GetByGameIdAsync(game.Id);
            bool hasEvents = events.Count > 0;
            bool isPlayed = game.Status == GameStatus.Finished;

            int home = 0, away = 0;
            foreach (var e in events)
            {
                if (e.ShotResult != ShotResult.Made) continue;
                int pts = e.StatType switch
                {
                    StatType.Points2 => 2,
                    StatType.Points3 => 3,
                    StatType.FreeThrow => 1,
                    _ => 0
                };
                if (pts == 0 || !playerTeam.TryGetValue(e.PlayerId, out var tid)) continue;
                if (tid == game.HomeTeamId) home += pts;
                else if (tid == game.AwayTeamId) away += pts;
            }

            var homeTeam = teamLookup.GetValueOrDefault(game.HomeTeamId);
            var awayTeam = teamLookup.GetValueOrDefault(game.AwayTeamId);

            result.Add(new SeasonGameSummary
            {
                GameId = game.Id,
                GameDate = game.GameDate,
                HomeTeamId = game.HomeTeamId,
                AwayTeamId = game.AwayTeamId,
                HomeTeamName = homeTeam?.Name ?? "Home",
                AwayTeamName = awayTeam?.Name ?? "Away",
                HomeTeamAbbr = homeTeam?.Abbreviation ?? "HOM",
                AwayTeamAbbr = awayTeam?.Abbreviation ?? "AWY",
                HomeScore = home,
                AwayScore = away,
                IsPlayed = isPlayed,
                Status = game.Status,
                HasEvents = hasEvents
            });
        }
        return result;
    }

    public async Task<List<PlayerSeasonStats>> GetSeasonStatsAsync(int seasonId)
    {
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);
        var allPlayers = teams.SelectMany(t => t.Players).ToList();

        var playerStats = new Dictionary<int, PlayerSeasonStats>();
        foreach (var player in allPlayers)
        {
            var team = teams.First(t => t.Id == player.TeamId);
            playerStats[player.Id] = new PlayerSeasonStats
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                JerseyNumber = player.JerseyNumber,
                TeamName = team.Name,
                TeamAbbr = team.Abbreviation,
                TeamColor = team.Color
            };
        }

        // Season averages aggregate only completed games — an in-progress game holds a
        // partial stat line that would otherwise drag down every participant's averages.
        foreach (var game in games.Where(g => g.Status == GameStatus.Finished))
        {
            var events = await _statEventRepository.GetByGameIdAsync(game.Id);
            var homeTeam = teams.FirstOrDefault(t => t.Id == game.HomeTeamId);
            var awayTeam = teams.FirstOrDefault(t => t.Id == game.AwayTeamId);

            var metrics = ComputeGameMetrics(events, game.HomeTeamId, game.AwayTeamId,
                homeTeam?.Players ?? [], awayTeam?.Players ?? [], GameFormat.FromGame(game));

            var playersInGame = events
                .Where(e => e.StatType != StatType.SubIn && e.StatType != StatType.SubOut)
                .Select(e => e.PlayerId)
                .Concat(metrics.PlayerSecondsOnCourt.Keys)
                .Distinct()
                .ToHashSet();

            foreach (var playerId in playersInGame)
            {
                if (!playerStats.ContainsKey(playerId)) continue;

                var ps = playerStats[playerId];
                ps.GamesPlayed++;

                foreach (var e in events.Where(ev => ev.PlayerId == playerId))
                {
                    ApplyStatEvent(ps, e);
                }

                if (metrics.PlayerSecondsOnCourt.TryGetValue(playerId, out var secs) && secs > 0)
                {
                    ps.TotalSecondsOnCourt += secs;
                    ps.GamesWithMinutes++;
                }

                if (metrics.PlayerPlusMinus.TryGetValue(playerId, out var pm))
                {
                    ps.TotalPlusMinus += pm;
                }
            }
        }

        return playerStats.Values
            .Where(p => p.GamesPlayed > 0)
            .OrderByDescending(p => p.TotalPoints / (double)Math.Max(1, p.GamesPlayed))
            .ToList();
    }

    public async Task<List<ShotChartPoint>> GetPlayerShotChartAsync(int playerId, int seasonId)
    {
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var shots = new List<ShotChartPoint>();

        // Only completed games, to stay consistent with the season averages shown alongside.
        foreach (var game in games.Where(g => g.Status == GameStatus.Finished))
        {
            var events = await _statEventRepository.GetByGameIdAsync(game.Id);
            foreach (var e in events.Where(ev =>
                ev.PlayerId == playerId &&
                ev.ShotResult.HasValue &&
                ev.CourtX.HasValue && ev.CourtY.HasValue &&
                (ev.StatType == StatType.Points2 || ev.StatType == StatType.Points3)))
            {
                shots.Add(new ShotChartPoint
                {
                    X = e.CourtX!.Value,
                    Y = e.CourtY!.Value,
                    IsMade = e.ShotResult == ShotResult.Made,
                    Is3Pt = e.StatType == StatType.Points3
                });
            }
        }

        return shots;
    }

    // ── Zone analytics (US-23) ──

    /// <summary>FG% by court zone for one player across the season's completed games.</summary>
    public async Task<List<ZoneStat>> GetPlayerZoneStatsAsync(int playerId, int seasonId)
    {
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var shots = new List<StatEvent>();
        foreach (var game in games.Where(g => g.Status == GameStatus.Finished))
        {
            var events = await _statEventRepository.GetByGameIdAsync(game.Id);
            shots.AddRange(events.Where(e => e.PlayerId == playerId && IsFieldGoalWithLocation(e)));
        }
        return AggregateZones(shots);
    }

    /// <summary>FG% by court zone for one team's shots within a single game.</summary>
    public async Task<List<ZoneStat>> GetGameZoneStatsAsync(int gameId, IReadOnlyCollection<int> teamPlayerIds)
    {
        var events = await _statEventRepository.GetByGameIdAsync(gameId);
        var shots = events.Where(e => teamPlayerIds.Contains(e.PlayerId) && IsFieldGoalWithLocation(e));
        return AggregateZones(shots);
    }

    private static bool IsFieldGoalWithLocation(StatEvent e) =>
        (e.StatType == StatType.Points2 || e.StatType == StatType.Points3)
        && e.CourtX.HasValue && e.CourtY.HasValue;

    private static List<ZoneStat> AggregateZones(IEnumerable<StatEvent> shots)
    {
        var byZone = CourtZones.All.ToDictionary(
            z => z,
            z => new ZoneStat { Zone = z, Label = CourtZones.Label(z), Is3Pt = CourtZones.IsThree(z) });

        foreach (var e in shots)
        {
            var s = byZone[CourtZones.GetZone(e.CourtX!.Value, e.CourtY!.Value)];
            s.Attempts++;
            if (e.ShotResult == ShotResult.Made) s.Made++;
        }

        // Preserve the display order defined in CourtZones.All.
        return CourtZones.All.Select(z => byZone[z]).ToList();
    }

    // ── Build per-game metrics (minutes + plus/minus) from sub/scoring events ──

    private static GameMetrics ComputeGameMetrics(
        IReadOnlyList<StatEvent> events,
        int homeTeamId,
        int awayTeamId,
        ICollection<Player> homePlayers,
        ICollection<Player> awayPlayers,
        GameFormat format)
    {
        var metrics = new GameMetrics();

        // Map player → team side for quick PM attribution
        var playerTeam = new Dictionary<int, int>();
        foreach (var p in homePlayers) playerTeam[p.Id] = homeTeamId;
        foreach (var p in awayPlayers) playerTeam[p.Id] = awayTeamId;

        // Only games with sub events get minutes and PM
        bool hasSubEvents = events.Any(e => e.StatType == StatType.SubIn || e.StatType == StatType.SubOut);
        if (!hasSubEvents) return metrics;

        // Order events by absolute time + id for a stable timeline
        var ordered = events
            .Select(e => new { Event = e, AbsSec = format.ToAbsoluteSeconds(e.Quarter, e.GameClock) })
            .OrderBy(x => x.AbsSec)
            .ThenBy(x => x.Event.Id)
            .ToList();

        // Compute game end: last non-sub event wins; fall back to last event
        int gameEndSec = ordered.Count > 0 ? ordered[^1].AbsSec : 0;

        // Build intervals per player
        var onSince = new Dictionary<int, int>();
        var intervals = new Dictionary<int, List<(int Start, int End)>>();

        foreach (var entry in ordered)
        {
            var e = entry.Event;
            var t = entry.AbsSec;

            if (e.StatType == StatType.SubIn)
            {
                onSince[e.PlayerId] = t;
            }
            else if (e.StatType == StatType.SubOut)
            {
                if (onSince.TryGetValue(e.PlayerId, out var start))
                {
                    if (!intervals.TryGetValue(e.PlayerId, out var list))
                    {
                        list = new List<(int, int)>();
                        intervals[e.PlayerId] = list;
                    }
                    list.Add((start, t));
                    onSince.Remove(e.PlayerId);
                }
            }
        }

        // Close any still-open intervals at game end
        foreach (var (playerId, start) in onSince)
        {
            if (!intervals.TryGetValue(playerId, out var list))
            {
                list = new List<(int, int)>();
                intervals[playerId] = list;
            }
            list.Add((start, gameEndSec));
        }

        // Minutes
        foreach (var (playerId, list) in intervals)
        {
            int total = list.Sum(i => Math.Max(0, i.End - i.Start));
            metrics.PlayerSecondsOnCourt[playerId] = total;
        }

        // Plus/Minus: for each scoring event, credit every on-court player
        foreach (var entry in ordered)
        {
            var e = entry.Event;
            int pts = ScoringPoints(e);
            if (pts == 0) continue;
            if (!playerTeam.TryGetValue(e.PlayerId, out var scoringTeam)) continue;

            int t = entry.AbsSec;

            foreach (var (playerId, list) in intervals)
            {
                if (!IsOnCourtAt(list, t)) continue;
                if (!playerTeam.TryGetValue(playerId, out var side)) continue;

                int delta = side == scoringTeam ? pts : -pts;
                metrics.PlayerPlusMinus.TryGetValue(playerId, out var cur);
                metrics.PlayerPlusMinus[playerId] = cur + delta;
            }
        }

        return metrics;
    }

    private static bool IsOnCourtAt(List<(int Start, int End)> intervals, int t)
    {
        foreach (var (s, e) in intervals)
        {
            if (t >= s && t < e) return true;
        }
        return false;
    }

    private static int ScoringPoints(StatEvent e)
    {
        if (e.ShotResult != ShotResult.Made) return 0;
        return e.StatType switch
        {
            StatType.Points2 => 2,
            StatType.Points3 => 3,
            StatType.FreeThrow => 1,
            _ => 0
        };
    }

    // ── Box-line building ──

    private static List<PlayerBoxLine> BuildBoxLines(
        IReadOnlyList<StatEvent> allEvents,
        ICollection<Player> players,
        GameMetrics metrics)
    {
        var lines = new Dictionary<int, PlayerBoxLine>();
        foreach (var player in players)
        {
            lines[player.Id] = new PlayerBoxLine
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                JerseyNumber = player.JerseyNumber
            };
        }

        foreach (var e in allEvents)
        {
            if (!lines.ContainsKey(e.PlayerId)) continue;
            ApplyStatEventToBox(lines[e.PlayerId], e);
        }

        foreach (var (playerId, line) in lines)
        {
            if (metrics.PlayerSecondsOnCourt.TryGetValue(playerId, out var secs))
                line.SecondsOnCourt = secs;
            if (metrics.PlayerPlusMinus.TryGetValue(playerId, out var pm))
                line.PlusMinus = pm;
        }

        return lines.Values
            .Where(l => l.HasStats || l.SecondsOnCourt > 0)
            .OrderByDescending(l => l.Points)
            .ToList();
    }

    private static void ApplyStatEventToBox(PlayerBoxLine line, StatEvent e)
    {
        switch (e.StatType)
        {
            case StatType.Points2:
                if (e.ShotResult == ShotResult.Made) { line.Fg2Made++; }
                line.Fg2Attempted++;
                break;
            case StatType.Points3:
                if (e.ShotResult == ShotResult.Made) { line.Fg3Made++; }
                line.Fg3Attempted++;
                break;
            case StatType.FreeThrow:
                if (e.ShotResult == ShotResult.Made) { line.FtMade++; }
                line.FtAttempted++;
                break;
            case StatType.OffensiveRebound:
                line.OffRebounds++;
                break;
            case StatType.DefensiveRebound:
                line.DefRebounds++;
                break;
            case StatType.Assist:
                line.Assists++;
                break;
            case StatType.Steal:
                line.Steals++;
                break;
            case StatType.Block:
                line.Blocks++;
                break;
            case StatType.Turnover:
                line.Turnovers++;
                break;
            case StatType.PersonalFoul:
                line.PersonalFouls++;
                break;
            case StatType.TechnicalFoul:
                line.TechnicalFouls++;
                break;
        }
    }

    private static void ApplyStatEvent(PlayerSeasonStats ps, StatEvent e)
    {
        switch (e.StatType)
        {
            case StatType.Points2:
                if (e.ShotResult == ShotResult.Made) { ps.Fg2Made++; ps.TotalPoints += 2; }
                ps.Fg2Attempted++;
                break;
            case StatType.Points3:
                if (e.ShotResult == ShotResult.Made) { ps.Fg3Made++; ps.TotalPoints += 3; }
                ps.Fg3Attempted++;
                break;
            case StatType.FreeThrow:
                if (e.ShotResult == ShotResult.Made) { ps.FtMade++; ps.TotalPoints += 1; }
                ps.FtAttempted++;
                break;
            case StatType.OffensiveRebound:
                ps.TotalOffRebounds++;
                break;
            case StatType.DefensiveRebound:
                ps.TotalDefRebounds++;
                break;
            case StatType.Assist:
                ps.TotalAssists++;
                break;
            case StatType.Steal:
                ps.TotalSteals++;
                break;
            case StatType.Block:
                ps.TotalBlocks++;
                break;
            case StatType.Turnover:
                ps.TotalTurnovers++;
                break;
            case StatType.PersonalFoul:
                ps.TotalPersonalFouls++;
                break;
            case StatType.TechnicalFoul:
                ps.TotalTechnicalFouls++;
                break;
        }
    }

    private class GameMetrics
    {
        public Dictionary<int, int> PlayerSecondsOnCourt { get; } = new();
        public Dictionary<int, int> PlayerPlusMinus { get; } = new();
    }
}

public class SeasonGameSummary
{
    public int GameId { get; set; }
    public DateTime GameDate { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string HomeTeamAbbr { get; set; } = string.Empty;
    public string AwayTeamAbbr { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    /// <summary>True when the game is Finished (appears in the completed-matches list).</summary>
    public bool IsPlayed { get; set; }
    public GameStatus Status { get; set; }
    /// <summary>Any stat events recorded (incl. starting lineup) — i.e. the game was entered.</summary>
    public bool HasEvents { get; set; }
}

public class GameBoxScore
{
    public int GameId { get; set; }
    public DateTime GameDate { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string HomeTeamAbbr { get; set; } = string.Empty;
    public string AwayTeamAbbr { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public List<PlayerBoxLine> HomeLines { get; set; } = [];
    public List<PlayerBoxLine> AwayLines { get; set; } = [];
}

public class PlayerBoxLine
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int JerseyNumber { get; set; }

    public int Fg2Made { get; set; }
    public int Fg2Attempted { get; set; }
    public int Fg3Made { get; set; }
    public int Fg3Attempted { get; set; }
    public int FtMade { get; set; }
    public int FtAttempted { get; set; }
    public int OffRebounds { get; set; }
    public int DefRebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int PersonalFouls { get; set; }
    public int TechnicalFouls { get; set; }
    public int SecondsOnCourt { get; set; }
    public int PlusMinus { get; set; }

    public int Rebounds => OffRebounds + DefRebounds;
    public int Fouls => PersonalFouls + TechnicalFouls;
    public int Points => (Fg2Made * 2) + (Fg3Made * 3) + FtMade;
    public int FgMade => Fg2Made + Fg3Made;
    public int FgAttempted => Fg2Attempted + Fg3Attempted;
    public string FgDisplay => $"{FgMade}/{FgAttempted}";
    public string Fg3Display => $"{Fg3Made}/{Fg3Attempted}";
    public string Fg2Display => $"{Fg2Made}/{Fg2Attempted}";
    public string FtDisplay => $"{FtMade}/{FtAttempted}";

    // Minutes display: "MM:SS" or "—" if not tracked
    public string MinutesDisplay => SecondsOnCourt > 0
        ? $"{SecondsOnCourt / 60}:{SecondsOnCourt % 60:D2}"
        : "—";

    public string PlusMinusDisplay => SecondsOnCourt > 0 ? (PlusMinus >= 0 ? $"+{PlusMinus}" : PlusMinus.ToString()) : "—";

    // Advanced (per-game — a single game)
    public double FgPct => FgAttempted > 0 ? (FgMade * 100.0 / FgAttempted) : 0;
    public double Fg2Pct => Fg2Attempted > 0 ? (Fg2Made * 100.0 / Fg2Attempted) : 0;
    public double Fg3Pct => Fg3Attempted > 0 ? (Fg3Made * 100.0 / Fg3Attempted) : 0;
    public double FtPct => FtAttempted > 0 ? (FtMade * 100.0 / FtAttempted) : 0;
    public double EFgPct => FgAttempted > 0 ? ((FgMade + 0.5 * Fg3Made) * 100.0 / FgAttempted) : 0;
    public double Tsa => FgAttempted + 0.44 * FtAttempted;
    public double TsPct => Tsa > 0 ? (Points * 100.0 / (2 * Tsa)) : 0;
    public double AssistToTurnover => Turnovers > 0 ? Assists / (double)Turnovers : Assists;
    public int Efficiency =>
        (Points + Rebounds + Assists + Steals + Blocks)
        - ((FgAttempted - FgMade) + (FtAttempted - FtMade) + Turnovers);
    public double GameScore =>
        Points + 0.4 * FgMade - 0.7 * FgAttempted - 0.4 * (FtAttempted - FtMade)
        + 0.7 * OffRebounds + 0.3 * DefRebounds + Steals + 0.7 * Assists
        + 0.7 * Blocks - 0.4 * PersonalFouls - Turnovers;

    public bool HasStats => FgAttempted > 0 || FtAttempted > 0 || Rebounds > 0 || Assists > 0 ||
                            Steals > 0 || Blocks > 0 || Turnovers > 0 || Fouls > 0;
}

public class PlayerSeasonStats
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int JerseyNumber { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string TeamAbbr { get; set; } = string.Empty;
    public string TeamColor { get; set; } = "#000000";
    public int GamesPlayed { get; set; }
    public int GamesWithMinutes { get; set; }

    public int TotalPoints { get; set; }
    public int TotalOffRebounds { get; set; }
    public int TotalDefRebounds { get; set; }
    public int TotalAssists { get; set; }
    public int TotalSteals { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalTurnovers { get; set; }
    public int TotalPersonalFouls { get; set; }
    public int TotalTechnicalFouls { get; set; }
    public int TotalSecondsOnCourt { get; set; }
    public int TotalPlusMinus { get; set; }

    public int Fg2Made { get; set; }
    public int Fg2Attempted { get; set; }
    public int Fg3Made { get; set; }
    public int Fg3Attempted { get; set; }
    public int FtMade { get; set; }
    public int FtAttempted { get; set; }

    private int Gp => Math.Max(1, GamesPlayed);

    public int TotalRebounds => TotalOffRebounds + TotalDefRebounds;
    public int TotalFouls => TotalPersonalFouls + TotalTechnicalFouls;

    public string Ppg => (TotalPoints / (double)Gp).ToString("F1");
    public string Rpg => (TotalRebounds / (double)Gp).ToString("F1");
    public string Orpg => (TotalOffRebounds / (double)Gp).ToString("F1");
    public string Drpg => (TotalDefRebounds / (double)Gp).ToString("F1");
    public string Apg => (TotalAssists / (double)Gp).ToString("F1");
    public string Spg => (TotalSteals / (double)Gp).ToString("F1");
    public string Bpg => (TotalBlocks / (double)Gp).ToString("F1");
    public string Topg => (TotalTurnovers / (double)Gp).ToString("F1");
    public string PfPg => (TotalPersonalFouls / (double)Gp).ToString("F1");
    public string TfPg => (TotalTechnicalFouls / (double)Gp).ToString("F1");

    // Minutes averaged only over games where minutes were actually tracked
    public string MpgDisplay =>
        GamesWithMinutes > 0 ? (TotalSecondsOnCourt / 60.0 / GamesWithMinutes).ToString("F1") : "—";

    public string TotalMinutesDisplay =>
        GamesWithMinutes > 0 ? $"{TotalSecondsOnCourt / 60}" : "—";

    public string PlusMinusDisplay =>
        GamesWithMinutes > 0 ? (TotalPlusMinus >= 0 ? $"+{TotalPlusMinus}" : TotalPlusMinus.ToString()) : "—";

    public int FgMade => Fg2Made + Fg3Made;
    public int FgAttempted => Fg2Attempted + Fg3Attempted;
    public double FgPct => FgAttempted > 0 ? (FgMade * 100.0 / FgAttempted) : 0;
    public double Fg2Pct => Fg2Attempted > 0 ? (Fg2Made * 100.0 / Fg2Attempted) : 0;
    public double Fg3Pct => Fg3Attempted > 0 ? (Fg3Made * 100.0 / Fg3Attempted) : 0;
    public double FtPct => FtAttempted > 0 ? (FtMade * 100.0 / FtAttempted) : 0;

    public string FgmPg => (FgMade / (double)Gp).ToString("F1");
    public string FgaPg => (FgAttempted / (double)Gp).ToString("F1");
    public string Fg2MPg => (Fg2Made / (double)Gp).ToString("F1");
    public string Fg2APg => (Fg2Attempted / (double)Gp).ToString("F1");
    public string Fg3MPg => (Fg3Made / (double)Gp).ToString("F1");
    public string Fg3APg => (Fg3Attempted / (double)Gp).ToString("F1");
    public string FtmPg => (FtMade / (double)Gp).ToString("F1");
    public string FtaPg => (FtAttempted / (double)Gp).ToString("F1");

    // Advanced
    public double AssistToTurnover => TotalTurnovers > 0 ? TotalAssists / (double)TotalTurnovers : TotalAssists;
    public string AtDisplay => AssistToTurnover.ToString("F2");

    public double EFgPct => FgAttempted > 0 ? ((FgMade + 0.5 * Fg3Made) * 100.0 / FgAttempted) : 0;
    public double Tsa => FgAttempted + 0.44 * FtAttempted;
    public string TsaPg => (Tsa / Gp).ToString("F1");
    public double TsPct => Tsa > 0 ? (TotalPoints * 100.0 / (2 * Tsa)) : 0;

    // Efficiency is typically shown per-game
    public double EffPerGame =>
        ((TotalPoints + TotalRebounds + TotalAssists + TotalSteals + TotalBlocks)
         - ((FgAttempted - FgMade) + (FtAttempted - FtMade) + TotalTurnovers)) / (double)Gp;
    public string EffDisplay => EffPerGame.ToString("F1");

    // Game Score averaged per game
    public double GmScPerGame =>
        (TotalPoints + 0.4 * FgMade - 0.7 * FgAttempted - 0.4 * (FtAttempted - FtMade)
         + 0.7 * TotalOffRebounds + 0.3 * TotalDefRebounds + TotalSteals + 0.7 * TotalAssists
         + 0.7 * TotalBlocks - 0.4 * TotalPersonalFouls - TotalTurnovers) / (double)Gp;
    public string GmScDisplay => GmScPerGame.ToString("F1");
}

public class ShotChartPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsMade { get; set; }
    public bool Is3Pt { get; set; }
}

/// <summary>Per-zone shooting line for the zone heat chart (US-23).</summary>
public class ZoneStat
{
    public CourtZone Zone { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Is3Pt { get; set; }
    public int Made { get; set; }
    public int Attempts { get; set; }

    public double Pct => Attempts > 0 ? Made * 100.0 / Attempts : 0;
    public bool HasShots => Attempts > 0;
    public string Display => Attempts > 0 ? $"{Made}/{Attempts}" : "—";
    public string PctDisplay => Attempts > 0 ? $"{Pct:F0}%" : "";

    /// <summary>Cold→hot hex from FG%: grey when there's no data, then blue → amber → red as
    /// the percentage climbs. Used by both the on-screen chart and (later) the PDF.</summary>
    public string HeatColor
    {
        get
        {
            if (Attempts == 0) return "#161616";
            double t = Math.Clamp(Pct / 100.0, 0, 1);
            var (r, g, b) = t < 0.5
                ? Lerp(59, 130, 246, 251, 191, 36, t / 0.5)   // blue → amber
                : Lerp(251, 191, 36, 239, 68, 68, (t - 0.5) / 0.5); // amber → red
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    private static (int R, int G, int B) Lerp(int r1, int g1, int b1, int r2, int g2, int b2, double t)
        => ((int)(r1 + (r2 - r1) * t), (int)(g1 + (g2 - g1) * t), (int)(b1 + (b2 - b1) * t));
}
