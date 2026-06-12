using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class SeasonRepository : ISeasonRepository
{
    private readonly ScoutDbContext _db;

    public SeasonRepository(ScoutDbContext db)
    {
        _db = db;
    }

    public async Task<Season?> GetByIdAsync(int id)
        => await _db.Seasons
            .Include(s => s.Teams)
            .Include(s => s.Games)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IReadOnlyList<Season>> GetAllAsync()
        => await _db.Seasons
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();

    public async Task<Season> AddAsync(Season season)
    {
        _db.Seasons.Add(season);
        await _db.SaveChangesAsync();
        return season;
    }

    public async Task UpdateAsync(Season season)
    {
        _db.Seasons.Update(season);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Deep cascade delete (US-12). Children are removed explicitly in dependency
    /// order inside a transaction, because relying on SQLite's own cascades can trip
    /// the Restrict FKs (Game→Team, StatEvent→Player) depending on cascade order.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var gameIds = _db.Games.Where(g => g.SeasonId == id).Select(g => g.Id);
        await _db.StatEvents.Where(e => gameIds.Contains(e.GameId)).ExecuteDeleteAsync();
        await _db.QuarterScores.Where(q => gameIds.Contains(q.GameId)).ExecuteDeleteAsync();
        await _db.Games.Where(g => g.SeasonId == id).ExecuteDeleteAsync();

        var teamIds = _db.Teams.Where(t => t.SeasonId == id).Select(t => t.Id);
        await _db.Players.Where(p => teamIds.Contains(p.TeamId)).ExecuteDeleteAsync();
        await _db.Teams.Where(t => t.SeasonId == id).ExecuteDeleteAsync();

        await _db.Seasons.Where(s => s.Id == id).ExecuteDeleteAsync();

        await tx.CommitAsync();
    }
}
