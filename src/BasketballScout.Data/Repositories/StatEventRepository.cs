using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class StatEventRepository : IStatEventRepository
{
    private readonly ScoutDbContext _db;

    public StatEventRepository(ScoutDbContext db)
    {
        _db = db;
    }

    public async Task<StatEvent?> GetByIdAsync(int id)
        => await _db.StatEvents
            .Include(e => e.Player)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IReadOnlyList<StatEvent>> GetByGameIdAsync(int gameId)
        => await _db.StatEvents
            .Include(e => e.Player)
            .Where(e => e.GameId == gameId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

    public async Task<IReadOnlyList<StatEvent>> GetByPlayerIdAsync(int playerId)
        => await _db.StatEvents
            .Where(e => e.PlayerId == playerId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

    public async Task<StatEvent> AddAsync(StatEvent statEvent)
    {
        _db.StatEvents.Add(statEvent);
        await _db.SaveChangesAsync();
        return statEvent;
    }

    public async Task UpdateAsync(StatEvent statEvent)
    {
        _db.StatEvents.Update(statEvent);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var statEvent = await _db.StatEvents.FindAsync(id);
        if (statEvent is not null)
        {
            _db.StatEvents.Remove(statEvent);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<StatEvent?> GetLastByGameIdAsync(int gameId)
        => await _db.StatEvents
            .Include(e => e.Player)
            .Where(e => e.GameId == gameId)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync();
}
