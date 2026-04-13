using System.IO;
using Raylib_cs;

namespace TavRay;

/// <summary>
/// Loads Roboto Mono for terminal-style rendering. Same pattern as Starflight’s <c>UiText</c> + Open Sans.
/// License: <c>Fonts/RobotoMono-OFL.txt</c>; summary: <c>Fonts/ATTRIBUTION.txt</c>.
/// </summary>
public static class RayMonoFont
{
    private static Font _font;
    private static bool _customLoaded;

    private const int AtlasFontSize = 128;

    public static Font Font => _font;

    public static void Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fonts", "RobotoMono.ttf");
        if (File.Exists(path))
        {
            _font = Raylib.LoadFontEx(path, AtlasFontSize, null, 0);
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
