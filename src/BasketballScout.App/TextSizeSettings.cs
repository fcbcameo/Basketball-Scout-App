namespace BasketballScout.App;

/// <summary>
/// US-30: a simple Normal/Large text-size setting for courtside readability. The choice is
/// stored in Preferences and pushed into the app resource dictionary as font sizes, so any
/// label bound with <c>{DynamicResource Fs…}</c> resizes live. Localization is out of scope;
/// this only affects size. OS dynamic-type scaling still applies on top (labels keep MAUI's
/// default FontAutoScalingEnabled), so this setting stacks with the system font-size setting.
/// </summary>
public static class TextSizeSettings
{
    private const string Key = "LargeText";
    private const double LargeScale = 1.3;

    // Base (Normal) sizes for the scoring screen's smaller text, keyed by resource name.
    private static readonly (string Key, double Base)[] Fonts =
    [
        ("FsRosterNum", 20),
        ("FsRosterName", 10),
        ("FsPlayLog", 12),
    ];

    public static bool IsLarge
    {
        get => Preferences.Default.Get(Key, false);
        set
        {
            Preferences.Default.Set(Key, value);
            Apply();
        }
    }

    /// <summary>Writes the current sizes into <see cref="Application.Current"/>'s resources.
    /// Safe to call at startup and whenever the setting changes.</summary>
    public static void Apply()
    {
        var res = Application.Current?.Resources;
        if (res is null) return;

        double scale = IsLarge ? LargeScale : 1.0;
        foreach (var (key, baseSize) in Fonts)
            res[key] = Math.Round(baseSize * scale);
    }
}
