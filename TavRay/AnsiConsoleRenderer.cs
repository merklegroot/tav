using System.Globalization;
using System.Numerics;
using Raylib_cs;

namespace TavRay;

/// <summary>Parses common ANSI SGR sequences and draws text with Raylib's default font.</summary>
internal static class AnsiConsoleRenderer
{
    private static readonly Color DefaultFg = new((byte)220, (byte)220, (byte)220, (byte)255);

    /// <summary>Draws a multi-line snapshot with margins; clips to the given area.</summary>
    public static void DrawScreen(string surface, Font font, Rectangle contentArea)
    {
        if (string.IsNullOrEmpty(surface))
            return;

        string[] lines = surface.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        float spacing = 1f;
        float fontSize = ComputeFontSize(font, lines, contentArea, spacing);
        float lineHeight = Raylib.MeasureTextEx(font, "Mg", fontSize, spacing).Y;
        if (lineHeight < 1f)
            lineHeight = fontSize * 1.15f;

        int maxLines = Math.Max(1, (int)Math.Floor(contentArea.Height / lineHeight));
        int startLine = Math.Max(0, lines.Length - maxLines);
        float y = contentArea.Y;
        for (int li = startLine; li < lines.Length; li++)
        {
            if (y + lineHeight > contentArea.Y + contentArea.Height)
                break;

            DrawLine(font, lines[li], new Vector2(contentArea.X, y), fontSize, spacing, contentArea);
            y += lineHeight;
        }
    }

    private static float ComputeFontSize(Font font, string[] lines, Rectangle area, float spacing)
    {
        for (float size = 22f; size >= 9f; size -= 0.5f)
        {
            float lineH = Raylib.MeasureTextEx(font, "Mg", size, spacing).Y;
            if (lineH < 1f)
                lineH = size * 1.15f;

            int maxLines = Math.Max(1, (int)Math.Floor(area.Height / lineH));
            int start = Math.Max(0, lines.Length - maxLines);
            float maxRowW = 0f;
            for (int li = start; li < lines.Length; li++)
            {
                float w = MeasureLineWidth(font, lines[li], size, spacing);
                if (w > maxRowW)
                    maxRowW = w;
            }

            int shown = lines.Length - start;
            if (maxRowW <= area.Width && lineH * shown <= area.Height + 0.5f)
                return size;
        }

        return 9f;
    }

    private static float MeasureLineWidth(Font font, string line, float fontSize, float spacing)
    {
        float x = 0f;
        int i = 0;
        while (i < line.Length)
        {
            if (TryConsumeCsi(line, ref i, out char final, out ReadOnlySpan<char> paramSpan))
            {
                if (final != 'm')
                    continue;

                continue;
            }

            int ch = char.ConvertToUtf32(line, i);
            int adv = char.IsSurrogatePair(line, i) ? 2 : 1;
            string glyph = line.Substring(i, adv);
            x += Raylib.MeasureTextEx(font, glyph, fontSize, spacing).X;
            i += adv;
        }

        return x;
    }

    private static void DrawLine(
        Font font,
        string line,
        Vector2 origin,
        float fontSize,
        float spacing,
        Rectangle clip)
    {
        float x = origin.X;
        float y = origin.Y;
        Color fg = DefaultFg;
        bool dim = false;
        int i = 0;
        while (i < line.Length)
        {
            if (TryConsumeCsi(line, ref i, out char final, out ReadOnlySpan<char> paramSpan))
            {
                if (final == 'm')
                    ApplySgr(paramSpan, ref fg, ref dim);

                continue;
            }

            int ch = char.ConvertToUtf32(line, i);
            int adv = char.IsSurrogatePair(line, i) ? 2 : 1;
            string glyph = line.Substring(i, adv);
            Color drawColor = dim ? DimColor(fg) : fg;
            if (x <= clip.X + clip.Width && x + Raylib.MeasureTextEx(font, glyph, fontSize, spacing).X >= clip.X)
                Raylib.DrawTextEx(font, glyph, new Vector2(x, y), fontSize, spacing, drawColor);

            x += Raylib.MeasureTextEx(font, glyph, fontSize, spacing).X;
            i += adv;
        }
    }

    private static Color DimColor(Color c) =>
        new(
            (byte)(c.R * 0.55f),
            (byte)(c.G * 0.55f),
            (byte)(c.B * 0.55f),
            c.A);

    private static bool TryConsumeCsi(string s, ref int i, out char final, out ReadOnlySpan<char> paramSpan)
    {
        final = '\0';
        paramSpan = ReadOnlySpan<char>.Empty;
        if (i >= s.Length || s[i] != '\x1b' || i + 1 >= s.Length || s[i + 1] != '[')
            return false;

        int j = i + 2;
        while (j < s.Length)
        {
            char c = s[j];
            if (c >= 0x40 && c <= 0x7E)
            {
                final = c;
                paramSpan = s.AsSpan(i + 2, j - (i + 2));
                i = j + 1;
                return true;
            }

            j++;
        }

        return false;
    }

    private static void ApplySgr(ReadOnlySpan<char> paramSpan, ref Color fg, ref bool dim)
    {
        if (paramSpan.Length == 0 || paramSpan.SequenceEqual("0".AsSpan()))
        {
            fg = DefaultFg;
            dim = false;
            return;
        }

        var parts = paramSpan.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int p = 0; p < parts.Length; p++)
        {
            if (!int.TryParse(parts[p], NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                continue;

            if (code == 0)
            {
                fg = DefaultFg;
                dim = false;
            }
            else if (code == 1)
            {
                dim = false;
            }
            else if (code == 2)
            {
                dim = true;
            }
            else if (code == 22)
            {
                dim = false;
            }
            else if (code is 39 or 49)
            {
                fg = DefaultFg;
            }
            else if (code >= 30 && code <= 37)
            {
                fg = Base8(code - 30, bright: false);
            }
            else if (code >= 90 && code <= 97)
            {
                fg = Base8(code - 90, bright: true);
            }
            else if (code == 38 && p + 2 < parts.Length &&
                     int.TryParse(parts[p + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode) &&
                     mode == 5 &&
                     int.TryParse(parts[p + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx256))
            {
                fg = ColorFrom256(idx256);
                p += 2;
            }
        }
    }

    private static Color Base8(int i, bool bright)
    {
        ReadOnlySpan<Color> normal =
        [
            new((byte)0, (byte)0, (byte)0, (byte)255),
            new((byte)205, (byte)49, (byte)49, (byte)255),
            new((byte)13, (byte)188, (byte)121, (byte)255),
            new((byte)229, (byte)229, (byte)16, (byte)255),
            new((byte)36, (byte)114, (byte)200, (byte)255),
            new((byte)188, (byte)63, (byte)188, (byte)255),
            new((byte)17, (byte)168, (byte)205, (byte)255),
            new((byte)229, (byte)229, (byte)229, (byte)255),
        ];
        ReadOnlySpan<Color> brightColors =
        [
            new((byte)102, (byte)102, (byte)102, (byte)255),
            new((byte)241, (byte)76, (byte)76, (byte)255),
            new((byte)35, (byte)209, (byte)139, (byte)255),
            new((byte)245, (byte)245, (byte)67, (byte)255),
            new((byte)59, (byte)142, (byte)234, (byte)255),
            new((byte)214, (byte)112, (byte)214, (byte)255),
            new((byte)41, (byte)184, (byte)219, (byte)255),
            new((byte)255, (byte)255, (byte)255, (byte)255),
        ];

        i = Math.Clamp(i, 0, 7);
        return bright ? brightColors[i] : normal[i];
    }

    private static Color ColorFrom256(int idx)
    {
        idx = Math.Clamp(idx, 0, 255);
        if (idx < 16)
            return idx < 8 ? Base8(idx, false) : Base8(idx - 8, true);

        if (idx >= 232)
        {
            int g = idx - 232;
            byte v = (byte)(g * 10 + 8);
            return new Color(v, v, v, (byte)255);
        }

        idx -= 16;
        int r = idx / 36;
        int rem = idx % 36;
        int g2 = rem / 6;
        int b = rem % 6;
        return new Color(
            (byte)(r == 0 ? 0 : 55 + r * 40),
            (byte)(g2 == 0 ? 0 : 55 + g2 * 40),
            (byte)(b == 0 ? 0 : 55 + b * 40),
            (byte)255);
    }
}
