using System.IO;
using Raylib_cs;

namespace TavRay;

/// <summary>
/// Loads Anonymous Pro for terminal-style rendering.
/// License: <c>Fonts/AnonymousPro-OFL.txt</c>; summary: <c>Fonts/ATTRIBUTION.txt</c>.
/// </summary>
/// <remarks>
/// Raylib’s <c>LoadFontEx(..., null, 0)</c> only builds an atlas for codepoints 32–126. TavLib uses
/// Unicode box drawing (e.g. ┌─│), Latin-1 punctuation (·), and general punctuation (— …), so we pass an explicit set.
/// </remarks>
public static class RayMonoFont
{
    private static Font _font;
    private static bool _customLoaded;

    private const int AtlasFontSize = 128;

    /// <summary>Glyphs needed for the in-game terminal surface (ASCII, Latin-1, box drawing, common dashes/ellipsis).</summary>
    private static readonly int[] TerminalCodepoints = BuildTerminalCodepoints();

    public static Font Font => _font;

    private static int[] BuildTerminalCodepoints()
    {
        const int asciiFirst = 32;
        const int asciiLast = 126;
        const int latin1First = 0xA0;
        const int latin1Last = 0xFF;
        const int boxFirst = 0x2500;
        const int boxLast = 0x257F;
        int count = (asciiLast - asciiFirst + 1)
            + (latin1Last - latin1First + 1)
            + (boxLast - boxFirst + 1)
            + 3;

        var codepoints = new int[count];
        int i = 0;
        for (int cp = asciiFirst; cp <= asciiLast; cp++)
            codepoints[i++] = cp;

        for (int cp = latin1First; cp <= latin1Last; cp++)
            codepoints[i++] = cp;

        for (int cp = boxFirst; cp <= boxLast; cp++)
            codepoints[i++] = cp;

        codepoints[i++] = 0x2013;
        codepoints[i++] = 0x2014;
        codepoints[i++] = 0x2026;
        return codepoints;
    }

    public static void Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fonts", "AnonymousPro.ttf");
        if (File.Exists(path))
        {
            _font = Raylib.LoadFontEx(path, AtlasFontSize, TerminalCodepoints, TerminalCodepoints.Length);
            Raylib.SetTextureFilter(_font.Texture, TextureFilter.TEXTURE_FILTER_BILINEAR);
            _customLoaded = true;
        }
        else
        {
            _font = Raylib.GetFontDefault();
            _customLoaded = false;
        }
    }

    public static void Unload()
    {
        if (!_customLoaded)
            return;

        Raylib.UnloadFont(_font);
        _customLoaded = false;
        _font = default;
    }
}
