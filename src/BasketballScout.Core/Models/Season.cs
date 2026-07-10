namespace BasketballScout.Core.Models;

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // ── Game format (US-21) ── Defaults are FIBA: 4 × 10:00 regulation, 5:00 overtime.
    // Copied onto each Game at creation, so changing these later never rewrites history.
    public int PeriodLengthMinutes { get; set; } = 10;
    public int PeriodCount { get; set; } = 4;
    public int OvertimeLengthMinutes { get; set; } = 5;

    public ICollection<Game> Games { get; set; } = new List<Game>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
