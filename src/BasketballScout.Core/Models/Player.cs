using BasketballScout.Core.Enums;

namespace BasketballScout.Core.Models;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int JerseyNumber { get; set; }
    public Position Position { get; set; }
    public bool IsActive { get; set; } = true;

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
}
