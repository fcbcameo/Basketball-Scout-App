namespace BasketballScout.Core.Models;

public class Game
{
    public int Id { get; set; }
    public DateTime GameDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    public ICollection<StatEvent> StatEvents { get; set; } = new List<StatEvent>();
    public ICollection<QuarterScore> QuarterScores { get; set; } = new List<QuarterScore>();
}
