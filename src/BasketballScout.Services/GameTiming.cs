namespace BasketballScout.Services;

/// <summary>
/// Single source of truth for mapping a game clock (period + "M:SS" remaining) onto an
/// absolute seconds-from-tip timeline used by minutes, +/- and the game-flow chart.
///
/// Period lengths are overtime-aware: regulation periods (1–4) are 10:00, overtime
/// periods (5+) are 5:00 — mirroring <c>GameScoringViewModel.PeriodLengthSeconds</c>.
/// The previous per-service copies assumed every period was 10:00, which inserted a
/// phantom 5:00 gap at each regulation→overtime boundary and inflated minutes/+/-.
///
/// US-21 will make these lengths configurable (per season/game); keeping all clock math
/// here means there is exactly one place to change when that lands.
/// </summary>
public static class GameTiming
{
    public const int RegulationPeriodSeconds = 600; // 10:00
    public const int OvertimePeriodSeconds = 300;   // 5:00

    /// <summary>Total regulation length — all four periods (2400s).</summary>
    public static int RegulationLengthSeconds => 4 * RegulationPeriodSeconds;

    /// <summary>Length in seconds of a given 1-based period (overtime = 5+).</summary>
    public static int PeriodLengthSeconds(int period) =>
        period >= 5 ? OvertimePeriodSeconds : RegulationPeriodSeconds;

    /// <summary>Absolute seconds from tip to the start of the given period.</summary>
    public static int PeriodStartOffsetSeconds(int period)
    {
        int offset = 0;
        for (int p = 1; p < Math.Max(1, period); p++)
            offset += PeriodLengthSeconds(p);
        return offset;
    }

    /// <summary>Absolute seconds from tip to the end of the last period reached by any
    /// event (minimum: end of regulation), so a chart's X-axis spans the whole game.</summary>
    public static int TotalSecondsForMaxPeriod(int maxPeriod)
    {
        int p = Math.Max(4, maxPeriod);
        return PeriodStartOffsetSeconds(p) + PeriodLengthSeconds(p);
    }

    /// <summary>Seconds remaining parsed from an "M:SS" clock, clamped to the period
    /// length. A blank/garbled clock is treated as full time remaining (period start).</summary>
    public static int ParseClockSeconds(string clock, int periodLengthSeconds)
    {
        if (string.IsNullOrWhiteSpace(clock)) return periodLengthSeconds;
        var parts = clock.Split(':');
        if (parts.Length != 2) return periodLengthSeconds;
        if (!int.TryParse(parts[0], out var m)) return periodLengthSeconds;
        if (!int.TryParse(parts[1], out var s)) return periodLengthSeconds;
        return Math.Clamp(m * 60 + s, 0, periodLengthSeconds);
    }

    /// <summary>Maps a (period, clock) pair to absolute seconds from tip. Overtime periods
    /// are offset by 5:00 rather than 10:00, so no phantom gap appears at the OT boundary.</summary>
    public static int ToAbsoluteSeconds(int period, string clock)
    {
        int p = Math.Max(1, period);
        int len = PeriodLengthSeconds(p);
        int remaining = ParseClockSeconds(clock, len);
        int elapsed = Math.Clamp(len - remaining, 0, len);
        return PeriodStartOffsetSeconds(p) + elapsed;
    }
}
