namespace BasketballScout.Core.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";

    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public ICollection<Player> Players { get; set; } = new List<Player>();
}
