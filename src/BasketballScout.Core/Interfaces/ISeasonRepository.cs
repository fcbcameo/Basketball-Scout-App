using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(int id);
    Task<IReadOnlyList<Season>> GetAllAsync();
    Task<Season> AddAsync(Season season);
    Task UpdateAsync(Season season);
    Task DeleteAsync(int id);
}
