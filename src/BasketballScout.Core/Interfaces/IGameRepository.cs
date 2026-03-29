using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(int id);
    Task<IReadOnlyList<Game>> GetBySeasonIdAsync(int seasonId);
    Task<Game> AddAsync(Game game);
    Task UpdateAsync(Game game);
    Task DeleteAsync(int id);
}
