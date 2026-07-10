using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly ScoutDbContextProvider _ctx;

    public TeamRepository(ScoutDbContextProvider ctx)
    {
        _ctx = ctx;
    }

    public async Task<Team?> GetByIdAsync(int id)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Teams
            .Include(t => t.Players)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IReadOnlyList<Team>> GetBySeasonIdAsync(int seasonId)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Teams
            .Include(t => t.Players)
            .Where(t => t.SeasonId == seasonId)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Team> AddAsync(Team team)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Teams.Add(team);
        await lease.Db.SaveChangesAsync();
        return team;
    }

    public async Task UpdateAsync(Team team)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Teams.Update(team);
        await lease.Db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var lease = _ctx.Lease();
        var team = await lease.Db.Teams.FindAsync(id);
        if (team is not null)
        {
            lease.Db.Teams.Remove(team);
            await lease.Db.SaveChangesAsync();
        }
    }
}
