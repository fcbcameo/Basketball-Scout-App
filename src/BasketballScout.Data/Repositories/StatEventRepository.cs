using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class StatEventRepository : IStatEventRepository
{
    private readonly ScoutDbContextProvider _ctx;

    public StatEventRepository(ScoutDbContextProvider ctx)
    {
        _ctx = ctx;
    }

    // Tracked (no AsNoTracking): inside an import unit of work this is called on the
    // just-inserted event and then updated, so it must return the tracked instance.
    public async Task<StatEvent?> GetByIdAsync(int id)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.StatEvents
            .Include(e => e.Player)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IReadOnlyList<StatEvent>> GetByGameIdAsync(int gameId)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.StatEvents
            .Include(e => e.Player)
            .Where(e => e.GameId == gameId)
            .OrderBy(e => e.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StatEvent>> GetByPlayerIdAsync(int playerId)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.StatEvents
            .Where(e => e.PlayerId == playerId)
            .OrderBy(e => e.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<StatEvent> AddAsync(StatEvent statEvent)
    {
        await using var lease = _ctx.Lease();
        lease.Db.StatEvents.Add(statEvent);
        await lease.Db.SaveChangesAsync();
        return statEvent;
    }

    public async Task UpdateAsync(StatEvent statEvent)
    {
        await using var lease = _ctx.Lease();
        lease.Db.StatEvents.Update(statEvent);
        await lease.Db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var lease = _ctx.Lease();
        var statEvent = await lease.Db.StatEvents.FindAsync(id);
        if (statEvent is not null)
        {
            lease.Db.StatEvents.Remove(statEvent);
            await lease.Db.SaveChangesAsync();
        }
    }

    // US-43 bulk operations: set-based ExecuteUpdate/ExecuteDelete (no change tracking), which
    // run on the ambient unit-of-work context — and so inside its transaction — when one is active.

    public async Task UnlinkEventsLinkedToPlayerAsync(int playerId)
    {
        await using var lease = _ctx.Lease();
        var playerEventIds = lease.Db.StatEvents.Where(e => e.PlayerId == playerId).Select(e => e.Id);
        await lease.Db.StatEvents
            .Where(e => e.LinkedEventId != null && playerEventIds.Contains(e.LinkedEventId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LinkedEventId, (int?)null));
    }

    public async Task DeleteByPlayerIdAsync(int playerId)
    {
        await using var lease = _ctx.Lease();
        await lease.Db.StatEvents.Where(e => e.PlayerId == playerId).ExecuteDeleteAsync();
    }

    public async Task ReassignPlayerAsync(int fromPlayerId, int toPlayerId)
    {
        await using var lease = _ctx.Lease();
        await lease.Db.StatEvents
            .Where(e => e.PlayerId == fromPlayerId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.PlayerId, toPlayerId));
    }

    public async Task<StatEvent?> GetLastByGameIdAsync(int gameId)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.StatEvents
            .Include(e => e.Player)
            .Where(e => e.GameId == gameId)
            .OrderByDescending(e => e.Timestamp)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }
}
