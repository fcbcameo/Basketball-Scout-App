using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(int id);
    Task<IReadOnlyList<Player>> GetByTeamIdAsync(int teamId);
    Task<Player> AddAsync(Player player);
    Task UpdateAsync(Player player);
    Task DeleteAsync(int id);
}
