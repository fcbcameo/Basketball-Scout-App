using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly ScoutDbContext _db;

    public TeamRepository(ScoutDbContext db)
    {
        _db = db;
    }

    public async Task<Team?> GetByIdAsync(int id)
        => await _db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<Team>> GetBySeasonIdAsync(int seasonId)
        => await _db.Teams
            .Include(t => t.Players)
            .Where(t => t.SeasonId == seasonId)
            .OrderBy(t => t.Name)
            .ToListAsync();

    public async Task<Team> AddAsync(Team team)
    {
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();
        return team;
    }

    public async Task UpdateAsync(Team team)
    {
        _db.Teams.Update(team);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var team = await _db.Teams.FindAsync(id);
        if (team is not null)
        {
            _db.Teams.Remove(team);
            await _db.SaveChangesAsync();
        }
    }
}
