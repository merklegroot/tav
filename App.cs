using System.Text;
using Tav.Models;
using Tav.Store;

namespace Tav;

public interface IApp
{
    void Run();
}

public class App(
    GameState state,
    IRoomStore roomStore,
    IMonsterStore monsterStore,
    IManipulativeStore manipulativeStore,
    IManipulativeUtil manipulativeDescriber,
    IMonsterImageStore monsterImageStore,
    IManipulativeImageStore manipulativeImageStore) : IApp
{
    private readonly Random _random = new();

    public void Run()
    {
        var rooms = roomStore.LoadAll();
        var roomsById = rooms.ToDictionary(r => r.Id.ToLowerInvariant());
        var monsters = monsterStore.LoadAll();

        while (!state.ShouldExit)
        {
            var menuItems = BuildMenuItems(monsters, state, () => state.ShouldExit = true);

            PrintScreen(state, menuItems);

            var input = ReadInputChar();
            var normalized = char.ToLowerInvariant(input);
            if (TryNavigateCompass(normalized, roomsById, state))
                continue;

            menuItems.FirstOrDefault(m => m.Key == normalized)?.Action.Invoke();
        }
    }

    private void ClearConsole()
    {
        if (Console.IsOutputRedirected)
            return;

        Console.Write("\u001b[H\u001b[2J");
        if (Terminal.UseAnsi)
            Console.Write(Terminal.Reset);
        Console.Out.Flush();
    }

    private void PrintScreen(GameState state, IReadOnlyList<MenuItem> menuItems)
    {
        ClearConsole();
        foreach (var line in BuildScreenLines(state, menuItems))
            Console.WriteLine(line);
    }

    private void PauseForContinue()
    {
        if (Console.IsInputRedirected)
        {
            Console.WriteLine("(press Enter to continue)");
            _ = Console.ReadLine();
            return;
        }

        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(intercept: true);
    }

    private void PrintVictoryScreen(GameState state)
    {
        var left = new List<string>
        {
            Terminal.Title("== Victory =="),
            "",
            Terminal.Ok("The crown fits cold and sure."),
            Terminal.Ok(
                "Banners you never hung stir in a wind that has waited ages for an heir."),
            "",
            Terminal.Muted(
                "The tower exhales. Somewhere below, a door you did not open swings shut."),
        };

        int ar = EquippedArmorRating(state);
        int hb = EquippedHelmetSlotAttackBonus(state);
        if (ar > 0 || hb != 0)
        {
            left.Add("");
            var parts = new List<string>();
            if (ar > 0)
                parts.Add($"Armor {ar}");
            if (hb != 0)
            {
                string sign = hb > 0 ? "+" : "";
                parts.Add($"attack (helmet) {sign}{hb}");
            }

            left.Add(Terminal.Muted(string.Join(" · ", parts) + " — regalia and reach."));
        }

        left.Add("");
        left.Add(
            Terminal.Muted(
                $"You stand crowned. HP {state.HitPoints}/{state.MaxHitPoints}, gold {state.Gold}. The story ends here."));

        string? helmetId = state.EquippedHelmetId;
        var def = helmetId is not null ? manipulativeStore.Get(helmetId) : null;
        List<string> portrait = [];
        if (def?.Image is { Length: > 0 } stem)
            portrait.AddRange(manipulativeImageStore.Lines(stem).Select(Terminal.Border));

        Console.WriteLine();
        WriteTextAndRightImagePanel(left, portrait);
        Console.WriteLine();
    }

    /// <summary>
    /// Same two-column rules as <see cref="BuildScreenLines"/> wide layout: fixed left width, fixed right panel width,
    /// <c>rightPanelTopOffset</c> 1 so row 0 is left-only (like room name vs map). Right column is item art when present, otherwise blank (same column geometry).
    /// Inventory detail pads the left column (before <c>(ESC) Back</c>) when needed so the portrait stays contiguous and the ESC row has no art.
    /// Fight and victory/defeat screens use the same layout with bordered monster portrait lines.
    /// </summary>
    private static void WriteTextAndRightImagePanel(
        IReadOnlyList<string> leftLines,
        IReadOnlyList<string> portraitLines)
    {
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        string[] panel = portraitLines.Count > 0
            ? BuildPortraitPanelCells(portraitLines, panelOuter)
            : [];

        if (Console.IsOutputRedirected || !CanUseWideLayout(AdventureLayout.ScreenWidth))
        {
            foreach (string line in leftLines)
                Console.WriteLine(line);
            if (panel.Length > 0)
            {
                Console.WriteLine();
                foreach (string line in panel)
                    Console.WriteLine(line);
            }

            return;
        }

        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int screenWidth = AdventureLayout.ScreenWidth;
        const int rightPanelTopOffset = 1;

        int imageH = panel.Length;
        int leftCount = leftLines.Count;
        string blankPanelRow = new string(' ', panelOuter);

        int h = Math.Max(leftCount, rightPanelTopOffset + imageH);

        for (int i = 0; i < h; i++)
        {
            string left = i < leftCount ? leftLines[i] : "";
            int legacyPi = i - rightPanelTopOffset;
            int pi = legacyPi >= 0 && legacyPi < imageH ? legacyPi : -1;

            string right = pi >= 0 ? panel[pi] : blankPanelRow;
            right = PadRightVisual(right, panelOuter);

            string row = PadRightVisual(left, leftColWidth) + new string(' ', AdventureLayout.Gap) + right;
            Console.WriteLine(PadRightVisual(row, screenWidth));
        }
    }

    /// <summary>
    /// Pads each bordered portrait row to the same visible width (the widest line) using trailing spaces only, then
    /// centers that block in the panel. Center-padding shorter rows nudged cap lines right vs the widest row; left-aligning
    /// the block keeps taper rows aligned with the middle (see crown art).
    /// </summary>
    private static string[] BuildPortraitPanelCells(IReadOnlyList<string> borderedLines, int panelOuter)
    {
        int n = borderedLines.Count;
        var clipped = new string[n];
        for (int i = 0; i < n; i++)
            clipped[i] = Terminal.TruncateVisible(borderedLines[i], panelOuter);

        int maxV = 0;
        for (int i = 0; i < n; i++)
        {
            int v = Terminal.VisibleLength(clipped[i]);
            if (v > maxV)
                maxV = v;
        }

        var panel = new string[n];
        for (int i = 0; i < n; i++)
        {
            int v = Terminal.VisibleLength(clipped[i]);
            string normalized = clipped[i] + new string(' ', maxV - v);
            panel[i] = CenterVisual(normalized, panelOuter);
        }

        return panel;
    }

    /// <summary>Word-wraps one description line to the adventure left column width (plain-word wrap; re-applies muted style).</summary>
    private static List<string> WrapInventoryDescriptionLineToColumn(string line, int columnWidth)
    {
        if (Terminal.VisibleLength(line) <= columnWidth)
            return [line];

        string plain = Terminal.StripAnsi(line);
        return WrapText(plain, columnWidth).Select(Terminal.Muted).ToList();
    }

    /// <summary>Screen as lines. <paramref name="forceWide"/> builds the 72-column map+text layout even if the window is narrow (for slide snapshots).</summary>
    private List<string> BuildScreenLines(GameState state, IReadOnlyList<MenuItem> menuItems, bool forceWide = false)
    {
        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        int screenWidth = AdventureLayout.ScreenWidth;

        // Layout spec: a single title bar line with game title + basic stats, then two panels beneath.
        string titleBar = BuildTitleBar(state, screenWidth);

        var leftLines = BuildMainViewLeftPanelLines(state, menuItems, leftColWidth);

        bool showCompass = menuItems.Count > 0;
        var panel = showCompass
            ? BuildMainViewRightPanel(state.CurrentRoom, panelOuter, isCurrentRoom: true)
            : BuildRoomPanel(state.CurrentRoom, panelOuter, isCurrentRoom: true);

        int minWidth = screenWidth;
        if (forceWide || CanUseWideLayout(minWidth))
        {
            var lines = new List<string>();
            lines.Add(titleBar);
            lines.Add("");
            const int rightPanelTopOffset = 1; // fixed: room vertical start in wide layout (below title + gap)
            int H = Math.Max(leftLines.Count, rightPanelTopOffset + panel.Length);
            string blankPanelRow = new string(' ', panelOuter);

            for (int i = 0; i < H; i++)
            {
                string left = i < leftLines.Count ? leftLines[i] : "";
                int pi = i - rightPanelTopOffset;
                string right = pi >= 0 && pi < panel.Length ? panel[pi] : blankPanelRow;
                right = PadRightVisual(right, panelOuter);

                string row = PadRightVisual(left, leftColWidth) + new string(' ', AdventureLayout.Gap) + right;
                lines.Add(PadRightVisual(row, screenWidth));
            }

            lines.Add(new string(' ', screenWidth));
            return lines;
        }

        var stacked = new List<string>();
        stacked.Add(titleBar);
        stacked.Add("");
        foreach (var line in leftLines)
            stacked.Add(line);
        stacked.Add("");
        foreach (var line in panel)
            stacked.Add(line);
        stacked.Add("");
        return stacked;
    }

    private static List<string> BuildMainViewLeftPanelLines(
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

    private static bool IsGroundMenuLine(MenuItem item) =>
        item.Key == 'g' && item.Text.StartsWith("(G)round", StringComparison.Ordinal);

    private static string FormatMenuLine(string text, char key, int maxWidth)
    {
        // Mirror Terminal.WriteMenuLine, but return a single formatted line.
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

    private string BuildTitleBar(GameState state, int screenWidth)
    {
        string left = Terminal.Title("== Adventure Game ==");
        int armor = EquippedArmorRating(state);
        string right = Terminal.HpStatus(state.HitPoints, state.MaxHitPoints)
                       + Terminal.Muted("  Gold: ")
                       + Terminal.Gold(state.Gold.ToString());
        if (armor > 0)
        {
            right += Terminal.Muted("  Armor ") + Terminal.Accent(armor.ToString());
        }

        // Keep the title on the left; right-align the basic stats.
        int leftV = Terminal.VisibleLength(left);
        int rightV = Terminal.VisibleLength(right);
        int spaces = Math.Max(1, screenWidth - leftV - rightV);
        return PadRightVisual(left + new string(' ', spaces) + right, screenWidth);
    }

    private static List<string> BuildLeftColumnLines(GameState state, int leftColWidth)
    {
        var leftLines = new List<string>
        {
            Terminal.Accent(Truncate(state.CurrentRoom.Name, leftColWidth)),
        };
        leftLines.Add("");
        leftLines.AddRange(WrapText(state.CurrentRoom.Description, leftColWidth).Select(Terminal.Muted));
        return leftLines;
    }

    /// <summary>
    /// Slides only the room box (five lines); compass is omitted here — same as when the main menu is hidden.
    /// Left column uses description only (no menu), matching that layout.
    /// </summary>
    private void AnimateRoomSlide(string[] oldPanel, string[] newPanel, GameState afterNavigate, char direction)
    {
        if (Console.IsOutputRedirected)
            return;

        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        int screenWidth = AdventureLayout.ScreenWidth;

        if (!CanUseWideLayout(screenWidth))
            return;

        var newLeft = BuildLeftColumnLines(afterNavigate, leftColWidth);
        int panelRows = oldPanel.Length;
        const int rightPanelTopOffset = 1; // fixed: match main view
        int H = Math.Max(newLeft.Count, rightPanelTopOffset + panelRows);

        string titleBar = BuildTitleBar(afterNavigate, screenWidth);
        string blankRight = new string(' ', panelOuter);

        var oldRows = PadPanelRows(oldPanel, panelOuter);
        var newRows = PadPanelRows(newPanel, panelOuter);
        // East/west sliding uses Substring on a concatenation; indices must match visible columns, not raw bytes (ANSI breaks alignment).
        var oldRowsPlain = PadPanelRows(oldPanel.Select(Terminal.StripAnsi).ToArray(), panelOuter);
        var newRowsPlain = PadPanelRows(newPanel.Select(Terminal.StripAnsi).ToArray(), panelOuter);

        const int frames = 22;
        for (int f = 0; f < frames; f++)
        {
            double t = frames <= 1 ? 1 : f / (double)(frames - 1);
            ClearConsole();
            Console.WriteLine(titleBar);
            Console.WriteLine();

            // North: new map above old in the strip; scroll down. South: old above new; scroll down.
            if (direction is not 'e' and not 'w')
            {
                var strip = direction == 'n'
                    ? BuildVerticalStrip(newRows, oldRows)
                    : BuildVerticalStrip(oldRows, newRows);
                int scroll = direction == 'n'
                    ? (int)Math.Round((1 - t) * panelRows)
                    : (int)Math.Round(t * panelRows);
                scroll = Math.Clamp(scroll, 0, panelRows);

                for (int r = 0; r < H; r++)
                {
                    string left = r < newLeft.Count
                        ? PadRightVisual(newLeft[r], leftColWidth)
                        : new string(' ', leftColWidth);

                    int pi = r - rightPanelTopOffset;
                    string right = blankRight;
                    if (pi >= 0 && pi < panelRows)
                        right = strip[scroll + pi];

                    Console.WriteLine(left + new string(' ', AdventureLayout.Gap) + right);
                }
            }

            // East: [old|new], window slides right (offset ↑) — old leaves left, new enters from the right.
            // West: [new|old], window slides left (offset ↓) — old leaves right, new enters from the left.
            if (direction is 'e' or 'w')
            {
                bool east = direction == 'e';
                int offset = east
                    ? (int)Math.Round(t * panelOuter)
                    : (int)Math.Round((1 - t) * panelOuter);
                for (int r = 0; r < H; r++)
                {
                    string left = r < newLeft.Count
                        ? PadRightVisual(newLeft[r], leftColWidth)
                        : new string(' ', leftColWidth);

                    int pi = r - rightPanelTopOffset;
                    string right = blankRight;
                    if (pi >= 0 && pi < panelRows)
                    {
                        string combined = east
                            ? oldRowsPlain[pi] + newRowsPlain[pi]
                            : newRowsPlain[pi] + oldRowsPlain[pi];
                        string rightPlain = combined.Substring(offset, panelOuter);
                        right = Terminal.Border(rightPlain);
                    }

                    Console.WriteLine(left + new string(' ', AdventureLayout.Gap) + right);
                }
            }

            Thread.Sleep(28);
        }
    }

    private static List<string> PadPanelRows(string[] panel, int panelOuter)
    {
        var list = new List<string>(panel.Length);
        foreach (string line in panel)
            list.Add(PadRightVisual(line, panelOuter));
        return list;
    }

    private static List<string> BuildVerticalStrip(IReadOnlyList<string> top, IReadOnlyList<string> bottom)
    {
        var strip = new List<string>(top.Count + bottom.Count);
        strip.AddRange(top);
        strip.AddRange(bottom);
        return strip;
    }

    private static bool CanUseWideLayout(int minWidth)
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

    private static string PadRightVisual(string s, int totalWidth)
    {
        int v = Terminal.VisibleLength(s);
        if (v > totalWidth)
            return Terminal.TruncateVisible(s, totalWidth);
        if (v == totalWidth)
            return s;
        return s + new string(' ', totalWidth - v);
    }

    private static string Truncate(string s, int maxChars)
    {
        if (s.Length <= maxChars)
            return s;
        if (maxChars <= 1)
            return s[..1];
        return s[..(maxChars - 1)] + "…";
    }

    private static List<string> WrapText(string text, int width)
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

    private static string[] BuildRoomPanel(
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

        // Title wrapping: center, up to 3 lines (spec in README).
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

        // Height spec: 5 total rows: top, 3 inner, bottom.
        // Vertically center title within the 3 inner rows.
        string blankInner = new string(' ', inner);
        var innerRows = new List<string>(capacity: 3);

        int titleCount = Math.Clamp(titleLines.Count, 1, 3);
        // For 3 inner rows, bias "centering" downward so 2-line titles look like the README example:
        // blank row, then line 1, then line 2 (startRow = 1).
        int startRow = (3 - titleCount + 1) / 2;
        int titleLineIndex = 0;
        for (int r = 0; r < 3; r++)
        {
            bool isTitleRow = r >= startRow && r < startRow + titleCount;
            string body = isTitleRow
                ? PadInner(titleLines[titleLineIndex++], inner)
                : blankInner;

            // East/West door marker: '=' replaces the wall character on the first title row (matches example).
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

    /// <summary>Room art, two blank rows, then the compass (see README).</summary>
    private static string[] BuildMainViewRightPanel(Room room, int outerWidth, bool isCurrentRoom = true)
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

    /// <summary>README compass: N/|/W-E row/|/S; available directions show as <c>(N)</c> etc.</summary>
    private static string[] BuildCompassPanel(Room room, int outerWidth)
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

    private static string CenterVisual(string content, int totalWidth)
    {
        int v = Terminal.VisibleLength(content);
        if (v >= totalWidth)
            return PadRightVisual(content, totalWidth);

        int pad = totalWidth - v;
        int left = pad / 2;
        int right = pad - left;
        return new string(' ', left) + content + new string(' ', right);
    }

    private static string PadInner(string content, int innerWidth)
    {
        if (content.Length > innerWidth)
            return content[..innerWidth];
        int pad = innerWidth - content.Length;
        int left = pad / 2;
        return new string(' ', left) + content + new string(' ', pad - left);
    }

    private static void WritePlayerStatusHeader(string screenTitle, GameState state, bool includeGold = true)
    {
        Console.WriteLine(Terminal.Title(screenTitle));
        Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
        if (!includeGold)
            return;

        Console.WriteLine(Terminal.Muted("Gold: ") + Terminal.Gold(state.Gold.ToString()));
    }

    private string FormatGroundStackLine(GroundItemStack stack)
    {
        string name = manipulativeDescriber.GetDisplayName(stack.Id);
        if (stack.Quantity <= 1)
            return name;

        return $"{name} (x{stack.Quantity})";
    }

    private static bool IsInventoryItemEquipped(GameState state, string manipulativeId)
    {
        if (state.EquippedWeaponId is not null
            && string.Equals(state.EquippedWeaponId, manipulativeId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (state.EquippedHelmetId is not null
            && string.Equals(state.EquippedHelmetId, manipulativeId, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Inventory list line: optional yellow <c>E</c> for equipped rows, muted padding when another row is equipped.</summary>
    private static void WriteInventoryListLine(
        bool showEquippedYellowE,
        bool padWhenUnequipped,
        int slotNumber,
        string displayName,
        char hotkey)
    {
        string menuText = $"({slotNumber}) {displayName}";
        if (!Terminal.UseAnsi)
        {
            if (showEquippedYellowE)
                Console.Write("E ");
            else if (padWhenUnequipped)
                Console.Write("  ");
            Console.WriteLine(menuText);
            return;
        }

        if (showEquippedYellowE)
        {
            Console.Write(Terminal.Warn("E"));
            Console.Write(Terminal.Muted(" "));
        }
        else if (padWhenUnequipped)
        {
            Console.Write(Terminal.Muted("  "));
        }

        char ku = char.ToUpperInvariant(hotkey);
        string needle = $"({ku})";
        int i = menuText.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0)
        {
            Console.WriteLine(menuText);
            return;
        }

        Console.Write(Terminal.Muted(menuText[..i]));
        Console.Write(Terminal.MenuParenKey(ku));
        Console.Write(Terminal.Muted(menuText[(i + needle.Length)..]));
        Console.WriteLine();
    }

    private void RunInventoryScreen(GameState state)
    {
        string? listFeedback = null;
        while (true)
        {
            ClearConsole();
            WritePlayerStatusHeader("== Inventory ==", state);
            Console.WriteLine();
            if (!string.IsNullOrEmpty(listFeedback))
            {
                foreach (string line in listFeedback.Split(Environment.NewLine))
                    Console.WriteLine(Terminal.Muted(line));
                Console.WriteLine();
                listFeedback = null;
            }

            int n = state.Inventory.Count;
            if (n == 0)
            {
                Console.WriteLine(Terminal.Muted("  (nothing)"));
                Console.WriteLine();
                PauseForContinue();
                return;
            }

            bool anyEquipped = false;
            for (int j = 0; j < n; j++)
            {
                if (IsInventoryItemEquipped(state, state.Inventory[j]))
                {
                    anyEquipped = true;
                    break;
                }
            }

            for (int i = 0; i < n; i++)
            {
                int num = i + 1;
                char key = (char)('0' + num);
                string id = state.Inventory[i];
                bool rowEquipped = IsInventoryItemEquipped(state, id);
                WriteInventoryListLine(
                    showEquippedYellowE: anyEquipped && rowEquipped,
                    padWhenUnequipped: anyEquipped && !rowEquipped,
                    slotNumber: num,
                    displayName: manipulativeDescriber.GetDisplayName(id),
                    hotkey: key);
            }
            Console.WriteLine();
            Console.WriteLine(Terminal.EscBackHint());

            int? selectedIndex = ReadInventoryItemIndex(n);
            if (selectedIndex is null)
                return;

            listFeedback = RunSelectedInventoryItem(state, selectedIndex.Value);
            if (state.GameWon)
            {
                ClearConsole();
                PrintVictoryScreen(state);
                PauseForContinue();
                state.ShouldExit = true;
                return;
            }
        }
    }

    private static void AddInventoryItemDetailMenuLines(
        List<string> left,
        bool canEat,
        bool offerEquip,
        bool offerUnequip,
        bool omitBlankLineBeforeEsc)
    {
        int w = AdventureLayout.LeftColumnWidth;
        if (canEat)
            left.Add(FormatMenuLine("(E)at", 'e', w));
        if (offerEquip)
            left.Add(FormatMenuLine("(E)quip", 'e', w));
        if (offerUnequip)
            left.Add(FormatMenuLine("(U)nequip", 'u', w));
        left.Add(FormatMenuLine("(D)rop", 'd', w));
        if (!omitBlankLineBeforeEsc)
            left.Add("");
        left.Add(Terminal.EscBackHint());
    }

    /// <summary>Detail screen for one stack. Returns text to show above the list on return, or <see langword="null"/> if the player backed out without acting.</summary>
    private string? RunSelectedInventoryItem(GameState state, int index)
    {
        if (index < 0 || index >= state.Inventory.Count)
            return null;

        string id = state.Inventory[index];
        var def = manipulativeStore.Get(id);
        bool canEat = def is { IsEdible: true } && (def.ConsumeEffects?.HealthRestored ?? 0) > 0;
        bool canEquipWeapon = def is { IsEquippableWeapon: true };
        bool canEquipHelmet = def is { IsEquippableHelmet: true };
        bool isWeaponEquipped = canEquipWeapon
            && state.EquippedWeaponId is not null
            && string.Equals(state.EquippedWeaponId, id, StringComparison.OrdinalIgnoreCase);
        bool isHelmetEquipped = canEquipHelmet
            && state.EquippedHelmetId is not null
            && string.Equals(state.EquippedHelmetId, id, StringComparison.OrdinalIgnoreCase);
        bool offerEquip = (canEquipWeapon && !isWeaponEquipped) || (canEquipHelmet && !isHelmetEquipped);
        bool offerUnequip = (canEquipWeapon && isWeaponEquipped) || (canEquipHelmet && isHelmetEquipped);

        ClearConsole();
        WritePlayerStatusHeader("== Inventory ==", state);
        Console.WriteLine();

        bool withImage = def?.Image is { Length: > 0 };

        var left = new List<string>
        {
            Terminal.Accent($"Selected: {manipulativeDescriber.GetDisplayName(id)}"),
        };
        int descCol = AdventureLayout.LeftColumnWidth;
        if (canEat && def is not null)
        {
            if (!withImage)
                left.Add("");
            foreach (string d in manipulativeDescriber.GetEdibleEffectDescriptionLines(def, state))
                left.AddRange(WrapInventoryDescriptionLineToColumn(d, descCol));
        }

        if (canEquipHelmet && def is not null)
        {
            if (!withImage)
                left.Add("");
            foreach (string d in manipulativeDescriber.GetHelmetEffectDescriptionLines(def))
                left.AddRange(WrapInventoryDescriptionLineToColumn(d, descCol));
        }

        if (!withImage)
            left.Add("");
        AddInventoryItemDetailMenuLines(left, canEat, offerEquip, offerUnequip, omitBlankLineBeforeEsc: withImage);

        List<string> portrait = [];
        if (withImage && def is not null && def.Image is { Length: > 0 } stem)
            portrait.AddRange(manipulativeImageStore.Lines(stem).Select(Terminal.Border));

        if (withImage && portrait.Count > 0)
        {
            // Keep portrait lines contiguous: ESC is last left row with a blank right panel. Without padding, skipping
            // ESC would leave a visible gap in the art between the last pre-ESC row and the rest of the image.
            const int rightPanelTopOffset = 1;
            int targetLeftLineCount = rightPanelTopOffset + portrait.Count + 1;
            int pad = targetLeftLineCount - left.Count;
            for (int i = 0; i < pad; i++)
                left.Insert(left.Count - 1, "");
        }

        Console.WriteLine();
        WriteTextAndRightImagePanel(left, portrait);

        var action = ReadInventoryItemDetailAction(canEat, offerEquip: offerEquip, offerUnequip: offerUnequip);
        if (action == InventoryItemDetailAction.BackToList)
            return null;
        if (action == InventoryItemDetailAction.Drop)
        {
            var dropped = GameStateGroundOps.DropInventoryItemAt(state, index);
            return $"You drop the {manipulativeDescriber.GetDisplayName(dropped)}.";
        }

        if (action == InventoryItemDetailAction.Equip)
        {
            if (def!.IsEquippableWeapon)
                state.EquippedWeaponId = def.Id;
            if (def.IsEquippableHelmet)
                state.EquippedHelmetId = def.Id;

            if (def.IsEquippableHelmet
                && string.Equals(def.Id, KnownManipulativeIds.Crown, StringComparison.OrdinalIgnoreCase))
                state.GameWon = true;

            var lines = new List<string>
            {
                def.IsEquippableHelmet && !def.IsEquippableWeapon
                    ? $"You put on the {def.Name.ToLowerInvariant()}."
                    : $"You equip the {def.Name.ToLowerInvariant()}.",
            };
            if (def.IsEquippableHelmet && (def.Armor ?? 0) > 0)
            {
                int ar = EquippedArmorRating(state);
                lines.Add(
                    $"Armor is now {ar}: each enemy hit loses up to {ar} damage (min. 1 per hit).");
            }

            if (def.IsEquippableWeapon && (def.AttackBonus ?? 0) != 0)
            {
                int wb = EquippedWeaponSlotAttackBonus(state);
                string sign = wb > 0 ? "+" : "";
                lines.Add(
                    $"Attack bonus from your weapon is now {sign}{wb} (adds to strike damage on each hit you land).");
            }

            if (def.IsEquippableHelmet && (def.AttackBonus ?? 0) != 0)
            {
                int hb = EquippedHelmetSlotAttackBonus(state);
                string sign = hb > 0 ? "+" : "";
                lines.Add(
                    $"Attack bonus from your helmet is now {sign}{hb} (stacks with your weapon on each hit).");
            }

            return string.Join(Environment.NewLine, lines);
        }

        if (action == InventoryItemDetailAction.Unequip)
        {
            if (canEquipWeapon && isWeaponEquipped)
                state.EquippedWeaponId = null;
            if (canEquipHelmet && isHelmetEquipped)
                state.EquippedHelmetId = null;

            var defForUnequip = def!;
            string unequipNote = defForUnequip switch
            {
                { IsEquippableHelmet: true, IsEquippableWeapon: false } =>
                    $"You take off the {defForUnequip.Name.ToLowerInvariant()}.",
                _ => "You put the weapon away.",
            };
            var lines = new List<string> { unequipNote };
            if (canEquipHelmet && isHelmetEquipped)
            {
                if ((defForUnequip.Armor ?? 0) > 0)
                {
                    int ar = EquippedArmorRating(state);
                    lines.Add($"Without that helmet, your Armor is {ar}.");
                }

                if ((defForUnequip.AttackBonus ?? 0) != 0)
                    lines.Add("This helmet no longer adds to your strike damage.");
            }

            if (canEquipWeapon && isWeaponEquipped && (defForUnequip.AttackBonus ?? 0) != 0)
                lines.Add("That weapon no longer adds to your strike damage.");

            return string.Join(Environment.NewLine, lines);
        }

        return TryUseInventoryItem(state, index);
    }

    /// <summary>Eats an edible item when the player chose (E)at. When consumed, the list shrinks at <paramref name="index"/>.</summary>
    private string TryUseInventoryItem(GameState state, int index)
    {
        string id = state.Inventory[index];
        var def = manipulativeStore.Get(id);
        if (def is null)
            return "You can't think of a way to use that here.";

        if (!def.IsEdible)
            return "You can't think of a way to use that here.";

        int cap = def.ConsumeEffects?.HealthRestored ?? 0;
        if (cap <= 0)
            return "You can't think of a way to use that here.";

        int missing = state.MaxHitPoints - state.HitPoints;
        int heal = Math.Min(cap, missing);
        state.HitPoints += heal;
        state.Inventory.RemoveAt(index);
        string label = def.Name.ToLowerInvariant();
        if (heal <= 0)
        {
            return $"You eat the {label}. You're already at full health — satisfying, but no healing needed.";
        }

        if (heal >= cap)
        {
            return $"You eat the {label}. Sweet juice; warmth spreads through you.";
        }

        return $"You eat the {label} and recover {heal} HP.";
    }

    private enum InventoryItemDetailAction
    {
        BackToList,
        Drop,
        UseOrEat,
        Equip,
        Unequip,
    }

    // Redirected stdin: Console.ReadKey is not supported — use ReadLine in those branches.
    private static InventoryItemDetailAction ReadInventoryItemDetailAction(
        bool offerEat,
        bool offerEquip,
        bool offerUnequip)
    {
        if (Console.IsInputRedirected)
        {
            while (true)
            {
                var line = Console.ReadLine();
                if (line is null)
                    return InventoryItemDetailAction.BackToList;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string t = line.Trim().ToLowerInvariant();
                if (t is "esc" or "escape")
                    return InventoryItemDetailAction.BackToList;
                if (t is "d" or "drop")
                    return InventoryItemDetailAction.Drop;
                if (offerEat && (t is "e" or "eat"))
                    return InventoryItemDetailAction.UseOrEat;
                if (offerEquip && (t == "equip" || (!offerEat && t == "e")))
                    return InventoryItemDetailAction.Equip;
                if (offerUnequip && (t is "u" or "unequip"))
                    return InventoryItemDetailAction.Unequip;
            }
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
                return InventoryItemDetailAction.BackToList;
            char c = char.ToLowerInvariant(key.KeyChar);
            if (c == 'd')
                return InventoryItemDetailAction.Drop;
            if (offerEat && c == 'e')
                return InventoryItemDetailAction.UseOrEat;
            if (offerEquip && c == 'e' && !offerEat)
                return InventoryItemDetailAction.Equip;
            if (offerUnequip && c == 'u')
                return InventoryItemDetailAction.Unequip;
        }
    }

    /// <summary>Returns 0-based inventory index to act on, or null to leave inventory.</summary>
    private int? ReadInventoryItemIndex(int itemCount)
    {
        if (itemCount <= 0)
            return null;

        if (Console.IsInputRedirected)
        {
            while (true)
            {
                var line = Console.ReadLine();
                if (line is null)
                    return null;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string t = line.Trim().ToLowerInvariant();
                if (t is "esc" or "escape")
                    return null;
                if (int.TryParse(line.Trim(), out int num) && num >= 1 && num <= itemCount)
                    return num - 1;
            }
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
                return null;
            char c = char.ToLowerInvariant(key.KeyChar);
            if (c >= '1' && c <= '0' + itemCount)
                return c - '1';
        }
    }

    private void RunPickUpScreen(GameState state)
    {
        while (true)
        {
            int n = GameStateGroundOps.GetStacksInCurrentRoom(state).Count;
            if (n == 0)
            {
                ClearConsole();
                Console.WriteLine(Terminal.Title("== Ground =="));
                Console.WriteLine(Terminal.Muted("Nothing on the ground."));
                Console.WriteLine();
                PauseForContinue();
                return;
            }

            ClearConsole();
            WritePlayerStatusHeader("== Ground ==", state);
            Console.WriteLine();
            for (int i = 0; i < n; i++)
            {
                int num = i + 1;
                char key = (char)('0' + num);
                Terminal.WriteMenuLine(
                    $"({num}) {FormatGroundStackLine(GameStateGroundOps.GetStacksInCurrentRoom(state)[i])}",
                    key);
            }
            Console.WriteLine();
            Console.WriteLine(Terminal.EscBackHint());

            int? selectedIndex = ReadInventoryItemIndex(n);
            if (selectedIndex is null)
                return;

            if (RunSelectedGroundItem(state, selectedIndex.Value))
                return;
        }
    }

    /// <returns><see langword="true"/> if an item was taken (caller should leave the Ground screen).</returns>
    private bool RunSelectedGroundItem(GameState state, int index)
    {
        var ground = GameStateGroundOps.GetStacksInCurrentRoom(state);
        if (index < 0 || index >= ground.Count)
            return false;

        GroundItemStack stack = ground[index];
        ClearConsole();
        WritePlayerStatusHeader("== Ground ==", state, includeGold: false);
        Console.WriteLine();
        Console.WriteLine(Terminal.Accent($"Selected: {FormatGroundStackLine(stack)}"));
        Console.WriteLine();
        Terminal.WriteMenuLine("(T)ake", 't');
        Console.WriteLine();
        Console.WriteLine(Terminal.EscBackHint());

        var action = ReadSelectedGroundItemAction();
        if (action == SelectedGroundItemAction.BackToList)
            return false;

        var taken = GameStateGroundOps.PickUpGroundItemAt(state, index);
        if (taken is null)
            return false;
        Console.WriteLine();
        Console.WriteLine(
            Terminal.Muted($"You pick up the {manipulativeDescriber.GetDisplayName(taken)}."));
        return true;
    }

    private enum SelectedGroundItemAction
    {
        BackToList,
        Take,
    }

    private static SelectedGroundItemAction ReadSelectedGroundItemAction()
    {
        if (Console.IsInputRedirected)
        {
            while (true)
            {
                var line = Console.ReadLine();
                if (line is null)
                    return SelectedGroundItemAction.BackToList;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string t = line.Trim().ToLowerInvariant();
                if (t is "esc" or "escape")
                    return SelectedGroundItemAction.BackToList;
                if (t is "t" or "take")
                    return SelectedGroundItemAction.Take;
            }
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
                return SelectedGroundItemAction.BackToList;
            char c = char.ToLowerInvariant(key.KeyChar);
            if (c == 't')
                return SelectedGroundItemAction.Take;
        }
    }

    private void PrintCharacter(GameState state)
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Character =="));
        Console.WriteLine();
        Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
        Console.WriteLine(Terminal.Muted("Gold: ") + Terminal.Gold(state.Gold.ToString()));
        Console.WriteLine(Terminal.Accent($"STR: {state.Strength}"));
        Console.WriteLine(Terminal.Accent($"DEX: {state.Dexterity}"));
        Console.WriteLine(
            Terminal.Muted(
                "Higher DEX helps you act first, dodge, and land cleaner hits in a fight."));
        int combatArmor = EquippedArmorRating(state);
        Console.WriteLine(
            Terminal.Accent($"Armor: {combatArmor}")
            + Terminal.Muted(
                " — strips up to that much from each enemy hit (min. 1 damage per hit)."));
        Console.WriteLine();
        WriteCharacterEquippedSection(state);
        Console.WriteLine();
        PauseForContinue();
    }

    private void WriteCharacterEquippedSection(GameState state)
    {
        Console.WriteLine(Terminal.Accent("Equipped"));
        if (state.EquippedWeaponId is null)
        {
            Console.WriteLine(Terminal.Muted("  Weapon: none"));
        }
        else
        {
            var weaponDef = manipulativeStore.Get(state.EquippedWeaponId);
            string weaponName = weaponDef?.Name ?? manipulativeDescriber.GetDisplayName(state.EquippedWeaponId);
            Console.WriteLine(Terminal.Accent($"  Weapon: {weaponName}"));

            if (weaponDef is null)
            {
                Console.WriteLine(Terminal.Muted("  No effect data for this item."));
            }
            else
            {
                bool wroteWeaponBonus = false;
                if (weaponDef.AttackBonus is int bonus && bonus != 0)
                {
                    string sign = bonus > 0 ? "+" : "";
                    Console.WriteLine(
                        Terminal.Muted($"  Attack {sign}{bonus} on each strike in a fight."));
                    wroteWeaponBonus = true;
                }

                if (!wroteWeaponBonus && weaponDef.IsEquippableWeapon)
                    Console.WriteLine(Terminal.Muted("  No combat bonuses from this weapon."));
            }
        }

        if (state.EquippedHelmetId is null)
        {
            Console.WriteLine(Terminal.Muted("  Helmet: none"));
            return;
        }

        var helmetDef = manipulativeStore.Get(state.EquippedHelmetId);
        string helmetName = helmetDef?.Name ?? manipulativeDescriber.GetDisplayName(state.EquippedHelmetId);
        Console.WriteLine(Terminal.Accent($"  Helmet: {helmetName}"));

        if (helmetDef is null)
        {
            Console.WriteLine(Terminal.Muted("  No effect data for this item."));
            return;
        }

        bool wroteHelmet = false;
        if (helmetDef.Armor is int ar && ar > 0)
        {
            Console.WriteLine(
                Terminal.Muted($"  Armor {ar} — strips up to {ar} from each enemy hit (min. 1 damage per hit)."));
            wroteHelmet = true;
        }
        else if (helmetDef.IsEquippableHelmet)
        {
            Console.WriteLine(Terminal.Muted("  Armor 0 — no reduction from this helmet."));
            wroteHelmet = true;
        }

        if (helmetDef.AttackBonus is int hb && hb != 0)
        {
            string sign = hb > 0 ? "+" : "";
            Console.WriteLine(
                Terminal.Muted($"  Attack {sign}{hb} from helmet — stacks with weapon on each hit you land."));
            wroteHelmet = true;
        }

        if (!wroteHelmet && helmetDef.IsEquippableHelmet)
            Console.WriteLine(Terminal.Muted("  No combat bonuses from this helmet."));
    }

    private void PrintHelp()
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Help =="));
        Console.WriteLine();
        Console.WriteLine(Terminal.Muted("Move with N, E, S, W (see compass)."));
        int helpW = HelpScreenMenuLineWidth();
        Console.WriteLine(
            FormatMenuLine(
                "(I)nventory: select an item. Edible gear shows healing; helmets (including crowns) show Armor and attack bonus; then Eat, Equip, Drop, or Esc.",
                'i',
                helpW));
        Console.WriteLine(FormatMenuLine("(G)round appears when something lies on the ground here.", 'g', helpW));
        Console.WriteLine(FormatMenuLine("(M)ap: overview of how the areas connect.", 'm', helpW));
        Console.WriteLine(
            FormatMenuLine("(F)ight: Attack or Run. Wins yield gold; sometimes a find.", 'f', helpW));
        Console.WriteLine();
        PauseForContinue();
    }

    /// <summary>Width for <see cref="FormatMenuLine"/> on the help screen so long lines are not clipped too aggressively.</summary>
    private static int HelpScreenMenuLineWidth()
    {
        try
        {
            int window = Console.WindowWidth;
            if (window > 1)
                return Math.Max(window - 1, AdventureLayout.ScreenWidth);
        }
        catch
        {
        }

        return Math.Max(96, AdventureLayout.ScreenWidth);
    }

    private void PrintMapScreen(GameState state)
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Map =="));
        Console.WriteLine();
        Console.WriteLine(
            Terminal.Muted("Rough layout of the grounds. Your room is drawn in yellow."));
        Console.WriteLine();

        // Map overview: a 3×3 window centered on the current room, drawn using the same room box as the right panel.
        // Coordinates: (0,0) is current. North is y=-1, south is y=+1.
        var allRooms = roomStore.LoadAll();
        var roomsById = allRooms.ToDictionary(r => r.Id.ToLowerInvariant());

        int outerW = AdventureLayout.MapPanelOuterWidth;
        int outerH = BuildRoomPanel(state.CurrentRoom, outerW, isCurrentRoom: true, forMapOverview: true)
            .Length;
        const int maxRadius = 1; // 3×3
        const int gap = 2;
        const int indent = 2;

        var placed = new Dictionary<(int x, int y), Room>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var q = new Queue<(Room room, int x, int y)>();

        q.Enqueue((state.CurrentRoom, 0, 0));
        seen.Add(state.CurrentRoom.Id);

        while (q.Count > 0)
        {
            var (room, x, y) = q.Dequeue();
            if (Math.Abs(x) > maxRadius || Math.Abs(y) > maxRadius)
                continue;

            if (!placed.ContainsKey((x, y)))
                placed[(x, y)] = room;

            if (room.Exits is null)
                continue;

            foreach (var kv in room.Exits)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                    continue;

                char dir = char.ToLowerInvariant(kv.Key.Trim()[0]);
                (int dx, int dy) = dir switch
                {
                    'n' => (0, -1),
                    'e' => (1, 0),
                    's' => (0, 1),
                    'w' => (-1, 0),
                    _ => (0, 0),
                };
                if (dx == 0 && dy == 0)
                    continue;

                string destId = kv.Value.ToLowerInvariant();
                if (!roomsById.TryGetValue(destId, out var dest))
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (Math.Abs(nx) > maxRadius || Math.Abs(ny) > maxRadius)
                    continue;

                if (seen.Add(dest.Id))
                    q.Enqueue((dest, nx, ny));
            }
        }

        string[] BlankPanel()
        {
            string blankLine = new string(' ', outerW);
            var p = new string[outerH];
            for (int i = 0; i < outerH; i++)
                p[i] = blankLine;
            return p;
        }

        string[] BoxFor((int x, int y) cell)
        {
            if (!placed.TryGetValue(cell, out var room))
                return BlankPanel();
            bool isCurrent = room.Id.Equals(state.CurrentRoom.Id, StringComparison.OrdinalIgnoreCase);
            return BuildRoomPanel(room, outerW, isCurrentRoom: isCurrent, forMapOverview: true);
        }

        // Render rows y=-1..1 (north to south), columns x=-1..1 (west to east).
        for (int y = -maxRadius; y <= maxRadius; y++)
        {
            var rowPanels = new[]
            {
                BoxFor((-maxRadius, y)),
                BoxFor((0, y)),
                BoxFor((maxRadius, y)),
            };

            for (int line = 0; line < outerH; line++)
            {
                Console.Write(new string(' ', indent));
                Console.Write(rowPanels[0][line]);
                Console.Write(new string(' ', gap));
                Console.Write(rowPanels[1][line]);
                Console.Write(new string(' ', gap));
                Console.Write(rowPanels[2][line]);
                Console.WriteLine();
            }

            if (y != maxRadius)
                Console.WriteLine();
        }

        Console.WriteLine();
        PauseForContinue();
    }

    private List<MenuItem> BuildMenuItems(
        IReadOnlyList<Monster> monsters,
        GameState state,
        Action exit)
    {
        var items = new List<MenuItem>();

        var groundStacks = GameStateGroundOps.GetStacksInCurrentRoom(state);
        if (groundStacks.Count > 0)
        {
            string groundList = string.Join(
                ", ",
                groundStacks.Select(FormatGroundStackLine));
            items.Add(new MenuItem
            {
                Text = $"(G)round - {groundList}",
                Key = 'g',
                Action = () => RunPickUpScreen(state),
            });
        }

        items.Add(new MenuItem
        {
            Text = "(I)nventory",
            Key = 'i',
            Action = () => RunInventoryScreen(state),
        });
        items.Add(new MenuItem
        {
            Text = "(C)haracter",
            Key = 'c',
            Action = () => PrintCharacter(state),
        });
        items.Add(new MenuItem
        {
            Text = "(M)ap",
            Key = 'm',
            Action = () => PrintMapScreen(state),
        });
        items.Add(new MenuItem
        {
            Text = "(F)ight",
            Key = 'f',
            Action = () => RunFightEncounter(state, monsters),
        });
        items.Add(new MenuItem
        {
            Text = "(H)elp",
            Key = 'h',
            Action = () => PrintHelp(),
        });
        items.Add(new MenuItem
        {
            Text = "e(X)it",
            Key = 'x',
            Action = exit,
        });
        return items;
    }

    private bool TryNavigateCompass(
        char normalized,
        IReadOnlyDictionary<string, Room> roomsById,
        GameState state)
    {
        if (normalized is not ('n' or 'e' or 's' or 'w'))
            return false;

        var room = state.CurrentRoom;
        if (room.Exits is null ||
            !room.Exits.TryGetValue(normalized.ToString(), out var destId))
            return false;

        if (!roomsById.TryGetValue(destId.ToLowerInvariant(), out var destRoom))
            return false;

        var oldRoomPanel = BuildRoomPanel(state.CurrentRoom, AdventureLayout.MapPanelOuterWidth, isCurrentRoom: true);
        state.CurrentRoom = destRoom;
        AnimateRoomSlide(
            oldRoomPanel,
            BuildRoomPanel(state.CurrentRoom, AdventureLayout.MapPanelOuterWidth, isCurrentRoom: true),
            state,
            normalized);
        return true;
    }

    private int EquippedWeaponSlotAttackBonus(GameState state)
    {
        if (state.EquippedWeaponId is null)
            return 0;
        return manipulativeStore.Get(state.EquippedWeaponId)?.AttackBonus ?? 0;
    }

    private int EquippedArmorRating(GameState state)
    {
        if (state.EquippedHelmetId is null)
            return 0;
        return manipulativeStore.Get(state.EquippedHelmetId)?.Armor ?? 0;
    }

    private int EquippedHelmetSlotAttackBonus(GameState state)
    {
        if (state.EquippedHelmetId is null)
            return 0;
        return manipulativeStore.Get(state.EquippedHelmetId)?.AttackBonus ?? 0;
    }

    private void RunFightEncounter(GameState state, IReadOnlyList<Monster> monsters)
    {
        var monster = monsters[_random.Next(monsters.Count)];
        int monsterHp = monster.HitPoints;
        var portraitLines = monsterImageStore.Lines(monster.Id).Select(Terminal.Border).ToList();
        var battleLog = new List<string>();
        bool showIntro = true;

        while (monsterHp > 0 && state.HitPoints > 0)
        {
            ClearConsole();
            var left = BuildFightLeftColumn(
                monster,
                monsterHp,
                state,
                battleLog,
                showIntro,
                EquippedArmorRating(state),
                EquippedHelmetSlotAttackBonus(state));
            WriteTextAndRightImagePanel(left, portraitLines);

            var key = char.ToLowerInvariant(ReadInputChar());
            if (key == 'r')
            {
                Console.WriteLine();
                Console.WriteLine(Terminal.Muted("You slip away and put distance between you and the creature."));
                PauseForContinue();
                return;
            }

            if (key != 'a')
                continue;

            showIntro = false;

            bool playerActsFirst = state.Dexterity > monster.Dexterity;
            if (state.Dexterity == monster.Dexterity)
                playerActsFirst = _random.Next(2) == 0;

            if (playerActsFirst)
            {
                if (TryCompletePlayerAttack(state, monster, ref monsterHp, portraitLines, battleLog))
                    return;
                if (TryCompleteMonsterAttack(state, monster, monsterHp, portraitLines, battleLog))
                    return;
            }
            else
            {
                if (TryCompleteMonsterAttack(state, monster, monsterHp, portraitLines, battleLog))
                    return;
                if (TryCompletePlayerAttack(state, monster, ref monsterHp, portraitLines, battleLog))
                    return;
            }
        }
    }

    /// <summary>Player attacks; returns true if the fight ended (victory).</summary>
    private bool TryCompletePlayerAttack(
        GameState state,
        Monster monster,
        ref int monsterHp,
        IReadOnlyList<string> portraitLines,
        List<string> battleLog)
    {
        int weaponBonus = EquippedWeaponSlotAttackBonus(state) + EquippedHelmetSlotAttackBonus(state);
        AttackResolution res = CombatMath.ResolveAttack(
            _random,
            state.Strength,
            state.Dexterity,
            weaponBonus,
            monster.Dexterity);

        if (!res.Hit)
        {
            AppendBattleLog(battleLog, "You miss.");
            return false;
        }

        monsterHp -= res.Damage;
        AppendBattleLog(battleLog, $"You hit for {res.Damage} damage.");

        if (portraitLines.Count > 0 && !Console.IsOutputRedirected)
        {
            ClearConsole();
            var leftAfterStrike = BuildFightLeftColumn(
                monster,
                monsterHp,
                state,
                battleLog,
                showIntro: false,
                EquippedArmorRating(state),
                EquippedHelmetSlotAttackBonus(state));
            WriteTextAndRightImagePanel(leftAfterStrike, portraitLines);
        }

        if (monsterHp > 0)
            return false;

        int goldFound = _random.Next(3, 11);
        state.Gold += goldFound;
        ClearConsole();
        var victoryLeft = new List<string>
        {
            Terminal.Title("== Fight =="),
            "",
            Terminal.Ok($"The {monster.Name} falls."),
            "",
            Terminal.Muted($"You scrape up {goldFound} gold among the debris."),
        };
        if (_random.NextDouble() < 0.35)
        {
            state.Inventory.Add(KnownManipulativeIds.Apple);
            victoryLeft.Add(Terminal.Ok("The monster has dropped an apple."));
        }

        victoryLeft.Add("");
        WriteTextAndRightImagePanel(victoryLeft, portraitLines);
        Console.WriteLine();
        PauseForContinue();
        return true;
    }

    /// <summary>Monster attacks; returns true if the fight ended (defeat).</summary>
    private bool TryCompleteMonsterAttack(
        GameState state,
        Monster monster,
        int monsterHp,
        IReadOnlyList<string> portraitLines,
        List<string> battleLog)
    {
        AttackResolution res = CombatMath.ResolveAttack(
            _random,
            monster.Strength,
            monster.Dexterity,
            monster.AttackBonus,
            state.Dexterity);

        if (!res.Hit)
        {
            AppendBattleLog(battleLog, $"The {monster.Name} misses.");
            return false;
        }

        int rolled = res.Damage;
        int armorRating = EquippedArmorRating(state);
        int damage = CombatMath.ApplyArmorToIncomingDamage(rolled, armorRating);
        int absorbed = rolled - damage;

        state.HitPoints = Math.Max(0, state.HitPoints - damage);
        string hitLine = absorbed > 0
            ? $"The {monster.Name} hits you for {damage} damage ({absorbed} absorbed by your armor)."
            : $"The {monster.Name} hits you for {damage} damage.";
        AppendBattleLog(battleLog, hitLine);

        if (state.HitPoints > 0)
            return false;

        ClearConsole();
        var defeatLeft = new List<string> { Terminal.Title("== Fight =="), "" };
        defeatLeft.AddRange(battleLog.Select(Terminal.Muted));
        if (battleLog.Count > 0)
            defeatLeft.Add("");
        defeatLeft.Add(
            Terminal.Warn($"You: 0/{state.MaxHitPoints} HP    ")
            + Terminal.Combat($"{monster.Name}: {monsterHp} HP"));
        defeatLeft.Add("");
        WriteTextAndRightImagePanel(defeatLeft, portraitLines);
        Console.WriteLine();
        Console.WriteLine(Terminal.Combat("Everything goes dark…"));
        Console.WriteLine(Terminal.Muted("You wake later, bruised and alone. Someone dragged you clear."));
        state.HitPoints = Math.Max(1, state.MaxHitPoints / 4);
        Console.WriteLine();
        PauseForContinue();
        return true;
    }

    private const int FightBattleLogMaxLines = 12;

    private static List<string> BuildFightLeftColumn(
        Monster monster,
        int monsterHp,
        GameState state,
        List<string> battleLog,
        bool showIntro,
        int defenderArmorRating,
        int helmetSlotAttackBonus)
    {
        int w = AdventureLayout.LeftColumnWidth;
        var left = new List<string> { Terminal.Title("== Fight =="), "" };
        if (showIntro)
        {
            // Keep multi-word names from wrapping in the middle when possible; same word-wrap rules as room text.
            string nameForWrap = monster.Name.Replace(" ", "\u00A0", StringComparison.Ordinal);
            string plainIntro = $"Something stirs — a {nameForWrap}! {monster.Blurb}";
            foreach (string rawLine in WrapText(plainIntro, w))
            {
                string line = rawLine.Replace("\u00A0", " ", StringComparison.Ordinal);
                int idx = line.IndexOf(monster.Name, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    left.Add(
                        Terminal.Muted(line[..idx])
                        + Terminal.Combat(monster.Name)
                        + Terminal.Muted(line[(idx + monster.Name.Length)..]));
                    continue;
                }

                left.Add(Terminal.Muted(line));
            }

            left.Add("");
        }

        foreach (string entry in battleLog)
            left.AddRange(WrapText(entry, w).Select(Terminal.Muted));
        if (battleLog.Count > 0)
            left.Add("");
        left.Add(
            Terminal.Warn($"You: {state.HitPoints}/{state.MaxHitPoints} HP    ")
            + Terminal.Combat($"{monster.Name}: {monsterHp} HP"));
        if (defenderArmorRating > 0)
        {
            string armorPlain =
                $"Armor {defenderArmorRating}: each enemy hit loses up to {defenderArmorRating} damage (min. 1 per hit).";
            left.AddRange(WrapText(armorPlain, w).Select(Terminal.Muted));
        }

        if (helmetSlotAttackBonus != 0)
        {
            string sign = helmetSlotAttackBonus > 0 ? "+" : "";
            string helmetPlain =
                $"Attack {sign}{helmetSlotAttackBonus} from helmet — stacks with weapon on each hit you land.";
            left.AddRange(WrapText(helmetPlain, w).Select(Terminal.Muted));
        }

        left.Add("");
        left.Add(FormatMenuLine("(A)ttack", 'a', w));
        left.Add(FormatMenuLine("(R)un", 'r', w));
        left.Add("");
        return left;
    }

    private static void AppendBattleLog(List<string> battleLog, string line)
    {
        battleLog.Add(line);
        while (battleLog.Count > FightBattleLogMaxLines)
            battleLog.RemoveAt(0);
    }

    // Redirected stdin: Console.ReadKey is not supported — use ReadLine in those branches.
    private static char ReadInputChar()
    {
        if (Console.IsInputRedirected)
        {
            while (true)
            {
                var line = Console.ReadLine();
                if (line is null)
                    return 'x';
                if (line.Length > 0)
                    return line[0];
            }
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.KeyChar != '\0' && !char.IsWhiteSpace(key.KeyChar))
                return key.KeyChar;
        }
    }
}
