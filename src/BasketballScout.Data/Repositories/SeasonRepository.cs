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

    public async Task DeleteAsync(int id)
    {
        var season = await _db.Seasons.FindAsync(id);
        if (season is not null)
        {
            _db.Seasons.Remove(season);
            await _db.SaveChangesAsync();
        }
    }
}
