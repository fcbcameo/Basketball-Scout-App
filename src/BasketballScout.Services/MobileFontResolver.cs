using PdfSharp.Fonts;

namespace BasketballScout.Services;

/// <summary>
/// Font resolver for PDFsharp on mobile platforms (Android/iOS) where
/// system fonts aren't accessible via GDI.
/// Reads font files from /system/fonts/ on Android or uses embedded fallback.
/// </summary>
public class MobileFontResolver : IFontResolver
{
    private static readonly string[] FontSearchPaths =
    [
        "/system/fonts",           // Android
        "/System/Library/Fonts",   // iOS
    ];

    private static readonly Dictionary<string, string[]> FontFamilyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = ["Roboto-Regular.ttf", "DroidSans.ttf", "NotoSans-Regular.ttf"],
        ["Arial Bold"] = ["Roboto-Bold.ttf", "DroidSans-Bold.ttf", "NotoSans-Bold.ttf"],
        ["Helvetica"] = ["Roboto-Regular.ttf", "DroidSans.ttf"],
        ["Helvetica Bold"] = ["Roboto-Bold.ttf", "DroidSans-Bold.ttf"],
    };

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var key = isBold ? $"{familyName} Bold" : familyName;

        // Try mapped font family
        if (FontFamilyMap.ContainsKey(key))
            return new FontResolverInfo(key);

        // Try base family
        if (FontFamilyMap.ContainsKey(familyName))
            return new FontResolverInfo(isBold ? $"{familyName} Bold" : familyName);

        // Fallback to Arial
        return new FontResolverInfo(isBold ? "Arial Bold" : "Arial");
    }

    public byte[]? GetFont(string faceName)
    {
        if (FontFamilyMap.TryGetValue(faceName, out var candidates))
        {
            foreach (var candidate in candidates)
            {
                foreach (var searchPath in FontSearchPaths)
                {
                    var path = Path.Combine(searchPath, candidate);
                    if (File.Exists(path))
                        return File.ReadAllBytes(path);
                }
            }
        }

        // Last resort: try to find any Roboto or DroidSans
        foreach (var searchPath in FontSearchPaths)
        {
            if (!Directory.Exists(searchPath)) continue;

            var fallbacks = new[] { "Roboto-Regular.ttf", "DroidSans.ttf", "NotoSans-Regular.ttf" };
            foreach (var fb in fallbacks)
            {
                var path = Path.Combine(searchPath, fb);
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
        }

        return null;
    }
}
