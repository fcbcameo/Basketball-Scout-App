namespace BasketballScout.Services;

/// <summary>Standard shot-chart zones. Coordinates are normalized 0–1 with the basket at the
/// bottom-centre (x≈0.5, y≈0.9), matching how the scoring screen records CourtX/CourtY.</summary>
public enum CourtZone
{
    Paint,
    MidLeft,
    MidCentre,
    MidRight,
    Corner3Left,
    Corner3Right,
    Wing3Left,
    Wing3Right,
    Top3
}

/// <summary>
/// Single source of truth for classifying a court position (US-23). The 3-point test is the
/// same arc math the scoring screen uses for its live 2PT/3PT suggestion, so the live hint and
/// the zone analytics can never disagree.
/// </summary>
public static class CourtZones
{
    /// <summary>The eight/nine zones in a stable display order (3PT first, then mid, then paint).</summary>
    public static readonly CourtZone[] All =
    [
        CourtZone.Corner3Left, CourtZone.Wing3Left, CourtZone.Top3, CourtZone.Wing3Right, CourtZone.Corner3Right,
        CourtZone.MidLeft, CourtZone.MidCentre, CourtZone.MidRight,
        CourtZone.Paint
    ];

    /// <summary>True if a shot at the given court percentages (0–100) is a three-pointer.
    /// Mirrors <c>GameScoringPage.OnCourtTapped</c> exactly.</summary>
    public static bool IsThreePointer(double xPct, double yPct)
    {
        double dx = xPct - 50;
        double dy = yPct - 90;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        return dist > 42 || ((xPct < 12 || xPct > 88) && yPct > 58);
    }

    /// <summary>Classifies a normalized (0–1) court position into a zone.</summary>
    public static CourtZone GetZone(float x, float y)
    {
        double xp = x * 100.0;
        double yp = y * 100.0;

        if (IsThreePointer(xp, yp))
        {
            // Corners: hugging the baseline (near the basket end) at the far sides.
            if (yp > 72 && xp < 20) return CourtZone.Corner3Left;
            if (yp > 72 && xp > 80) return CourtZone.Corner3Right;
            // Top of the key: central and further out.
            if (xp >= 38 && xp <= 62) return CourtZone.Top3;
            // Otherwise a wing three.
            return xp < 50 ? CourtZone.Wing3Left : CourtZone.Wing3Right;
        }

        // Two-pointers. Paint = central column near the basket.
        if (xp >= 33 && xp <= 67 && yp >= 60) return CourtZone.Paint;
        if (xp < 40) return CourtZone.MidLeft;
        if (xp > 60) return CourtZone.MidRight;
        return CourtZone.MidCentre;
    }

    public static bool IsThree(CourtZone zone) => zone is
        CourtZone.Corner3Left or CourtZone.Corner3Right or
        CourtZone.Wing3Left or CourtZone.Wing3Right or CourtZone.Top3;

    public static string Label(CourtZone zone) => zone switch
    {
        CourtZone.Paint => "Paint",
        CourtZone.MidLeft => "Mid L",
        CourtZone.MidCentre => "Mid C",
        CourtZone.MidRight => "Mid R",
        CourtZone.Corner3Left => "Corner 3 L",
        CourtZone.Corner3Right => "Corner 3 R",
        CourtZone.Wing3Left => "Wing 3 L",
        CourtZone.Wing3Right => "Wing 3 R",
        CourtZone.Top3 => "Top 3",
        _ => zone.ToString()
    };
}
