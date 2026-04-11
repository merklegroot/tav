using System.Text;
using Tav.Models;

namespace Tav;

/// <summary>Fixed column geometry and adventure/map panel composition. Draws into a <see cref="ScreenBuffer"/> at explicit coordinates.</summary>
public static class AdventureLayout
{
    public const int Gap = 2;
    public const int LeftColumnWidth = 46;
    // Room panel spec (see README): 16 chars wide, 5 chars tall.
    public const int MapPanelOuterWidth = 16;
    public const int ScreenWidth = LeftColumnWidth + Gap + MapPanelOuterWidth;

    /// <summary>First column of the right panel in wide layout (map, portrait, compass).</summary>
    public const int RightPanelStartX = LeftColumnWidth + Gap;

    /// <summary>Vertical offset from the content start row: room/map begins one line below the title block gap.</summary>
    public const int MainViewRightPanelTopOffset = 1;

    public static bool CanUseWideLayout(int minWidth)
    {
        if (Console.IsOutputRedirected)
            return false;
        try
        {
            return Console.WindowWidth >= minWidth + 1;
        }
        catch
        {
            return false;
        }
    }

    public static string PadRightVisual(string s, int totalWidth)
    {
        int v = Terminal.VisibleLength(s);
        if (v > totalWidth)
            return Terminal.TruncateVisible(s, totalWidth);
        if (v == totalWidth)
            return s;
        return s + new string(' ', totalWidth - v);
    }

    public static string CenterVisual(string content, int totalWidth)
    {
        int v = Terminal.VisibleLength(content);
        if (v >= totalWidth)
            return PadRightVisual(content, totalWidth);

        int pad = totalWidth - v;
        int left = pad / 2;
        int right = pad - left;
        return new string(' ', left) + content + new string(' ', right);
    }

    /// <summary>Like <see cref="CenterVisual"/> but puts odd remainder padding on the left (portrait sprites).</summary>
    private static string CenterVisualBiasOddLeft(string content, int totalWidth)
    {
        int v = Terminal.VisibleLength(content);
        if (v >= totalWidth)
            return PadRightVisual(content, totalWidth);

        int pad = totalWidth - v;
        int right = pad / 2;
        int left = pad - right;
        return new string(' ', left) + content + new string(' ', right);
    }

    public static string Truncate(string s, int maxChars)
    {
        if (s.Length <= maxChars)
            return s;
        if (maxChars <= 1)
            return s[..1];
        return s[..(maxChars - 1)] + "…";
    }

    public static List<string> WrapText(string text, int width)
    {
        var lines = new List<string>();
        if (width <= 0)
        {
            lines.Add(text);
            return lines;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var words = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();
            foreach (var word in words)
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                    continue;
                }

                if (current.Length + 1 + word.Length <= width)
                {
                    current.Append(' ').Append(word);
                    continue;
                }

                lines.Add(current.ToString());
                current.Clear().Append(word);
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                continue;
            }

            if (words.Length == 0)
                lines.Add("");
        }

        if (lines.Count == 0)
            lines.Add("");
        return lines;
    }

    public static string BuildTitleBar(string screenTitle, GameState state, int screenWidth, int equippedArmorRating)
    {
        string left = Terminal.Title(screenTitle);
        string right = Terminal.HpStatus(state.HitPoints, state.MaxHitPoints)
                       + Terminal.Muted("  Gold: ")
                       + Terminal.Gold(state.Gold.ToString());
        if (equippedArmorRating > 0)
        {
            right += Terminal.Muted("  Armor ") + Terminal.Accent(equippedArmorRating.ToString());
        }

        int leftV = Terminal.VisibleLength(left);
        int rightV = Terminal.VisibleLength(right);
        int spaces = Math.Max(1, screenWidth - leftV - rightV);
        return PadRightVisual(left + new string(' ', spaces) + right, screenWidth);
    }

    public static List<string> BuildLeftColumnLines(GameState state, int leftColWidth)
    {
        var leftLines = new List<string>
        {
            Terminal.Accent(Truncate(state.CurrentRoom.Name, leftColWidth)),
        };
        leftLines.Add("");
        leftLines.AddRange(WrapText(state.CurrentRoom.Description, leftColWidth).Select(Terminal.Muted));
        return leftLines;
    }

    public static bool IsGroundMenuLine(MenuItem item) =>
        item.Key == 'g' && item.Text.StartsWith("(G)round", StringComparison.Ordinal);

    public static string FormatMenuLine(string text, char key, int maxWidth)
    {
        if (!Terminal.UseAnsi)
            return Truncate(text, maxWidth);

        char ku = char.ToUpperInvariant(key);
        string needle = $"({ku})";
        int i = text.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0)
            return Truncate(text, maxWidth);

        string before = Terminal.Muted(text[..i]);
        string hotkey = Terminal.MenuParenKey(ku);
        string after = Terminal.Muted(text[(i + needle.Length)..]);
        return Terminal.TruncateVisible(before + hotkey + after, maxWidth);
    }

    public static List<string> BuildMainViewLeftPanelLines(
        GameState state,
        IReadOnlyList<MenuItem> menuItems,
        int leftColWidth)
    {
        var lines = BuildLeftColumnLines(state, leftColWidth);
        if (menuItems.Count == 0)
            return lines;

        lines.Add("");
        int i = 0;
        if (IsGroundMenuLine(menuItems[0]))
        {
            lines.Add(FormatMenuLine(menuItems[0].Text, menuItems[0].Key, leftColWidth));
            lines.Add("");
            i = 1;
        }

        for (; i < menuItems.Count; i++)
            lines.Add(FormatMenuLine(menuItems[i].Text, menuItems[i].Key, leftColWidth));
        return lines;
    }

    private static bool HasExit(Room room, char dir) =>
        room.Exits?.ContainsKey(dir.ToString()) == true;

    /// <summary>Horizontal wall; optional centered <c>| |</c> for north (top) or south (bottom) exit (see README).</summary>
    private static string BuildHorizontalWall(
        char leftCorner,
        char rightCorner,
        int innerWidth,
        bool hasNorthSouthDoor)
    {
        if (!hasNorthSouthDoor || innerWidth < 3)
            return leftCorner + new string('─', innerWidth) + rightCorner;

        int dashTotal = innerWidth - 3;
        int leftDashes = dashTotal / 2;
        int rightDashes = dashTotal - leftDashes;
        return leftCorner
               + new string('─', leftDashes)
               + "| |"
               + new string('─', rightDashes)
               + rightCorner;
    }

    /// <summary>Inner rows: │ walls; <c>=</c> on the first title row when west/east exit (see README).</summary>
    private static string BuildSideWallLine(
        string paddedBody,
        int inner,
        bool openWest,
        bool openEast,
        bool useWestEastMarkers)
    {
        char left = openWest && useWestEastMarkers ? '=' : '│';
        char right = openEast && useWestEastMarkers ? '=' : '│';
        return left + paddedBody + right;
    }

    private static string PadInner(string content, int innerWidth)
    {
        if (content.Length > innerWidth)
            return content[..innerWidth];
        int pad = innerWidth - content.Length;
        int left = pad / 2;
        return new string(' ', left) + content + new string(' ', pad - left);
    }

    public static string[] BuildRoomPanel(
        Room room,
        int outerWidth,
        bool isCurrentRoom = false,
        bool forMapOverview = false)
    {
        int inner = outerWidth - 2;

        bool n = HasExit(room, 'n');
        bool e = HasExit(room, 'e');
        bool s = HasExit(room, 's');
        bool w = HasExit(room, 'w');

        var titleLines = WrapText(room.Name, Math.Max(1, inner));
        if (titleLines.Count > 3)
        {
            titleLines = titleLines.Take(3).ToList();
            titleLines[2] = Truncate(titleLines[2], Math.Max(1, inner));
        }

        for (int i = 0; i < titleLines.Count; i++)
            titleLines[i] = Truncate(titleLines[i], Math.Max(1, inner));

        string top = BuildHorizontalWall('┌', '┐', inner, hasNorthSouthDoor: n);
        string bottom = BuildHorizontalWall('└', '┘', inner, hasNorthSouthDoor: s);

        string blankInner = new string(' ', inner);
        var innerRows = new List<string>(capacity: 3);

        int titleCount = Math.Clamp(titleLines.Count, 1, 3);
        int startRow = (3 - titleCount + 1) / 2;
        int titleLineIndex = 0;
        for (int r = 0; r < 3; r++)
        {
            bool isTitleRow = r >= startRow && r < startRow + titleCount;
            string body = isTitleRow
                ? PadInner(titleLines[titleLineIndex++], inner)
                : blankInner;

            bool useMarkers = r == startRow;
            innerRows.Add(BuildSideWallLine(body, inner, w, e, useWestEastMarkers: useMarkers));
        }

        var plain = new[]
        {
            top,
            innerRows[0],
            innerRows[1],
            innerRows[2],
            bottom,
        };

        if (forMapOverview)
        {
            if (isCurrentRoom)
                return plain.Select(Terminal.MapHere).ToArray();
            return plain.Select(Terminal.Border).ToArray();
        }

        var bordered = plain.Select(Terminal.Border).ToArray();
        if (isCurrentRoom)
            return bordered.Select(Terminal.Accent).ToArray();

        return bordered;
    }

    public static string[] BuildCompassPanel(Room room, int outerWidth)
    {
        bool n = HasExit(room, 'n');
        bool e = HasExit(room, 'e');
        bool s = HasExit(room, 's');
        bool w = HasExit(room, 'w');

        string DirLetter(bool open, char letter) =>
            open ? Terminal.MenuParenKey(letter) : Terminal.Muted(letter.ToString());

        string rowN = CenterVisual(DirLetter(n, 'N'), outerWidth);
        string rowS = CenterVisual(DirLetter(s, 'S'), outerWidth);
        string pipe = CenterVisual(Terminal.Muted("|"), outerWidth);
        string rowWe = CenterVisual(
            DirLetter(w, 'W') + Terminal.Muted(" -   - ") + DirLetter(e, 'E'),
            outerWidth);

        return new[] { rowN, pipe, rowWe, pipe, rowS };
    }

    /// <summary>Room art, two blank rows, then the compass (see README).</summary>
    public static string[] BuildMainViewRightPanel(Room room, int outerWidth, bool isCurrentRoom = true)
    {
        string[] roomLines = BuildRoomPanel(room, outerWidth, isCurrentRoom);
        string[] compassLines = BuildCompassPanel(room, outerWidth);
        string blankRow = new string(' ', outerWidth);
        var combined = new string[roomLines.Length + 2 + compassLines.Length];
        int c = 0;
        for (int i = 0; i < roomLines.Length; i++)
            combined[c++] = roomLines[i];
        combined[c++] = blankRow;
        combined[c++] = blankRow;
        for (int i = 0; i < compassLines.Length; i++)
            combined[c++] = compassLines[i];
        return combined;
    }

    /// <summary>
    /// Every row truncated to <paramref name="panelOuter"/> visible columns, then centered (monster/item art, HP lines).
    /// Short rows are right-padded to a common block width so sprite columns line up, then centered with odd padding biased left.
    /// </summary>
    public static string[] BuildPortraitPanelCells(IReadOnlyList<string> borderedLines, int panelOuter)
    {
        int n = borderedLines.Count;
        int blockW = 0;
        for (int i = 0; i < n; i++)
        {
            int v = Terminal.VisibleLength(borderedLines[i]);
            if (v > 0 && v < panelOuter)
                blockW = Math.Max(blockW, v);
        }

        if (blockW == 0)
            blockW = 1;

        var panel = new string[n];
        for (int i = 0; i < n; i++)
        {
            string line = borderedLines[i];
            int v = Terminal.VisibleLength(line);
            string sized = v == 0 || v >= panelOuter
                ? line
                : PadRightVisual(line, blockW);
            string clipped = Terminal.TruncateVisible(sized, panelOuter);
            bool widenedToBlock = v > 0 && v < panelOuter && v < blockW;
            panel[i] = widenedToBlock
                ? CenterVisualBiasOddLeft(clipped, panelOuter)
                : CenterVisual(clipped, panelOuter);
        }

        return panel;
    }

    /// <summary>Thin box: inner rows padded to <c>outerWidth - 2</c> visible columns.</summary>
    public static string[] WrapThinBoxAroundInnerRows(string[] innerRows, int outerWidth)
    {
        int inner = outerWidth - 2;
        string top = Terminal.Border("┌" + new string('─', inner) + "┐");
        string bottom = Terminal.Border("└" + new string('─', inner) + "┘");
        int n = innerRows.Length;
        var result = new string[n + 2];
        result[0] = top;
        for (int i = 0; i < n; i++)
        {
            string body = PadRightVisual(innerRows[i], inner);
            result[i + 1] = Terminal.Border("│") + body + Terminal.Border("│");
        }

        result[n + 1] = bottom;
        return result;
    }

    /// <summary>Wide layout: left column at x=0, right panel at <see cref="RightPanelStartX"/>.</summary>
    public static void DrawTwoColumnRegion(
        ScreenBuffer buffer,
        int startY,
        int leftColWidth,
        int gap,
        int panelOuter,
        int screenWidth,
        IReadOnlyList<string> leftLines,
        IReadOnlyList<string> rightPanelLines,
        int rightPanelTopOffset)
    {
        int imageH = rightPanelLines.Count;
        int leftCount = leftLines.Count;
        string blankPanelRow = new string(' ', panelOuter);
        int h = Math.Max(leftCount, rightPanelTopOffset + imageH);

        for (int i = 0; i < h; i++)
        {
            string left = i < leftCount ? leftLines[i] : "";
            int legacyPi = i - rightPanelTopOffset;
            int pi = legacyPi >= 0 && legacyPi < imageH ? legacyPi : -1;
            string right = pi >= 0 ? rightPanelLines[pi] : blankPanelRow;
            right = PadRightVisual(right, panelOuter);
            string row = PadRightVisual(left, leftColWidth) + new string(' ', gap) + right;
            buffer.DrawText(0, startY + i, PadRightVisual(row, screenWidth));
        }
    }

    /// <summary>
    /// Draws the same row as <see cref="DrawTwoColumnRegion"/> in one pass (used when the right cell is computed per frame).
    /// </summary>
    public static void DrawWideCompositeRow(
        ScreenBuffer buffer,
        int y,
        string left,
        string right,
        int leftColWidth,
        int gap,
        int panelOuter,
        int screenWidth)
    {
        string r = PadRightVisual(right, panelOuter);
        string row = PadRightVisual(left, leftColWidth) + new string(' ', gap) + r;
        buffer.DrawText(0, y, PadRightVisual(row, screenWidth));
    }

    /// <summary>Line count for <see cref="DrawStackedTwoColumnFallback"/> (excluding title rows).</summary>
    public static int CountStackedContentRows(int leftCount, int portraitLineCount, int rightPanelTopOffset)
    {
        int n = leftCount;
        if (portraitLineCount > 0 && rightPanelTopOffset > 0)
            n++;
        n += portraitLineCount;
        return n;
    }

    /// <summary>Narrow / redirected: left block then gap then right block, full width each section line-by-line.</summary>
    public static void DrawStackedTwoColumnFallback(
        ScreenBuffer buffer,
        int startY,
        IReadOnlyList<string> leftLines,
        IReadOnlyList<string> portraitPanelLines,
        int rightPanelTopOffset)
    {
        int y = startY;
        foreach (string line in leftLines)
        {
            buffer.DrawText(0, y, line);
            y++;
        }

        if (portraitPanelLines.Count > 0)
        {
            if (rightPanelTopOffset > 0)
            {
                buffer.DrawText(0, y, "");
                y++;
            }

            foreach (string line in portraitPanelLines)
            {
                buffer.DrawText(0, y, line);
                y++;
            }
        }
    }

    /// <summary>Main adventure view: title, body (wide or stacked), trailing blank line in wide mode.</summary>
    public static void DrawInto(
        ScreenBuffer buffer,
        GameState state,
        IReadOnlyList<MenuItem> menuItems,
        int equippedArmorRating)
    {
        int leftColWidth = LeftColumnWidth;
        int panelOuter = MapPanelOuterWidth;
        int screenWidth = ScreenWidth;

        string titleBar = BuildTitleBar("== Adventure Game ==", state, screenWidth, equippedArmorRating);
        var leftLines = BuildMainViewLeftPanelLines(state, menuItems, leftColWidth);

        bool showCompass = menuItems.Count > 0;
        var panel = showCompass
            ? BuildMainViewRightPanel(state.CurrentRoom, panelOuter, isCurrentRoom: true)
            : BuildRoomPanel(state.CurrentRoom, panelOuter, isCurrentRoom: true);

        buffer.DrawText(0, 0, titleBar);
        buffer.DrawText(0, 1, "");

        if (CanUseWideLayout(screenWidth))
        {
            int H = Math.Max(leftLines.Count, MainViewRightPanelTopOffset + panel.Length);
            DrawTwoColumnRegion(
                buffer,
                startY: 2,
                leftColWidth,
                Gap,
                panelOuter,
                screenWidth,
                leftLines,
                panel,
                MainViewRightPanelTopOffset);
            buffer.DrawText(0, 2 + H, new string(' ', screenWidth));
            return;
        }

        DrawStackedTwoColumnFallback(buffer, 2, leftLines, panel, rightPanelTopOffset: 1);
        buffer.DrawText(0, 2 + leftLines.Count + 1 + panel.Length, "");
    }
}
