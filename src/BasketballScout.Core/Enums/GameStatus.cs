namespace BasketballScout.Core.Enums;

/// <summary>
/// Lifecycle of a game. Persisted as an int on the Game row. Legacy games
/// created before this field existed are backfilled to <see cref="Finished"/>.
/// </summary>
public enum GameStatus
{
    /// <summary>Started (or being set up) and resumable — not yet ended.</summary>
    InProgress = 0,

    /// <summary>Explicitly ended via "Finish Game"; appears in the completed-matches overview.</summary>
    Finished = 1
}
