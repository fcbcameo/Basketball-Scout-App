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
