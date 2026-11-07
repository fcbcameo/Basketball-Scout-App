using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;

namespace BasketballScout.Services;

/// <summary>
/// US-44: repairs one match that was scored as two games — e.g. the first game was finished by
/// mistake after Q2, and the rest was scored in a new game. Periods before the split period come
/// from the first game, the split period onwards from the second; the result is a single game
/// (the first one's id, date and location) and the second game is removed. Irreversible, so it
/// runs as one transaction.
/// </summary>
public class GameMergeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGameRepository _gameRepository;
    private readonly IStatEventRepository _statEventRepository;
    private readonly IPlayerRepository _playerRepository;

    public GameMergeService(
        IUnitOfWork unitOfWork,
        IGameRepository gameRepository,
        IStatEventRepository statEventRepository,
        IPlayerRepository playerRepository)
    {
        _unitOfWork = unitOfWork;
        _gameRepository = gameRepository;
        _statEventRepository = statEventRepository;
        _playerRepository = playerRepository;
    }

    /// <summary>The periods the second game can take over from (Q2 up to the last regulation
    /// period), the most likely one first: the first period in which the second game recorded
    /// anything besides its starting lineup (the scorer advanced to it right away).</summary>
    public async Task<IReadOnlyList<GameMergeSplitOption>> GetSplitOptionsAsync(int firstGameId, int secondGameId)
    {
        var first = await _gameRepository.GetByIdAsync(firstGameId);
        var second = await _gameRepository.GetByIdAsync(secondGameId);
        if (first is null || second is null) return [];

        var format = GameFormat.FromGame(first);
        int last = Math.Max(2, format.RegulationPeriods);
        var played = second.StatEvents.Where(e => !IsSub(e)).Select(e => e.Quarter).ToList();
        int suggested = Math.Clamp(played.Count > 0 ? played.Min() : second.CurrentPeriod, 2, last);

        return Enumerable.Range(2, last - 1)
            .Select(p => new GameMergeSplitOption(p,
                $"{FirstPartLabel(format, p)} from the first · {format.PeriodLabel(p)} on from the second"))
            .OrderBy(o => o.Period != suggested)
            .ToList();
    }

    private static string FirstPartLabel(GameFormat format, int splitPeriod) =>
        splitPeriod == 2 ? format.PeriodLabel(1) : $"{format.PeriodLabel(1)}–{format.PeriodLabel(splitPeriod - 1)}";

    /// <summary>What combining would produce, or why it can't be done — shown before confirming.</summary>
    public async Task<GameMergePreview> PreviewAsync(int firstGameId, int secondGameId, int splitPeriod)
    {
        var plan = await PlanAsync(firstGameId, secondGameId, splitPeriod);
        return plan.Preview;
    }

    /// <summary>Combines the two games into the first one and deletes the second. Call
    /// <see cref="PreviewAsync"/> first; throws if the games can't be combined.</summary>
    public async Task MergeAsync(int firstGameId, int secondGameId, int splitPeriod)
    {
        var plan = await PlanAsync(firstGameId, secondGameId, splitPeriod);
        if (plan.Preview.Problem is { } problem) throw new InvalidOperationException(problem);
        var first = plan.First!;
        var second = plan.Second!;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Dropped events must not leave dangling follow-up links behind.
            await _statEventRepository.UnlinkEventsLinkedToAsync(plan.DroppedIds);
            await _statEventRepository.DeleteByIdsAsync(plan.DroppedIds);

            // Hand the court over at the split: whoever was on at the end of the first game's last
            // period but didn't start the second game's part goes off, and vice versa. Players in
            // both lineups simply stay on, so their minutes run on unbroken.
            foreach (var e in plan.HandoverEvents)
                await _statEventRepository.AddAsync(e);

            await _statEventRepository.MoveToGameAsync(plan.MovedIds, first.Id);

            // The combined game carries on where the second one is: same status, period and clock,
            // so an unfinished second game stays resumable. Rosters: everyone present in either.
            await _gameRepository.UpdateGameStateAsync(first.Id, second.Status,
                second.ClockSecondsRemaining, second.CurrentPeriod);
            await _gameRepository.UpdateRosterIdsAsync(first.Id,
                UnionRoster(first.HomeRosterIds, second.HomeRosterIds),
                UnionRoster(first.AwayRosterIds, second.AwayRosterIds));

            // Removes the second game with what's left of it (its own pre-split events).
            await _gameRepository.DeleteAsync(second.Id);
        });
    }

    private async Task<MergePlan> PlanAsync(int firstGameId, int secondGameId, int splitPeriod)
    {
        var first = await _gameRepository.GetByIdAsync(firstGameId);
        var second = await _gameRepository.GetByIdAsync(secondGameId);
        if (first is null || second is null || firstGameId == secondGameId)
            return MergePlan.Refused("Pick two different matches.");
        if (first.HomeTeamId != second.HomeTeamId || first.AwayTeamId != second.AwayTeamId)
            return MergePlan.Refused("These matches aren't between the same home and away teams.");

        var format = GameFormat.FromGame(first);
        if (format != GameFormat.FromGame(second))
            return MergePlan.Refused("These matches use a different game format (period lengths).");
        if (splitPeriod < 2)
            return MergePlan.Refused("The second match must take over from Q2 or later.");

        var homeIds = (await _playerRepository.GetByTeamIdAsync(first.HomeTeamId)).Select(p => p.Id).ToHashSet();
        var awayIds = (await _playerRepository.GetByTeamIdAsync(first.AwayTeamId)).Select(p => p.Id).ToHashSet();

        var firstKept = first.StatEvents.Where(e => e.Quarter < splitPeriod).ToList();
        var firstDropped = first.StatEvents.Where(e => e.Quarter >= splitPeriod).ToList();
        var secondKept = second.StatEvents.Where(e => e.Quarter >= splitPeriod).ToList();
        var secondBefore = second.StatEvents.Where(e => e.Quarter < splitPeriod).ToList();

        // Lineups at the split, replayed from each game's own substitutions (its starting lineup
        // included). The second game's pre-split subs are replaced by the handover below.
        var onAtEndOfFirst = OnCourt(firstKept);
        var onAtStartOfSecond = OnCourt(secondBefore);

        var lastFirst = firstKept.Count > 0 ? firstKept.Max(e => e.Timestamp) : first.GameDate;
        var outAt = lastFirst.AddMilliseconds(1);
        var inAt = second.StatEvents.Count > 0 ? second.StatEvents.Min(e => e.Timestamp) : outAt;
        if (inAt <= outAt) inAt = outAt.AddMilliseconds(1);

        var handover = new List<StatEvent>();
        foreach (var playerId in onAtEndOfFirst.Except(onAtStartOfSecond))
            handover.Add(new StatEvent
            {
                GameId = first.Id, PlayerId = playerId, StatType = StatType.SubOut,
                Quarter = splitPeriod - 1, GameClock = "0:00", Timestamp = outAt
            });
        foreach (var playerId in onAtStartOfSecond.Except(onAtEndOfFirst))
            handover.Add(new StatEvent
            {
                GameId = first.Id, PlayerId = playerId, StatType = StatType.SubIn,
                Quarter = splitPeriod, GameClock = FormatClock(format.PeriodLengthSeconds(splitPeriod)),
                Timestamp = inAt
            });

        var preview = new GameMergePreview
        {
            SplitLabel = format.PeriodLabel(splitPeriod),
            FirstPartLabel = FirstPartLabel(format, splitPeriod),
            FirstHomeScore = Points(firstKept, homeIds),
            FirstAwayScore = Points(firstKept, awayIds),
            SecondHomeScore = Points(secondKept, homeIds),
            SecondAwayScore = Points(secondKept, awayIds),
            DroppedFromFirst = firstDropped.Count(e => !IsSub(e)),
            DroppedFromSecond = secondBefore.Count(e => !IsSub(e)),
            SecondIsFinished = second.Status == GameStatus.Finished
        };

        return new MergePlan(preview, first, second,
            firstDropped.Concat(secondBefore).Select(e => e.Id).ToList(),
            secondKept.Select(e => e.Id).ToList(),
            handover);
    }

    /// <summary>Who is on court after these events, netting SubIn/SubOut in recorded order.</summary>
    private static HashSet<int> OnCourt(IEnumerable<StatEvent> events)
    {
        var on = new HashSet<int>();
        foreach (var e in events.Where(IsSub).OrderBy(e => e.Timestamp).ThenBy(e => e.Id))
        {
            if (e.StatType == StatType.SubIn) on.Add(e.PlayerId);
            else on.Remove(e.PlayerId);
        }
        return on;
    }

    private static int Points(IEnumerable<StatEvent> events, HashSet<int> teamPlayerIds) =>
        events.Where(e => e.ShotResult == ShotResult.Made && teamPlayerIds.Contains(e.PlayerId))
            .Sum(e => e.StatType switch
            {
                StatType.Points2 => 2,
                StatType.Points3 => 3,
                StatType.FreeThrow => 1,
                _ => 0
            });

    private static bool IsSub(StatEvent e) => e.StatType is StatType.SubIn or StatType.SubOut;

    /// <summary>Union of two stored rosters; null (whole team, legacy) wins, so nobody disappears.</summary>
    private static string? UnionRoster(string? a, string? b)
    {
        var first = Game.ParseRosterIds(a);
        var second = Game.ParseRosterIds(b);
        if (first is null || second is null) return null;
        first.UnionWith(second);
        return Game.FormatRosterIds(first.Order());
    }

    private static string FormatClock(int seconds) => $"{seconds / 60}:{seconds % 60:D2}";

    private sealed record MergePlan(
        GameMergePreview Preview,
        Game? First,
        Game? Second,
        IReadOnlyCollection<int> DroppedIds,
        IReadOnlyCollection<int> MovedIds,
        IReadOnlyList<StatEvent> HandoverEvents)
    {
        public static MergePlan Refused(string problem) =>
            new(new GameMergePreview { Problem = problem }, null, null, [], [], []);
    }
}

/// <summary>A period the second game can take over from, with the label offered to the scorer.</summary>
public record GameMergeSplitOption(int Period, string Label);

public class GameMergePreview
{
    /// <summary>Why the games can't be combined; null when they can.</summary>
    public string? Problem { get; init; }

    /// <summary>The period the second game takes over from, e.g. "Q3".</summary>
    public string SplitLabel { get; init; } = string.Empty;

    /// <summary>The periods taken from the first game, e.g. "Q1–Q2".</summary>
    public string FirstPartLabel { get; init; } = string.Empty;

    public int FirstHomeScore { get; init; }
    public int FirstAwayScore { get; init; }
    public int SecondHomeScore { get; init; }
    public int SecondAwayScore { get; init; }
    public int HomeScore => FirstHomeScore + SecondHomeScore;
    public int AwayScore => FirstAwayScore + SecondAwayScore;

    /// <summary>Stats recorded in the first game from the split period on — they'd be removed.</summary>
    public int DroppedFromFirst { get; init; }

    /// <summary>Stats recorded in the second game before the split period — they'd be removed.</summary>
    public int DroppedFromSecond { get; init; }

    public bool SecondIsFinished { get; init; }
}
