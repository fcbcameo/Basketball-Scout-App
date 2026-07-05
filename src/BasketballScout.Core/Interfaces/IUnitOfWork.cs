namespace BasketballScout.Core.Interfaces;

/// <summary>
/// Runs a unit of work atomically. Used by multi-step operations (e.g. importing a game
/// or a season) so a mid-way failure rolls the whole thing back instead of leaving
/// half-created teams/players/games behind (US-19).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Executes <paramref name="work"/> inside a database transaction: commits on
    /// success, rolls back if it throws. The repositories used inside <paramref name="work"/>
    /// must share the same database context for their writes to participate.</summary>
    Task ExecuteInTransactionAsync(Func<Task> work);
}
