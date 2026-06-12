using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class GameRepository : IGameRepository
{
    private readonly ScoutDbContext _db;

    public GameRepository(ScoutDbContext db)
    {
        _db = db;
    }

    public async Task<Game?> GetByIdAsync(int id)
        => await _db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.StatEvents)
            .Include(g => g.QuarterScores)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<IReadOnlyList<Game>> GetBySeasonIdAsync(int seasonId)
        => await _db.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Where(g => g.SeasonId == seasonId)
            .OrderByDescending(g => g.GameDate)
            .ToListAsync();

    public async Task<Game> AddAsync(Game game)
    {
        _db.Games.Add(game);
        await _db.SaveChangesAsync();
        return game;
    }

    public async Task UpdateAsync(Game game)
    {
        _db.Games.Update(game);
        await _db.SaveChangesAsync();
    }

    /// <summary>Deletes a game and its stat events / quarter scores (US-12). Explicit
    /// child cleanup in a transaction rather than relying on DB-level cascades.</summary>
    public async Task DeleteAsync(int id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        await _db.StatEvents.Where(e => e.GameId == id).ExecuteDeleteAsync();
        await _db.QuarterScores.Where(q => q.GameId == id).ExecuteDeleteAsync();
        await _db.Games.Where(g => g.Id == id).ExecuteDeleteAsync();

        await tx.CommitAsync();
    }

    public async Task UpdateGameStateAsync(int id, GameStatus status, int clockSecondsRemaining, int currentPeriod)
    {
        // Load the bare row (no Includes) so only the three scalar fields are marked
        // modified — avoids re-saving the whole StatEvent/QuarterScore graph.
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return;

        game.Status = status;
        game.ClockSecondsRemaining = clockSecondsRemaining;
        game.CurrentPeriod = currentPeriod;
        await _db.SaveChangesAsync();
    }
}
