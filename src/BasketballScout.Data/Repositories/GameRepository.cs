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

    public async Task DeleteAsync(int id)
    {
        var game = await _db.Games.FindAsync(id);
        if (game is not null)
        {
            _db.Games.Remove(game);
            await _db.SaveChangesAsync();
        }
    }
}
