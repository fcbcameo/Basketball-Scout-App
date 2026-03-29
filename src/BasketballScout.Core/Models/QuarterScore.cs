namespace BasketballScout.Core.Models;

public class QuarterScore
{
    public int Id { get; set; }
    public int Quarter { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
}
