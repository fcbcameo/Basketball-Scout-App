using PdfSharpCore.Fonts;

namespace BasketballScout.Services;

/// <summary>
/// PdfSharpCore font resolver backed by fonts embedded in this assembly.
///
/// Without a custom resolver, PdfSharpCore falls back to its built-in resolver,
/// which scans the OS font directory. That works on Windows/Android but throws
/// a TypeInitializationException on iOS (no system font directory), breaking PDF
/// generation. This resolver maps every requested family to the embedded
/// OpenSans, so PDF output is identical and deterministic on all platforms.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    private const string RegularFace = "OpenSans#Regular";
    private const string BoldFace = "OpenSans#Bold";

    private static readonly byte[] RegularData = LoadFont("OpenSans-Regular.ttf");
    private static readonly byte[] BoldData = LoadFont("OpenSans-Semibold.ttf");

    public string DefaultFontName => "OpenSans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(isBold ? BoldFace : RegularFace);

    public byte[] GetFont(string faceName)
        => faceName == BoldFace ? BoldData : RegularData;

    private static byte[] LoadFont(string fileName)
    {
        var asm = typeof(EmbeddedFontResolver).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Embedded font '{fileName}' not found. Available: "
                + string.Join(", ", asm.GetManifestResourceNames()));

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
