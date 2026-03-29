using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly ScoutDbContext _db;

    public PlayerRepository(ScoutDbContext db)
    {
        _db = db;
    }

    public async Task<Player?> GetByIdAsync(int id)
        => await _db.Players.FindAsync(id);

    public async Task<IReadOnlyList<Player>> GetByTeamIdAsync(int teamId)
        => await _db.Players
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.JerseyNumber)
            .ToListAsync();

    public async Task<Player> AddAsync(Player player)
    {
        _db.Players.Add(player);
        await _db.SaveChangesAsync();
        return player;
    }

    public async Task UpdateAsync(Player player)
    {
        _db.Players.Update(player);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var player = await _db.Players.FindAsync(id);
        if (player is not null)
        {
            _db.Players.Remove(player);
            await _db.SaveChangesAsync();
        }
    }
}
