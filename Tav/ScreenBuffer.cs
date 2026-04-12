using System.Text;

namespace Tav;

/// <summary>
/// Character grid with per-cell SGR state for predictable terminal layout. Coordinates are 0-based;
/// <see cref="DrawText"/> clips to the buffer. Render groups ANSI runs per row for fewer writes.
/// </summary>
public sealed class ScreenBuffer
{
    private readonly struct Cell
    {
        public char Ch { get; init; }
        /// <summary>Escape sequence(s) to emit before this glyph after a reset (empty = default color).</summary>
        public string OpenAnsi { get; init; }
    }

    private readonly Cell[,] _cells;
    private readonly int[] _rowMaxDrawnX;
    private readonly int _width;
    private readonly int _height;
    private readonly ITerminal _terminal;
    private readonly IConsoleWrapper _console;

    public int Width => _width;
    public int Height => _height;

    private ScreenBuffer(int width, int height, ITerminal terminal, IConsoleWrapper console)
    {
        _terminal = terminal;
        _console = console;
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _cells = new Cell[_height, _width];
        _rowMaxDrawnX = new int[_height];
        Clear();
    }

    /// <summary>
    /// Game frames use a fixed width (<see cref="AdventureLayout.ScreenWidth"/>) and one row per legacy <c>WriteLine</c>.
    /// </summary>
    public static ScreenBuffer ForGameLayout(
        int rowCount,
        ITerminal terminal,
        IConsoleWrapper console,
        int? widthOverride = null)
    {
        int w = widthOverride ?? AdventureLayout.ScreenWidth;
        return new ScreenBuffer(w, Math.Max(1, rowCount), terminal, console);
    }

    public void Clear()
    {
        for (int y = 0; y < _height; y++)
        {
            _rowMaxDrawnX[y] = -1;
            for (int x = 0; x < _width; x++)
                _cells[y, x] = new Cell { Ch = ' ', OpenAnsi = "" };
        }
    }

    /// <summary>Draws text with optional leading SGR open code prepended to the parse stream (same rules as <see cref="ITerminal.VisibleLength"/>).</summary>
    public void DrawText(int x, int y, string text, string ansiStyle = "")
    {
        if (y < 0 || y >= _height || string.IsNullOrEmpty(text) && string.IsNullOrEmpty(ansiStyle))
            return;

        string merged = string.IsNullOrEmpty(ansiStyle) ? text : ansiStyle + text;
        if (merged.Length == 0)
            return;

        int col = 0;
        string persistentOpen = "";
        int i = 0;
        while (i < merged.Length)
        {
            if (merged[i] == '\x1b' && i + 1 < merged.Length && merged[i + 1] == '[')
            {
                int start = i;
                i += 2;
                while (i < merged.Length && merged[i] != 'm')
                    i++;
                if (i >= merged.Length)
                    break;
                string seq = merged.Substring(start, i - start + 1);
                if (seq == _terminal.Reset)
                    persistentOpen = "";
                else
                    persistentOpen += seq;
                i++;
                continue;
            }

            if (col >= _width)
                return;

            int gx = x + col;
            if (gx >= 0 && gx < _width && y >= 0 && y < _height)
            {
                _cells[y, gx] = new Cell { Ch = merged[i], OpenAnsi = persistentOpen };
                if (_rowMaxDrawnX[y] < gx)
                    _rowMaxDrawnX[y] = gx;
            }

            col++;
            i++;
        }
    }

    public void DrawMultiLine(int x, int y, IReadOnlyList<string> lines, string ansiStyle = "")
    {
        for (int r = 0; r < lines.Count; r++)
            DrawText(x, y + r, lines[r], ansiStyle);
    }

    /// <summary>Centers a single line horizontally (same math as legacy <c>CenterVisual</c>).</summary>
    public void DrawCentered(int y, string text, string ansiStyle = "")
    {
        int v = _terminal.VisibleLength(text);
        if (v >= _width)
        {
            DrawText(0, y, text, ansiStyle);
            return;
        }

        int pad = _width - v;
        int left = pad / 2;
        DrawText(left, y, text, ansiStyle);
    }

    /// <summary>Thin box: <paramref name="width"/>×<paramref name="height"/> outer size using ┌┐└┘─│; inner area is left blank for callers.</summary>
    public void DrawBox(int x, int y, int width, int height, string borderAnsiOpen = "")
    {
        if (width < 2 || height < 2 || y + height > _height || x + width > _width || x < 0 || y < 0)
            return;

        int inner = width - 2;
        string top = "┌" + new string('─', inner) + "┐";
        string bottom = "└" + new string('─', inner) + "┘";
        DrawText(x, y, top, borderAnsiOpen);
        DrawText(x, y + height - 1, bottom, borderAnsiOpen);
        for (int r = 1; r < height - 1; r++)
        {
            DrawText(x, y + r, "│", borderAnsiOpen);
            DrawText(x + width - 1, y + r, "│", borderAnsiOpen);
        }
    }

    /// <summary>Clears the screen, homes the cursor, writes the buffer in one pass, hides the cursor (interactive terminals only).</summary>
    public void RenderToConsole()
    {
        if (_console.IsOutputRedirected)
        {
            for (int y = 0; y < _height; y++)
            {
                var sb = new StringBuilder(_width);
                if (_rowMaxDrawnX[y] < 0)
                {
                    _console.WriteLine();
                    continue;
                }

                int end = _width - 1;
                while (end > _rowMaxDrawnX[y] && _cells[y, end].Ch == ' ')
                    end--;
                for (int x = 0; x <= end; x++)
                    sb.Append(_cells[y, x].Ch);
                _console.WriteLine(sb.ToString());
            }

            return;
        }

        _console.SetCursorVisible(false);
        _console.Write("\u001b[H\u001b[2J");
        if (_terminal.UseAnsi)
            _console.Write(_terminal.Reset);

        var rowSb = new StringBuilder(_width * 4);
        for (int y = 0; y < _height; y++)
        {
            rowSb.Clear();
            string? lastOpen = null;
            for (int x = 0; x < _width; x++)
            {
                Cell c = _cells[y, x];
                if (_terminal.UseAnsi)
                {
                    if (c.OpenAnsi != lastOpen)
                    {
                        rowSb.Append(_terminal.Reset);
                        if (c.OpenAnsi.Length > 0)
                            rowSb.Append(c.OpenAnsi);
                        lastOpen = c.OpenAnsi;
                    }
                }

                rowSb.Append(c.Ch);
            }

            if (_terminal.UseAnsi)
                rowSb.Append(_terminal.Reset);
            _console.WriteLine(rowSb.ToString());
        }

        _console.FlushOutput();
    }
}
