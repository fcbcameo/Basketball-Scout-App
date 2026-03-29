using BasketballScout.Core.Enums;

namespace BasketballScout.Core.Models;

public class StatEvent
{
    public int Id { get; set; }
    public StatType StatType { get; set; }
    public ShotResult? ShotResult { get; set; }
    public float? CourtX { get; set; }
    public float? CourtY { get; set; }
    public int Quarter { get; set; }
    public string GameClock { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    public int? LinkedEventId { get; set; }
    public StatEvent? LinkedEvent { get; set; }
}
