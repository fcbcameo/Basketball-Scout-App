using BasketballScout.Core.Enums;

namespace BasketballScout.Core.Models;

public class Game
{
    public int Id { get; set; }
    public DateTime GameDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // ── Live game lifecycle (US-10) ──
    /// <summary>InProgress until explicitly finished. Drives resume vs. completed-matches.</summary>
    public GameStatus Status { get; set; } = GameStatus.InProgress;

    /// <summary>Seconds left on the game clock when the scorer last left the game — for exact resume.</summary>
    public int ClockSecondsRemaining { get; set; } = 600;

    /// <summary>Current period when last left: 1–4 regulation, 5+ overtime.</summary>
    public int CurrentPeriod { get; set; } = 1;

    /// <summary>Stable identity stamped at creation (US-19). Travels in export bundles so a
    /// re-import of the same game can be detected as a duplicate. Null for legacy games.</summary>
    public string? ExportGuid { get; set; }

    // ── Game format snapshot (US-21) ── Copied from the Season at creation so the clock,
    // minutes and +/- math use this game's own lengths even if the season is edited later.
    public int PeriodLengthSeconds { get; set; } = 600;
    public int OvertimeLengthSeconds { get; set; } = 300;
    public int RegulationPeriods { get; set; } = 4;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    public ICollection<StatEvent> StatEvents { get; set; } = new List<StatEvent>();
    public ICollection<QuarterScore> QuarterScores { get; set; } = new List<QuarterScore>();
}
