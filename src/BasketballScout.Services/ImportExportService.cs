using System.Text.Json;
using System.Text.Json.Serialization;
using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;

namespace BasketballScout.Services;

public class ImportExportService
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IStatEventRepository _statEventRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ImportExportService(
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        IGameRepository gameRepository,
        IStatEventRepository statEventRepository)
    {
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _gameRepository = gameRepository;
        _statEventRepository = statEventRepository;
    }

    public async Task<string> ExportSeasonAsync(int seasonId)
    {
        var season = await _seasonRepository.GetByIdAsync(seasonId);
        if (season is null) throw new InvalidOperationException("Season not found");

        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);

        var exportTeams = new List<TeamExport>();
        foreach (var team in teams)
        {
            var players = await _playerRepository.GetByTeamIdAsync(team.Id);
            exportTeams.Add(new TeamExport
            {
                Name = team.Name,
                Abbreviation = team.Abbreviation,
                Color = team.Color,
                Players = players.Select(p => new PlayerExport
                {
                    Name = p.Name,
                    JerseyNumber = p.JerseyNumber,
                    Position = p.Position,
                    IsActive = p.IsActive
                }).ToList()
            });
        }

        var exportGames = new List<GameExport>();
        foreach (var game in games)
        {
            var homeTeam = teams.FirstOrDefault(t => t.Id == game.HomeTeamId);
            var awayTeam = teams.FirstOrDefault(t => t.Id == game.AwayTeamId);
            var events = await _statEventRepository.GetByGameIdAsync(game.Id);

            exportGames.Add(new GameExport
            {
                GameDate = game.GameDate,
                Location = game.Location,
                Notes = game.Notes,
                HomeTeamName = homeTeam?.Name ?? "",
                AwayTeamName = awayTeam?.Name ?? "",
                Events = events.Select(e =>
                {
                    // Resolve player name from teams
                    var player = teams.SelectMany(t => t.Players)
                        .FirstOrDefault(p => p.Id == e.PlayerId);
                    return new StatEventExport
                    {
                        PlayerName = player?.Name ?? "",
                        PlayerJersey = player?.JerseyNumber ?? 0,
                        TeamName = teams.FirstOrDefault(t => t.Players.Any(p => p.Id == e.PlayerId))?.Name ?? "",
                        StatType = e.StatType,
                        ShotResult = e.ShotResult,
                        CourtX = e.CourtX,
                        CourtY = e.CourtY,
                        Quarter = e.Quarter,
                        GameClock = e.GameClock,
                        Timestamp = e.Timestamp
                    };
                }).ToList()
            });
        }

        var export = new SeasonExport
        {
            Version = 1,
            ExportDate = DateTime.UtcNow,
            Season = new SeasonDataExport
            {
                Name = season.Name,
                StartDate = season.StartDate,
                EndDate = season.EndDate
            },
            Teams = exportTeams,
            Games = exportGames
        };

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    public async Task<int> ImportSeasonAsync(string json)
    {
        var import = JsonSerializer.Deserialize<SeasonExport>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid JSON format");

        // Create season
        var season = await _seasonRepository.AddAsync(new Season
        {
            Name = import.Season.Name + " (imported)",
            StartDate = import.Season.StartDate,
            EndDate = import.Season.EndDate
        });

        // Create teams and players, tracking name → ID mappings
        var teamIdByName = new Dictionary<string, int>();
        var playerIdByKey = new Dictionary<string, int>(); // "teamName|playerName|jersey" → id

        foreach (var teamExport in import.Teams)
        {
            var team = await _teamRepository.AddAsync(new Team
            {
                Name = teamExport.Name,
                Abbreviation = teamExport.Abbreviation,
                Color = teamExport.Color,
                SeasonId = season.Id
            });
            teamIdByName[teamExport.Name] = team.Id;

            foreach (var playerExport in teamExport.Players)
            {
                var player = await _playerRepository.AddAsync(new Player
                {
                    Name = playerExport.Name,
                    JerseyNumber = playerExport.JerseyNumber,
                    Position = playerExport.Position,
                    IsActive = playerExport.IsActive,
                    TeamId = team.Id
                });
                playerIdByKey[$"{teamExport.Name}|{playerExport.Name}|{playerExport.JerseyNumber}"] = player.Id;
            }
        }

        // Create games and stat events
        foreach (var gameExport in import.Games)
        {
            var homeTeamId = teamIdByName.GetValueOrDefault(gameExport.HomeTeamName);
            var awayTeamId = teamIdByName.GetValueOrDefault(gameExport.AwayTeamName);
            if (homeTeamId == 0 || awayTeamId == 0) continue;

            var game = await _gameRepository.AddAsync(new Game
            {
                GameDate = gameExport.GameDate,
                Location = gameExport.Location,
                Notes = gameExport.Notes,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                SeasonId = season.Id
            });

            foreach (var eventExport in gameExport.Events)
            {
                var key = $"{eventExport.TeamName}|{eventExport.PlayerName}|{eventExport.PlayerJersey}";
                var playerId = playerIdByKey.GetValueOrDefault(key);
                if (playerId == 0) continue;

                await _statEventRepository.AddAsync(new StatEvent
                {
                    PlayerId = playerId,
                    GameId = game.Id,
                    StatType = eventExport.StatType,
                    ShotResult = eventExport.ShotResult,
                    CourtX = eventExport.CourtX,
                    CourtY = eventExport.CourtY,
                    Quarter = eventExport.Quarter,
                    GameClock = eventExport.GameClock,
                    Timestamp = eventExport.Timestamp
                });
            }
        }

        return season.Id;
    }

    // ── Single-game bundle (US-14) ──

    /// <summary>
    /// Exports one game as a self-contained JSON bundle: the game's lifecycle fields,
    /// both teams with their full rosters, and every stat event (including shot
    /// locations and assist/rebound links). Designed to import cleanly onto a device
    /// that has never seen this game. Per-quarter scores are derived from event data,
    /// so they are not stored separately.
    /// </summary>
    public async Task<string> ExportGameAsync(int gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId)
            ?? throw new InvalidOperationException("Game not found");

        var homeRoster = await _playerRepository.GetByTeamIdAsync(game.HomeTeamId);
        var awayRoster = await _playerRepository.GetByTeamIdAsync(game.AwayTeamId);

        // playerId → (which side, name, jersey) so each event can be re-attributed on import.
        var playerInfo = new Dictionary<int, (bool IsHome, string Name, int Jersey)>();
        foreach (var p in homeRoster) playerInfo[p.Id] = (true, p.Name, p.JerseyNumber);
        foreach (var p in awayRoster) playerInfo[p.Id] = (false, p.Name, p.JerseyNumber);

        var events = (await _statEventRepository.GetByGameIdAsync(gameId))
            .OrderBy(e => e.Timestamp).ThenBy(e => e.Id)
            .ToList();

        // Give each event a stable local id, then express LinkedEventId as a local id so
        // the assist→shot / rebound→miss links survive the re-keying done on import.
        var localIdByEventId = new Dictionary<int, int>();
        for (int i = 0; i < events.Count; i++)
            localIdByEventId[events[i].Id] = i;

        var bundle = new GameBundle
        {
            Version = 1,
            ExportDate = DateTime.UtcNow,
            Game = new GameDataExport
            {
                GameDate = game.GameDate,
                Location = game.Location,
                Notes = game.Notes,
                Status = game.Status,
                ClockSecondsRemaining = game.ClockSecondsRemaining,
                CurrentPeriod = game.CurrentPeriod
            },
            HomeTeam = ToTeamBundle(game.HomeTeam, homeRoster),
            AwayTeam = ToTeamBundle(game.AwayTeam, awayRoster),
            Events = events.Select(e =>
            {
                playerInfo.TryGetValue(e.PlayerId, out var info);
                int? linkedLocalId = e.LinkedEventId is int linkedId
                    && localIdByEventId.TryGetValue(linkedId, out var ll) ? ll : null;
                return new GameStatEventExport
                {
                    LocalId = localIdByEventId[e.Id],
                    IsHomeTeam = info.IsHome,
                    PlayerName = info.Name,
                    PlayerJersey = info.Jersey,
                    StatType = e.StatType,
                    ShotResult = e.ShotResult,
                    CourtX = e.CourtX,
                    CourtY = e.CourtY,
                    Quarter = e.Quarter,
                    GameClock = e.GameClock,
                    Timestamp = e.Timestamp,
                    LinkedLocalId = linkedLocalId
                };
            }).ToList()
        };

        return JsonSerializer.Serialize(bundle, JsonOptions);
    }

    private static TeamBundle ToTeamBundle(Team team, IReadOnlyList<Player> roster) => new()
    {
        Name = team.Name,
        Abbreviation = team.Abbreviation,
        Color = team.Color,
        Players = roster.Select(p => new PlayerExport
        {
            Name = p.Name,
            JerseyNumber = p.JerseyNumber,
            Position = p.Position,
            IsActive = p.IsActive
        }).ToList()
    };

    /// <summary>
    /// Imports a single-game bundle into an existing season. Teams are matched to
    /// existing teams in the target season by name (case-insensitive) and reused, or
    /// created when absent; players are likewise matched within their team by name and
    /// reused or created. So re-importing never duplicates a roster and never throws on
    /// an "already exists" clash — the new-vs-existing case is handled for every
    /// embedded team/player. The game itself is always added as a fresh copy (a
    /// re-import adds another game rather than overwriting). Returns the new game id.
    /// </summary>
    public async Task<GameImportResult> ImportGameAsync(string json, int targetSeasonId)
    {
        GameBundle? bundle;
        try
        {
            bundle = JsonSerializer.Deserialize<GameBundle>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("This file isn't a valid game export.", ex);
        }

        if (bundle is null || bundle.Game is null || bundle.HomeTeam is null || bundle.AwayTeam is null)
            throw new InvalidOperationException("This file isn't a valid game export.");

        _ = await _seasonRepository.GetByIdAsync(targetSeasonId)
            ?? throw new InvalidOperationException("Target season not found");

        var result = new GameImportResult();

        // Match-or-create both teams (with their rosters) in the target season.
        var existingTeams = await _teamRepository.GetBySeasonIdAsync(targetSeasonId);
        var home = await ResolveTeamAsync(bundle.HomeTeam, existingTeams, targetSeasonId, result);
        var away = await ResolveTeamAsync(bundle.AwayTeam, existingTeams, targetSeasonId, result);

        var game = await _gameRepository.AddAsync(new Game
        {
            GameDate = bundle.Game.GameDate,
            Location = bundle.Game.Location,
            Notes = bundle.Game.Notes,
            Status = bundle.Game.Status,
            ClockSecondsRemaining = bundle.Game.ClockSecondsRemaining,
            CurrentPeriod = bundle.Game.CurrentPeriod,
            SeasonId = targetSeasonId,
            HomeTeamId = home.TeamId,
            AwayTeamId = away.TeamId
        });
        result.GameId = game.Id;

        // Pass 1: insert every event without its link, mapping localId → new event id.
        var newIdByLocalId = new Dictionary<int, int>();
        var linkers = new List<(int NewId, int LinkedLocalId)>();

        foreach (var ev in bundle.Events.OrderBy(e => e.LocalId))
        {
            var map = ev.IsHomeTeam ? home.PlayerIdByName : away.PlayerIdByName;
            if (!map.TryGetValue(ev.PlayerName, out var playerId)) continue;

            var newEv = await _statEventRepository.AddAsync(new StatEvent
            {
                GameId = game.Id,
                PlayerId = playerId,
                StatType = ev.StatType,
                ShotResult = ev.ShotResult,
                CourtX = ev.CourtX,
                CourtY = ev.CourtY,
                Quarter = ev.Quarter,
                GameClock = ev.GameClock,
                Timestamp = ev.Timestamp
            });

            newIdByLocalId[ev.LocalId] = newEv.Id;
            if (ev.LinkedLocalId is int linked) linkers.Add((newEv.Id, linked));
        }
        result.EventsImported = newIdByLocalId.Count;

        // Pass 2: wire up assist→shot / rebound→miss links now that all ids exist.
        foreach (var (newId, linkedLocalId) in linkers)
        {
            if (!newIdByLocalId.TryGetValue(linkedLocalId, out var linkedNewId)) continue;
            var ev = await _statEventRepository.GetByIdAsync(newId);
            if (ev is null) continue;
            ev.LinkedEventId = linkedNewId;
            await _statEventRepository.UpdateAsync(ev);
        }

        return result;
    }

    /// <summary>Finds the bundle team in the target season by name (case-insensitive),
    /// or creates it; then match-or-creates each roster player by name within that team.
    /// Tallies matched/created counts into <paramref name="result"/> and returns the
    /// resolved team id with a name→playerId map covering every embedded player.</summary>
    private async Task<(int TeamId, Dictionary<string, int> PlayerIdByName)> ResolveTeamAsync(
        TeamBundle bundleTeam, IReadOnlyList<Team> existingTeams, int targetSeasonId, GameImportResult result)
    {
        var existing = existingTeams.FirstOrDefault(t =>
            string.Equals(t.Name, bundleTeam.Name, StringComparison.OrdinalIgnoreCase));

        int teamId;
        if (existing is not null)
        {
            teamId = existing.Id;
            result.TeamsMatched++;
        }
        else
        {
            var created = await _teamRepository.AddAsync(new Team
            {
                Name = bundleTeam.Name,
                Abbreviation = bundleTeam.Abbreviation,
                Color = bundleTeam.Color,
                SeasonId = targetSeasonId
            });
            teamId = created.Id;
            result.TeamsCreated++;
        }

        var roster = await _playerRepository.GetByTeamIdAsync(teamId);
        var playerIdByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in roster) playerIdByName[p.Name] = p.Id;

        foreach (var bp in bundleTeam.Players)
        {
            if (playerIdByName.ContainsKey(bp.Name))
            {
                result.PlayersMatched++; // existing player → reuse
                continue;
            }
            var created = await _playerRepository.AddAsync(new Player
            {
                Name = bp.Name,
                JerseyNumber = bp.JerseyNumber,
                Position = bp.Position,
                IsActive = bp.IsActive,
                TeamId = teamId
            });
            playerIdByName[bp.Name] = created.Id;
            result.PlayersCreated++;
        }

        return (teamId, playerIdByName);
    }
}

// Export DTOs
public class SeasonExport
{
    public int Version { get; set; } = 1;
    public DateTime ExportDate { get; set; }
    public SeasonDataExport Season { get; set; } = new();
    public List<TeamExport> Teams { get; set; } = [];
    public List<GameExport> Games { get; set; } = [];
}

public class SeasonDataExport
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class TeamExport
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
    public List<PlayerExport> Players { get; set; } = [];
}

public class PlayerExport
{
    public string Name { get; set; } = string.Empty;
    public int JerseyNumber { get; set; }
    public Position Position { get; set; }
    public bool IsActive { get; set; } = true;
}

public class GameExport
{
    public DateTime GameDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public List<StatEventExport> Events { get; set; } = [];
}

public class StatEventExport
{
    public string PlayerName { get; set; } = string.Empty;
    public int PlayerJersey { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public StatType StatType { get; set; }
    public ShotResult? ShotResult { get; set; }
    public float? CourtX { get; set; }
    public float? CourtY { get; set; }
    public int Quarter { get; set; }
    public string GameClock { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>Outcome of a single-game import — surfaced so the UI can report exactly
/// what happened (teams/players matched vs created, events imported).</summary>
public class GameImportResult
{
    public int GameId { get; set; }
    public int TeamsMatched { get; set; }
    public int TeamsCreated { get; set; }
    public int PlayersMatched { get; set; }
    public int PlayersCreated { get; set; }
    public int EventsImported { get; set; }
}

// Single-game bundle DTOs (US-14)
public class GameBundle
{
    public int Version { get; set; } = 1;
    public DateTime ExportDate { get; set; }
    public GameDataExport Game { get; set; } = new();
    public TeamBundle HomeTeam { get; set; } = new();
    public TeamBundle AwayTeam { get; set; } = new();
    public List<GameStatEventExport> Events { get; set; } = [];
}

public class GameDataExport
{
    public DateTime GameDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public int ClockSecondsRemaining { get; set; }
    public int CurrentPeriod { get; set; }
}

public class TeamBundle
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
    public List<PlayerExport> Players { get; set; } = [];
}

public class GameStatEventExport
{
    /// <summary>Stable per-bundle id (0..n-1) used to express event links portably.</summary>
    public int LocalId { get; set; }
    /// <summary>True if the acting player is on the home team — picks the roster to resolve against.</summary>
    public bool IsHomeTeam { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int PlayerJersey { get; set; }
    public StatType StatType { get; set; }
    public ShotResult? ShotResult { get; set; }
    public float? CourtX { get; set; }
    public float? CourtY { get; set; }
    public int Quarter { get; set; }
    public string GameClock { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    /// <summary>LocalId of the event this one links to (assist→shot, rebound→miss), if any.</summary>
    public int? LinkedLocalId { get; set; }
}
