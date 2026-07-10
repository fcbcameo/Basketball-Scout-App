using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class GameRepository : IGameRepository
{
    private readonly ScoutDbContextProvider _ctx;

    public GameRepository(ScoutDbContextProvider ctx)
    {
        _ctx = ctx;
    }

    public async Task<Game?> GetByIdAsync(int id)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.StatEvents)
            .Include(g => g.QuarterScores)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<IReadOnlyList<Game>> GetBySeasonIdAsync(int seasonId)
    {
        await using var lease = _ctx.Lease();
        return await lease.Db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Where(g => g.SeasonId == seasonId)
            .OrderByDescending(g => g.GameDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Game> AddAsync(Game game)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Games.Add(game);
        await lease.Db.SaveChangesAsync();
        return game;
    }

    public async Task UpdateAsync(Game game)
    {
        await using var lease = _ctx.Lease();
        lease.Db.Games.Update(game);
        await lease.Db.SaveChangesAsync();
    }

    /// <summary>Deletes a game and its stat events / quarter scores (US-12). Runs in a
    /// transaction: its own when leased standalone, or the ambient one inside a unit of work.</summary>
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
        await db.StatEvents.Where(e => e.GameId == id).ExecuteDeleteAsync();
        await db.QuarterScores.Where(q => q.GameId == id).ExecuteDeleteAsync();
        await db.Games.Where(g => g.Id == id).ExecuteDeleteAsync();
    }

    public async Task UpdateGameStateAsync(int id, GameStatus status, int clockSecondsRemaining, int currentPeriod)
    {
        // Load the bare row (no Includes) so only the three scalar fields are marked
        // modified — avoids re-saving the whole StatEvent/QuarterScore graph.
        await using var lease = _ctx.Lease();
        var game = await lease.Db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return;

        game.Status = status;
        game.ClockSecondsRemaining = clockSecondsRemaining;
        game.CurrentPeriod = currentPeriod;
        await lease.Db.SaveChangesAsync();
    }
}
