using BasketballScout.Core.Models;

namespace BasketballScout.Services;

/// <summary>
/// A game's period structure — the single source of truth for clock math (US-18/US-21).
/// Maps a period + "M:SS" remaining onto an absolute seconds-from-tip timeline used by
/// minutes, +/- and the game-flow chart. Lengths are configurable per game (US-21): the
/// defaults are FIBA (4 × 10:00 regulation, 5:00 overtime), but a season can set 8- or
/// 12-minute periods or a different number of periods, and each game snapshots the format
/// at creation so later season edits never rewrite history.
/// </summary>
public sealed record GameFormat(int RegulationPeriodSeconds, int OvertimePeriodSeconds, int RegulationPeriods)
{
    public static readonly GameFormat Default = new(600, 300, 4);

    /// <summary>Builds the format from a game's snapshot, falling back to defaults for any
    /// legacy/zero values (games created before US-21).</summary>
    public static GameFormat FromGame(Game game) => new(
        game.PeriodLengthSeconds > 0 ? game.PeriodLengthSeconds : 600,
        game.OvertimeLengthSeconds > 0 ? game.OvertimeLengthSeconds : 300,
        game.RegulationPeriods > 0 ? game.RegulationPeriods : 4);

    /// <summary>True once the period index is past regulation (i.e. an overtime period).</summary>
    public bool IsOvertime(int period) => period > RegulationPeriods;

    /// <summary>Length in seconds of a given 1-based period (regulation vs overtime).</summary>
    public int PeriodLengthSeconds(int period) =>
        IsOvertime(period) ? OvertimePeriodSeconds : RegulationPeriodSeconds;

    /// <summary>Total regulation length (all regulation periods).</summary>
    public int RegulationLengthSeconds => RegulationPeriods * RegulationPeriodSeconds;

    /// <summary>Absolute seconds from tip to the start of the given period.</summary>
    public int PeriodStartOffsetSeconds(int period)
    {
        int offset = 0;
        for (int p = 1; p < Math.Max(1, period); p++)
            offset += PeriodLengthSeconds(p);
        return offset;
    }

    /// <summary>Absolute seconds from tip to the end of the last period reached (minimum:
    /// end of regulation), so a chart's X-axis spans the whole game.</summary>
    public int TotalSecondsForMaxPeriod(int maxPeriod)
    {
        int p = Math.Max(RegulationPeriods, maxPeriod);
        return PeriodStartOffsetSeconds(p) + PeriodLengthSeconds(p);
    }

    /// <summary>Seconds remaining parsed from an "M:SS" clock, clamped to the period length.
    /// A blank/garbled clock is treated as full time remaining (period start).</summary>
    public int ParseClockSeconds(string clock, int periodLengthSeconds)
    {
        if (string.IsNullOrWhiteSpace(clock)) return periodLengthSeconds;
        var parts = clock.Split(':');
        if (parts.Length != 2) return periodLengthSeconds;
        if (!int.TryParse(parts[0], out var m)) return periodLengthSeconds;
        if (!int.TryParse(parts[1], out var s)) return periodLengthSeconds;
        return Math.Clamp(m * 60 + s, 0, periodLengthSeconds);
    }

    /// <summary>Maps a (period, clock) pair to absolute seconds from tip, using this format's
    /// period lengths so overtime and non-10:00 periods produce no phantom gaps.</summary>
    public int ToAbsoluteSeconds(int period, string clock)
    {
        int p = Math.Max(1, period);
        int len = PeriodLengthSeconds(p);
        int remaining = ParseClockSeconds(clock, len);
        int elapsed = Math.Clamp(len - remaining, 0, len);
        return PeriodStartOffsetSeconds(p) + elapsed;
    }

    /// <summary>"Q1".."Qn" for regulation, then "OT1", "OT2", … past it.</summary>
    public string PeriodLabel(int period) =>
        IsOvertime(period) ? $"OT{period - RegulationPeriods}" : $"Q{period}";
}
