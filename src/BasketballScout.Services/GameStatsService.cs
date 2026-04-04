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

        var homeLines = BuildBoxLines(events, homeTeam?.Players ?? []);
        var awayLines = BuildBoxLines(events, awayTeam?.Players ?? []);

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

        foreach (var game in games)
        {
            var events = await _statEventRepository.GetByGameIdAsync(game.Id);
            var playersInGame = events.Select(e => e.PlayerId).Distinct().ToHashSet();

            foreach (var playerId in playersInGame)
            {
                if (!playerStats.ContainsKey(playerId)) continue;

                var ps = playerStats[playerId];
                ps.GamesPlayed++;

                foreach (var e in events.Where(ev => ev.PlayerId == playerId))
                {
                    ApplyStatEvent(ps, e);
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

        foreach (var game in games)
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

    private static List<PlayerBoxLine> BuildBoxLines(IReadOnlyList<StatEvent> allEvents, ICollection<Player> players)
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

        return lines.Values
            .Where(l => l.HasStats)
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
            case StatType.DefensiveRebound:
                line.Rebounds++;
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
            case StatType.TechnicalFoul:
                line.Fouls++;
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
            case StatType.DefensiveRebound:
                ps.TotalRebounds++;
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
        }
    }
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
    public int Rebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int Fouls { get; set; }

    public int Points => (Fg2Made * 2) + (Fg3Made * 3) + FtMade;
    public int FgMade => Fg2Made + Fg3Made;
    public int FgAttempted => Fg2Attempted + Fg3Attempted;
    public string FgDisplay => $"{FgMade}/{FgAttempted}";
    public string Fg3Display => $"{Fg3Made}/{Fg3Attempted}";
    public string FtDisplay => $"{FtMade}/{FtAttempted}";
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

    public int TotalPoints { get; set; }
    public int TotalRebounds { get; set; }
    public int TotalAssists { get; set; }
    public int TotalSteals { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalTurnovers { get; set; }

    public int Fg2Made { get; set; }
    public int Fg2Attempted { get; set; }
    public int Fg3Made { get; set; }
    public int Fg3Attempted { get; set; }
    public int FtMade { get; set; }
    public int FtAttempted { get; set; }

    private int Gp => Math.Max(1, GamesPlayed);
    public string Ppg => (TotalPoints / (double)Gp).ToString("F1");
    public string Rpg => (TotalRebounds / (double)Gp).ToString("F1");
    public string Apg => (TotalAssists / (double)Gp).ToString("F1");
    public string Spg => (TotalSteals / (double)Gp).ToString("F1");
    public string Bpg => (TotalBlocks / (double)Gp).ToString("F1");
    public string Topg => (TotalTurnovers / (double)Gp).ToString("F1");

    public int FgMade => Fg2Made + Fg3Made;
    public int FgAttempted => Fg2Attempted + Fg3Attempted;
    public double FgPct => FgAttempted > 0 ? (FgMade * 100.0 / FgAttempted) : 0;
    public double Fg3Pct => Fg3Attempted > 0 ? (Fg3Made * 100.0 / Fg3Attempted) : 0;
    public double FtPct => FtAttempted > 0 ? (FtMade * 100.0 / FtAttempted) : 0;
}

public class ShotChartPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsMade { get; set; }
    public bool Is3Pt { get; set; }
}
