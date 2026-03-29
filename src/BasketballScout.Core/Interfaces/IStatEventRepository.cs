using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface IStatEventRepository
{
    Task<StatEvent?> GetByIdAsync(int id);
    Task<IReadOnlyList<StatEvent>> GetByGameIdAsync(int gameId);
    Task<IReadOnlyList<StatEvent>> GetByPlayerIdAsync(int playerId);
    Task<StatEvent> AddAsync(StatEvent statEvent);
    Task UpdateAsync(StatEvent statEvent);
    Task DeleteAsync(int id);
    Task<StatEvent?> GetLastByGameIdAsync(int gameId);
}
