using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface IStatEventRepository
{
    Task<StatEvent?> GetByIdAsync(int id);
    Task<IReadOnlyList<StatEvent>> GetByGameIdAsync(int gameId);
    Task<IReadOnlyList<StatEvent>> GetByPlayerIdAsync(int playerId);
    Task<StatEvent> AddAsync(StatEvent statEvent);
    Task UpdateAsync(StatEvent statEvent);
    Task DeleteAsync(int id);
    Task<StatEvent?> GetLastByGameIdAsync(int gameId);

    // ── Permanent player removal (US-43) ──

    /// <summary>Clears <c>LinkedEventId</c> on every event that points at one of this player's
    /// events, so deleting them leaves no dangling links (the linking events stay).</summary>
    Task UnlinkEventsLinkedToPlayerAsync(int playerId);

    /// <summary>Deletes every event recorded for this player.</summary>
    Task DeleteByPlayerIdAsync(int playerId);

    /// <summary>Moves every event of one player to another (merging a duplicate).</summary>
    Task ReassignPlayerAsync(int fromPlayerId, int toPlayerId);
}
