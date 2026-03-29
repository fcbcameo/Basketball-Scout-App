namespace BasketballScout.Core.Models;

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public ICollection<Game> Games { get; set; } = new List<Game>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
