using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly ScoutDbContextProvider _ctx;

    public PlayerRepository(ScoutDbContextProvider ctx)
    {
        _ctx = ctx;
    }

    public async Task<Player?> GetByIdAsync(int id)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Players.FindAsync(id);
    }

    public async Task<IReadOnlyList<Player>> GetByTeamIdAsync(int teamId)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Players
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.JerseyNumber)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Player> AddAsync(Player player)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Players.Add(player);
        await lease.Db.SaveChangesAsync();
        return player;
    }

    public async Task UpdateAsync(Player player)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Players.Update(player);
        await lease.Db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var lease = _ctx.Lease();
        var player = await lease.Db.Players.FindAsync(id);
        if (player is not null)
        {
            lease.Db.Players.Remove(player);
            await lease.Db.SaveChangesAsync();
        }
    }
}
