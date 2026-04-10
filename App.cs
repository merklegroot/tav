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
    IMonsterImageStore monsterImageStore) : IApp
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

    /// <summary>Screen as lines. <paramref name="forceWide"/> builds the 72-column map+text layout even if the window is narrow (for slide snapshots).</summary>
    private static List<string> BuildScreenLines(GameState state, IReadOnlyList<MenuItem> menuItems, bool forceWide = false)
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

    private static string BuildTitleBar(GameState state, int screenWidth)
    {
        string left = Terminal.Title("== Adventure Game ==");
        string right = Terminal.HpStatus(state.HitPoints, state.MaxHitPoints)
                       + Terminal.Muted("  Gold: ")
                       + Terminal.Accent(state.Gold.ToString());

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

        Console.WriteLine(Terminal.Muted($"Gold: {state.Gold}"));
    }

    private string FormatGroundStackLine(GroundItemStack stack)
    {
        string name = manipulativeStore.GetDisplayName(stack.Id);
        if (stack.Quantity <= 1)
            return name;

        return $"{name} (x{stack.Quantity})";
    }

    private void RunInventoryScreen(GameState state)
    {
        while (true)
        {
            ClearConsole();
            WritePlayerStatusHeader("== Inventory ==", state);
            Console.WriteLine();
            int n = state.Inventory.Count;
            if (n == 0)
            {
                Console.WriteLine(Terminal.Muted("  (nothing)"));
                Console.WriteLine();
                PauseForContinue();
                return;
            }

            for (int i = 0; i < n; i++)
            {
                int num = i + 1;
                char key = (char)('0' + num);
                Terminal.WriteMenuLine(
                    $"({num}) {manipulativeStore.GetDisplayName(state.Inventory[i])}",
                    key);
            }
            Console.WriteLine();
            Console.WriteLine(Terminal.EscBackHint());

            int? selectedIndex = ReadInventoryItemIndex(n);
            if (selectedIndex is null)
                return;

            if (RunSelectedInventoryItem(state, selectedIndex.Value))
                return;
        }
    }

    /// <summary>Hotkey lines use <see cref="Terminal.WriteMenuLine"/> like the main adventure menu.</summary>
    private static void WriteInventoryItemDetailMenu(bool canEat, bool offerEquip, bool offerUnequip)
    {
        if (canEat)
            Terminal.WriteMenuLine("(E)at", 'e');
        if (offerEquip)
            Terminal.WriteMenuLine("(E)quip", 'e');
        if (offerUnequip)
            Terminal.WriteMenuLine("(U)nequip", 'u');
        Terminal.WriteMenuLine("(D)rop", 'd');
        Console.WriteLine();
        Console.WriteLine(Terminal.EscBackHint());
    }

    /// <returns><see langword="true"/> if the player dropped an item (caller should leave Inventory).</returns>
    private bool RunSelectedInventoryItem(GameState state, int index)
    {
        while (true)
        {
            if (index < 0 || index >= state.Inventory.Count)
                return false;

            string id = state.Inventory[index];
            var def = manipulativeStore.Get(id);
            bool canEat = def is { IsEdible: true } && (def.ConsumeEffects?.HealthRestored ?? 0) > 0;
            bool canEquip = def is { IsEquippableWeapon: true };
            bool isEquipped = canEquip
                && state.EquippedWeaponId is not null
                && string.Equals(state.EquippedWeaponId, id, StringComparison.OrdinalIgnoreCase);

            ClearConsole();
            WritePlayerStatusHeader("== Inventory ==", state);
            Console.WriteLine();
            Console.WriteLine(Terminal.Accent($"Selected: {manipulativeStore.GetDisplayName(id)}"));
            if (canEat)
            {
                Console.WriteLine();
                manipulativeStore.WriteEdibleEffectDescription(def!, state);
            }

            Console.WriteLine();
            WriteInventoryItemDetailMenu(canEat, offerEquip: canEquip && !isEquipped, offerUnequip: isEquipped);

            var action = ReadInventoryItemDetailAction(canEat, offerEquip: canEquip && !isEquipped, offerUnequip: isEquipped);
            if (action == InventoryItemDetailAction.BackToList)
                return false;
            if (action == InventoryItemDetailAction.Drop)
            {
                var dropped = GameStateGroundOps.DropInventoryItemAt(state, index);
                Console.WriteLine();
                Console.WriteLine(
                    Terminal.Muted($"You drop the {manipulativeStore.GetDisplayName(dropped)}."));
                return true;
            }

            if (action == InventoryItemDetailAction.Equip)
            {
                state.EquippedWeaponId = def!.Id;
                Console.WriteLine();
                Console.WriteLine(
                    Terminal.Muted($"You equip the {def.Name.ToLowerInvariant()}."));
                PauseForContinue();
                continue;
            }

            if (action == InventoryItemDetailAction.Unequip)
            {
                state.EquippedWeaponId = null;
                Console.WriteLine();
                Console.WriteLine(Terminal.Muted("You put the weapon away."));
                PauseForContinue();
                continue;
            }

            var useResult = TryUseInventoryItem(state, index);
            Console.WriteLine();
            Console.WriteLine(Terminal.Muted(useResult.Message));
            PauseForContinue();
            if (useResult.Consumed)
                return false;
        }
    }

    /// <summary>Eats an edible item when the player chose (E)at. When consumed, the list shrinks at <paramref name="index"/>—caller should return to the list.</summary>
    private InventoryUseResult TryUseInventoryItem(GameState state, int index)
    {
        string id = state.Inventory[index];
        var def = manipulativeStore.Get(id);
        if (def is null)
            return new InventoryUseResult(false, "You can't think of a way to use that here.");

        if (!def.IsEdible)
            return new InventoryUseResult(false, "You can't think of a way to use that here.");

        int cap = def.ConsumeEffects?.HealthRestored ?? 0;
        if (cap <= 0)
            return new InventoryUseResult(false, "You can't think of a way to use that here.");

        int missing = state.MaxHitPoints - state.HitPoints;
        int heal = Math.Min(cap, missing);
        state.HitPoints += heal;
        state.Inventory.RemoveAt(index);
        string label = def.Name.ToLowerInvariant();
        if (heal <= 0)
        {
            return new InventoryUseResult(
                true,
                $"You eat the {label}. You're already at full health — satisfying, but no healing needed.");
        }

        if (heal >= cap)
        {
            return new InventoryUseResult(true, $"You eat the {label}. Sweet juice; warmth spreads through you.");
        }

        return new InventoryUseResult(true, $"You eat the {label} and recover {heal} HP.");
    }

    private readonly struct InventoryUseResult
    {
        public InventoryUseResult(bool consumed, string message)
        {
            Consumed = consumed;
            Message = message;
        }

        public bool Consumed { get; }
        public string Message { get; }
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
            Terminal.Muted($"You pick up the {manipulativeStore.GetDisplayName(taken)}."));
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
        Console.WriteLine(Terminal.Accent($"Gold: {state.Gold}"));
        Console.WriteLine(Terminal.Accent($"STR: {state.Strength}"));
        Console.WriteLine(Terminal.Accent($"DEX: {state.Dexterity}"));
        Console.WriteLine(
            Terminal.Muted(
                "Higher DEX helps you act first, dodge, and land cleaner hits in a fight."));
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
            return;
        }

        var weaponDef = manipulativeStore.Get(state.EquippedWeaponId);
        string weaponName = weaponDef?.Name ?? manipulativeStore.GetDisplayName(state.EquippedWeaponId);
        Console.WriteLine(Terminal.Accent($"  Weapon: {weaponName}"));

        if (weaponDef is null)
        {
            Console.WriteLine(Terminal.Muted("  No effect data for this item."));
            return;
        }

        bool wroteAny = false;
        if (weaponDef.WeaponDamageBonus is int bonus && bonus != 0)
        {
            string sign = bonus > 0 ? "+" : "";
            Console.WriteLine(
                Terminal.Muted($"  {sign}{bonus} damage on each strike in a fight."));
            wroteAny = true;
        }

        if (!wroteAny && weaponDef.IsEquippableWeapon)
            Console.WriteLine(Terminal.Muted("  No combat bonuses from this weapon."));
    }

    private void PrintHelp()
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Help =="));
        Console.WriteLine();
        Console.WriteLine(Terminal.Muted("Move with N, E, S, W (see compass)."));
        Console.WriteLine(
            Terminal.Muted(
                "(I)nventory: select an item. Edible ones list eating effects; then Eat, Drop, or Esc."));
        Console.WriteLine(Terminal.Muted("(G)round appears when something lies on the ground here."));
        Console.WriteLine(Terminal.Muted("(M)ap: overview of how the areas connect."));
        Console.WriteLine(Terminal.Muted("(F)ight: Attack or Run. Wins yield gold; sometimes a find."));
        Console.WriteLine();
        PauseForContinue();
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

    private int EquippedWeaponDamageBonus(GameState state)
    {
        if (state.EquippedWeaponId is null)
            return 0;
        return manipulativeStore.Get(state.EquippedWeaponId)?.WeaponDamageBonus ?? 0;
    }

    private void RunFightEncounter(GameState state, IReadOnlyList<Monster> monsters)
    {
        var monster = monsters[_random.Next(monsters.Count)];
        int monsterHp = monster.HitPoints;
        var portraitLines = monsterImageStore.Lines(monster.Id).ToList();
        var battleLog = new List<string>();
        bool showIntro = true;

        while (monsterHp > 0 && state.HitPoints > 0)
        {
            ClearConsole();
            var left = BuildFightLeftColumn(monster, monsterHp, state, battleLog, showIntro);
            RenderFightScreen(left, portraitLines);

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
        int weaponBonus = EquippedWeaponDamageBonus(state);
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

        var leftBeforeStrike = BuildFightLeftColumn(monster, monsterHp, state, battleLog, showIntro: false);
        monsterHp -= res.Damage;
        AnimatePlayerHit(leftBeforeStrike, portraitLines, res.Damage);
        AppendBattleLog(battleLog, $"You hit for {res.Damage} damage.");

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
            state.Inventory.Add(KnownManipulativeIds.BoneShard);
            victoryLeft.Add(Terminal.Ok("Something worth taking: a sharp bone shard."));
        }

        victoryLeft.Add("");
        RenderFightScreen(victoryLeft, portraitLines);
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
            monster.WeaponDamageBonus,
            state.Dexterity);

        if (!res.Hit)
        {
            AppendBattleLog(battleLog, $"The {monster.Name} misses.");
            return false;
        }

        state.HitPoints = Math.Max(0, state.HitPoints - res.Damage);
        AppendBattleLog(battleLog, $"The {monster.Name} hits you for {res.Damage} damage.");

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
        RenderFightScreen(defeatLeft, portraitLines);
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
        bool showIntro)
    {
        var left = new List<string> { Terminal.Title("== Fight =="), "" };
        if (showIntro)
        {
            left.Add(
                Terminal.Muted("Something stirs — a ")
                + Terminal.Combat(monster.Name)
                + Terminal.Muted($"! {monster.Blurb}"));
            left.Add("");
        }

        left.AddRange(battleLog.Select(Terminal.Muted));
        if (battleLog.Count > 0)
            left.Add("");
        left.Add(
            Terminal.Warn($"You: {state.HitPoints}/{state.MaxHitPoints} HP    ")
            + Terminal.Combat($"{monster.Name}: {monsterHp} HP"));
        left.Add(Terminal.Muted("(A)ttack  (R)un"));
        left.Add("");
        return left;
    }

    private static void AppendBattleLog(List<string> battleLog, string line)
    {
        battleLog.Add(line);
        while (battleLog.Count > FightBattleLogMaxLines)
            battleLog.RemoveAt(0);
    }

    /// <summary>Short multi-frame “impact” on the portrait: shake, flash, sparks, damage number.</summary>
    private void AnimatePlayerHit(
        IReadOnlyList<string> leftColumn,
        IReadOnlyList<string> portraitLines,
        int damage)
    {
        if (portraitLines.Count == 0 || Console.IsOutputRedirected)
            return;

        const int frameCount = 12;
        for (int frame = 0; frame < frameCount; frame++)
        {
            ClearConsole();
            var framePortrait = BuildPlayerHitFrame(portraitLines, frame, damage);
            RenderFightScreen(leftColumn, framePortrait);
            Thread.Sleep(frame is 4 or 5 ? 95 : frame >= 9 ? 140 : 55);
        }
    }

    private static List<string> BuildPlayerHitFrame(IReadOnlyList<string> basePortrait, int frame, int damage)
    {
        // Horizontal shake (pixels as spaces)
        int shake = frame switch
        {
            0 => 0,
            1 => 2,
            2 => 5,
            3 => 3,
            4 => 6,
            5 => 2,
            6 => 4,
            7 => 1,
            _ => 0,
        };
        string pad = new(' ', shake);
        var lines = basePortrait.Select(line => pad + line).ToList();

        if (lines.Count > 0 && frame <= 4)
        {
            // Strike motion toward the head (first line), symbols only
            string swing = frame switch
            {
                0 => "   ·-->>·",
                1 => "     ·->",
                2 => "      ×·",
                3 => "     ∿∿∿",
                _ => "",
            };
            lines[0] += swing;
        }

        // “Flash” — swap key glyphs for a beat
        if (frame is >= 3 and <= 7)
        {
            lines = lines
                .Select(l => l.Replace("@", "*", StringComparison.Ordinal).Replace("▼", "▽", StringComparison.Ordinal))
                .ToList();
        }

        // Falling sparks / debris under the art
        if (frame is >= 4 and <= 9)
        {
            string sparks = (frame & 1) == 0 ? "   · * · ▪ · * ·" : "   ▪ · · * · ▪ ·";
            lines.Add(sparks);
        }

        if (frame >= 8)
            lines.Add(Terminal.DamageNumber(damage));

        return lines;
    }

    /// <summary>Left column text with portrait on the right. Uses two columns whenever stdout is a TTY.</summary>
    private void RenderFightScreen(IReadOnlyList<string> leftLines, IReadOnlyList<string> portraitLines)
    {
        int minLeftWidth = AdventureLayout.LeftColumnWidth;

        if (portraitLines.Count == 0)
        {
            foreach (var line in leftLines)
                Console.WriteLine(line);
            return;
        }

        // Stacked layout only when redirected (e.g. piped); interactive always side-by-side so the image stays on the right.
        if (Console.IsOutputRedirected)
        {
            foreach (var line in leftLines)
                Console.WriteLine(line);
            Console.WriteLine();
            foreach (var line in portraitLines)
                Console.WriteLine(line);
            return;
        }

        int leftW = leftLines.Count == 0
            ? minLeftWidth
            : Math.Max(minLeftWidth, leftLines.Max(l => l.Length));

        int rows = Math.Max(leftLines.Count, portraitLines.Count);
        for (int i = 0; i < rows; i++)
        {
            string left = i < leftLines.Count ? leftLines[i] : "";
            string right = i < portraitLines.Count ? portraitLines[i] : "";
            Console.WriteLine(PadRightVisual(left, leftW) + new string(' ', AdventureLayout.Gap) + right);
        }
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
