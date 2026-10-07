using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;

namespace BasketballScout.Services;

/// <summary>
/// US-43: permanently removing a player who has game history — either deleting them with all
/// their stats, or merging them into another player of the same team (typically to clean up an
/// accidental duplicate). Both are irreversible and run as one transaction each.
/// </summary>
public class PlayerCleanupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStatEventRepository _statEventRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IGameRepository _gameRepository;

    public PlayerCleanupService(
        IUnitOfWork unitOfWork,
        IStatEventRepository statEventRepository,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository,
        IGameRepository gameRepository)
    {
        _unitOfWork = unitOfWork;
        _statEventRepository = statEventRepository;
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
        _gameRepository = gameRepository;
    }

    /// <summary>How much the player has recorded. Shown in the confirmation so the scorer can tell
    /// a duplicate from the original — they usually share name and jersey.</summary>
    public async Task<(int Events, int Games)> GetHistoryAsync(int playerId)
    {
        var events = await _statEventRepository.GetByPlayerIdAsync(playerId);
        return (events.Count, events.Select(e => e.GameId).Distinct().Count());
    }

    /// <summary>Deletes the player and every event they recorded. Other players' follow-ups that
    /// pointed at those events (an assist on their basket, a rebound off their miss) stay credited;
    /// only the link is cleared.</summary>
    public Task DeletePermanentlyAsync(int playerId) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await RewriteMatchRostersAsync(playerId, replacementId: null);
            await _statEventRepository.UnlinkEventsLinkedToPlayerAsync(playerId);
            await _statEventRepository.DeleteByPlayerIdAsync(playerId);
            await _playerRepository.DeleteAsync(playerId);
        });

    /// <summary>The first game in which both players were on court at the same moment, or null.
    /// One person can't be on court twice, so such a merge must be refused.</summary>
    public async Task<Game?> FindCourtOverlapAsync(int playerA, int playerB)
    {
        var gamesA = (await _statEventRepository.GetByPlayerIdAsync(playerA)).Select(e => e.GameId);
        var gamesB = (await _statEventRepository.GetByPlayerIdAsync(playerB)).Select(e => e.GameId);

        foreach (var gameId in gamesA.Intersect(gamesB))
        {
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (game is null) continue;

            var events = await _statEventRepository.GetByGameIdAsync(gameId);
            if (GameStatsService.WereOnCourtTogether(events, GameFormat.FromGame(game), playerA, playerB))
                return game;
        }

        return null;
    }

    /// <summary>Moves every event of <paramref name="fromPlayerId"/> to <paramref name="intoPlayerId"/>
    /// and removes the former, so no stats are lost. Call <see cref="FindCourtOverlapAsync"/> first.</summary>
    public Task MergeAsync(int fromPlayerId, int intoPlayerId) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await RewriteMatchRostersAsync(fromPlayerId, intoPlayerId);
            await _statEventRepository.ReassignPlayerAsync(fromPlayerId, intoPlayerId);
            await _playerRepository.DeleteAsync(fromPlayerId);
        });

    /// <summary>Keeps the stored match rosters (US-42) consistent: the removed player's id is
    /// dropped from every game of their season, or replaced by the merge target.</summary>
    private async Task RewriteMatchRostersAsync(int playerId, int? replacementId)
    {
        var player = await _playerRepository.GetByIdAsync(playerId);
        if (player is null) return;
        var team = await _teamRepository.GetByIdAsync(player.TeamId);
        if (team is null) return;

        foreach (var game in await _gameRepository.GetBySeasonIdAsync(team.SeasonId))
        {
            var home = ReplaceInRoster(game.HomeRosterIds, playerId, replacementId);
            var away = ReplaceInRoster(game.AwayRosterIds, playerId, replacementId);
            if (home != game.HomeRosterIds || away != game.AwayRosterIds)
                await _gameRepository.UpdateRosterIdsAsync(game.Id, home, away);
        }
    }

    private static string? ReplaceInRoster(string? stored, int playerId, int? replacementId)
    {
        var ids = Game.ParseRosterIds(stored);
        if (ids is null || !ids.Remove(playerId)) return stored;
        if (replacementId is int id) ids.Add(id);
        return Game.FormatRosterIds(ids);
    }
}
