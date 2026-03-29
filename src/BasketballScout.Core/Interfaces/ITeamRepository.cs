using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(int id);
    Task<IReadOnlyList<Team>> GetBySeasonIdAsync(int seasonId);
    Task<Team> AddAsync(Team team);
    Task UpdateAsync(Team team);
    Task DeleteAsync(int id);
}
