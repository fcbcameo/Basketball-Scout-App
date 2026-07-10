using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly ScoutDbContextProvider _ctx;

    public SeasonRepository(ScoutDbContextProvider ctx)
    {
        _ctx = ctx;
    }

    public async Task<Season?> GetByIdAsync(int id)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Seasons
            .Include(s => s.Teams)
            .Include(s => s.Games)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IReadOnlyList<Season>> GetAllAsync()
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Seasons
            .OrderByDescending(s => s.StartDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Season> AddAsync(Season season)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Seasons.Add(season);
        await lease.Db.SaveChangesAsync();
        return season;
    }

    public async Task UpdateAsync(Season season)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Seasons.Update(season);
        await lease.Db.SaveChangesAsync();
    }

    /// <summary>
    /// Deep cascade delete (US-12). Children are removed explicitly in dependency order
    /// inside a transaction (its own when standalone, or the ambient one within a unit of
    /// work), because relying on SQLite's own cascades can trip the Restrict FKs
    /// (Game→Team, StatEvent→Player) depending on cascade order.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        await using var lease = _ctx.Lease();
        if (lease.Owned)
        {
            await using var tx = await lease.Db.Database.BeginTransactionAsync();
            await DeleteCoreAsync(lease.Db, id);
            await tx.CommitAsync();
        }
        else
        {
            await DeleteCoreAsync(lease.Db, id);
        }
    }

    private static async Task DeleteCoreAsync(ScoutDbContext db, int id)
    {
        var gameIds = db.Games.Where(g => g.SeasonId == id).Select(g => g.Id);
        await db.StatEvents.Where(e => gameIds.Contains(e.GameId)).ExecuteDeleteAsync();
        await db.QuarterScores.Where(q => gameIds.Contains(q.GameId)).ExecuteDeleteAsync();
        await db.Games.Where(g => g.SeasonId == id).ExecuteDeleteAsync();

        var teamIds = db.Teams.Where(t => t.SeasonId == id).Select(t => t.Id);
        await db.Players.Where(p => teamIds.Contains(p.TeamId)).ExecuteDeleteAsync();
        await db.Teams.Where(t => t.SeasonId == id).ExecuteDeleteAsync();

        await db.Seasons.Where(s => s.Id == id).ExecuteDeleteAsync();
    }
}
