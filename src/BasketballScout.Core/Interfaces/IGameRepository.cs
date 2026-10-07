using BasketballScout.Core.Enums;
using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(int id);
    Task<IReadOnlyList<Game>> GetBySeasonIdAsync(int seasonId);
    Task<Game> AddAsync(Game game);
    Task UpdateAsync(Game game);
    Task DeleteAsync(int id);

    /// <summary>
    /// Persists just the live-game lifecycle fields (US-10) without touching the
    /// game's stat-event graph — used on pause, period change, and exit/finish.
    /// </summary>
    Task UpdateGameStateAsync(int id, GameStatus status, int clockSecondsRemaining, int currentPeriod);

    /// <summary>Persists just the stored match rosters (US-42), e.g. after a player is
    /// deleted or merged (US-43), without touching the rest of the game graph.</summary>
    Task UpdateRosterIdsAsync(int id, string? homeRosterIds, string? awayRosterIds);
}
