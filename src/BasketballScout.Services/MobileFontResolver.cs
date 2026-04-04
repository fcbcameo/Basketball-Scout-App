using PdfSharp.Fonts;

namespace BasketballScout.Services;

/// <summary>
/// Font resolver for PDFsharp on all platforms.
/// The base PDFsharp NuGet package has no built-in font resolver,
/// so we must provide one for Windows, Android, and iOS.
/// </summary>
public class PlatformFontResolver : IFontResolver
{
    private static readonly string[] FontSearchPaths = BuildSearchPaths();

    private static readonly Dictionary<string, string[]> FontFamilyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows fonts
        ["Arial"] = ["arial.ttf", "Roboto-Regular.ttf", "DroidSans.ttf", "NotoSans-Regular.ttf"],
        ["Arial Bold"] = ["arialbd.ttf", "Roboto-Bold.ttf", "DroidSans-Bold.ttf", "NotoSans-Bold.ttf"],
        ["Arial Italic"] = ["ariali.ttf", "Roboto-Italic.ttf"],
        ["Arial Bold Italic"] = ["arialbi.ttf", "Roboto-BoldItalic.ttf"],
    };

    private static string[] BuildSearchPaths()
    {
        var paths = new List<string>();

        // Windows
        var winFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        if (Directory.Exists(winFonts))
            paths.Add(winFonts);

        // User fonts on Windows
        var userFonts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");
        if (Directory.Exists(userFonts))
            paths.Add(userFonts);

        // Android
        if (Directory.Exists("/system/fonts"))
            paths.Add("/system/fonts");

        // iOS / macOS
        if (Directory.Exists("/System/Library/Fonts"))
            paths.Add("/System/Library/Fonts");

        return paths.ToArray();
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string key;
        if (isBold && isItalic)
            key = $"{familyName} Bold Italic";
        else if (isBold)
            key = $"{familyName} Bold";
        else if (isItalic)
            key = $"{familyName} Italic";
        else
            key = familyName;

        if (FontFamilyMap.ContainsKey(key))
            return new FontResolverInfo(key);

        // Fallback: try without style
        if (FontFamilyMap.ContainsKey(familyName))
            return new FontResolverInfo(familyName);

        // Default to Arial
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

        // Last resort: scan all search paths for any usable font
        foreach (var searchPath in FontSearchPaths)
        {
            if (!Directory.Exists(searchPath)) continue;

            var fallbacks = new[] { "arial.ttf", "Roboto-Regular.ttf", "DroidSans.ttf", "segoeui.ttf" };
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
