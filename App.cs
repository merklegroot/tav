using System.Text;
using System.Text.Json;
using System.Threading;

namespace Tav;

internal sealed class App : IApp
{
    private readonly Random _random = new();

    public void Run()
    {
        var rooms = RoomStore.LoadAll();
        var roomsById = rooms.ToDictionary(r => r.Id.ToLowerInvariant());
        var monsters = MonsterStore.LoadAll();

        var state = new GameState(roomsById["castle_entrance"]);

        while (!state.ShouldExit)
        {
            PrintScreen(state);

            var menuItems = BuildMenuItems(
                state.CurrentRoom,
                roomsById,
                monsters,
                state,
                r => state.CurrentRoom = r,
                () => state.ShouldExit = true);

            foreach (var item in menuItems)
                Terminal.WriteMenuLine(item.Text, item.Key);
            Console.WriteLine();

            var input = ReadInputChar();
            var normalized = char.ToLowerInvariant(input);
            menuItems.FirstOrDefault(m => m.Key == normalized)?.Action.Invoke();
        }
    }

    private void ClearConsole()
    {
        if (Console.IsOutputRedirected == false)
        {
            Console.Write("\u001b[H\u001b[2J");
            if (Terminal.UseAnsi)
                Console.Write(Terminal.Reset);
            Console.Out.Flush();
        }
    }

    private void PrintScreen(GameState state)
    {
        ClearConsole();
        foreach (var line in BuildScreenLines(state))
            Console.WriteLine(line);
    }

    /// <summary>Screen as lines. <paramref name="forceWide"/> builds the 72-column map+text layout even if the window is narrow (for slide snapshots).</summary>
    private static List<string> BuildScreenLines(GameState state, bool forceWide = false)
    {
        const int gap = 2;
        const int leftColWidth = 46;
        const int panelOuter = 24;
        int screenWidth = leftColWidth + gap + panelOuter;

        var leftLines = BuildLeftColumnLines(state, leftColWidth);

        var panel = BuildRoomPanel(state.CurrentRoom, panelOuter);

        int minWidth = screenWidth;
        if (!forceWide && !CanUseWideLayout(minWidth))
        {
            var stacked = new List<string>();
            foreach (var line in leftLines)
                stacked.Add(line);
            stacked.Add("");
            foreach (var line in panel)
                stacked.Add(line);
            stacked.Add("");
            return stacked;
        }

        var lines = new List<string>();
        for (int i = 0; i < panel.Length; i++)
        {
            string left = i < leftLines.Count ? leftLines[i] : "";
            string row = PadRightVisual(left, leftColWidth) + new string(' ', gap) + panel[i];
            lines.Add(PadRightVisual(row, screenWidth));
        }

        for (int i = panel.Length; i < leftLines.Count; i++)
            lines.Add(PadRightVisual(leftLines[i], screenWidth));

        lines.Add(new string(' ', screenWidth));
        return lines;
    }

    private static List<string> BuildLeftColumnLines(GameState state, int leftColWidth)
    {
        var leftLines = new List<string>
        {
            Terminal.Title("== Adventure Game =="),
            Terminal.HpStatus(state.HitPoints, state.MaxHitPoints),
            "",
            Terminal.Accent(Truncate(state.CurrentRoom.Name, leftColWidth)),
        };
        leftLines.AddRange(WrapText(state.CurrentRoom.Description, leftColWidth).Select(Terminal.Muted));
        if (state.GroundItemsInCurrentRoom.Count > 0)
        {
            leftLines.Add("");
            leftLines.Add(
                Terminal.Muted("On the ground: ")
                + string.Join(", ", state.GroundItemsInCurrentRoom));
        }
        return leftLines;
    }

    /// <summary>
    /// Slides only the map panel (right column); left column shows the new room’s text immediately.
    /// </summary>
    private void AnimateRoomSlide(string[] oldPanel, string[] newPanel, GameState afterNavigate, char direction)
    {
        if (Console.IsOutputRedirected)
            return;

        const int gap = 2;
        const int leftColWidth = 46;
        const int panelOuter = 24;
        int screenWidth = leftColWidth + gap + panelOuter;

        if (!CanUseWideLayout(screenWidth))
            return;

        var newLeft = BuildLeftColumnLines(afterNavigate, leftColWidth);
        int panelRows = oldPanel.Length;
        int H = Math.Max(newLeft.Count, panelRows);

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

            if (direction is 'e' or 'w')
            {
                // East: [old|new], window slides right (offset ↑) — old leaves left, new enters from the right.
                // West: [new|old], window slides left (offset ↓) — old leaves right, new enters from the left.
                bool east = direction == 'e';
                int offset = east
                    ? (int)Math.Round(t * panelOuter)
                    : (int)Math.Round((1 - t) * panelOuter);
                for (int r = 0; r < H; r++)
                {
                    if (r >= panelRows)
                    {
                        if (r < newLeft.Count)
                            Console.WriteLine(PadRightVisual(newLeft[r], screenWidth));
                        continue;
                    }

                    string left = r < newLeft.Count ? PadRightVisual(newLeft[r], leftColWidth) : new string(' ', leftColWidth);
                    string combined = east
                        ? oldRowsPlain[r] + newRowsPlain[r]
                        : newRowsPlain[r] + oldRowsPlain[r];
                    string rightPlain = combined.Substring(offset, panelOuter);
                    string right = Terminal.Border(rightPlain);
                    Console.WriteLine(left + new string(' ', gap) + right);
                }
            }
            else
            {
                // North: new map above old in the strip; scroll down. South: old above new; scroll down.
                var strip = direction == 'n'
                    ? BuildVerticalStrip(newRows, oldRows)
                    : BuildVerticalStrip(oldRows, newRows);
                int scroll = direction == 'n'
                    ? (int)Math.Round((1 - t) * panelRows)
                    : (int)Math.Round(t * panelRows);
                scroll = Math.Clamp(scroll, 0, panelRows);

                for (int r = 0; r < H; r++)
                {
                    if (r >= panelRows)
                    {
                        if (r < newLeft.Count)
                            Console.WriteLine(PadRightVisual(newLeft[r], screenWidth));
                        continue;
                    }

                    string left = r < newLeft.Count ? PadRightVisual(newLeft[r], leftColWidth) : new string(' ', leftColWidth);
                    int idx = scroll + r;
                    string right = strip[idx];
                    Console.WriteLine(left + new string(' ', gap) + right);
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
                    current.Append(word);
                else if (current.Length + 1 + word.Length <= width)
                    current.Append(' ').Append(word);
                else
                {
                    lines.Add(current.ToString());
                    current.Clear().Append(word);
                }
            }

            if (current.Length > 0)
                lines.Add(current.ToString());
            else if (words.Length == 0)
                lines.Add("");
        }

        if (lines.Count == 0)
            lines.Add("");
        return lines;
    }

    private static bool HasExit(Room room, char dir) =>
        room.Exits?.ContainsKey(dir.ToString()) == true;

    /// <summary>Horizontal wall; optional centered " for north (top) or south (bottom) exit.</summary>
    private static string BuildHorizontalWall(
        char leftCorner,
        char rightCorner,
        int innerWidth,
        bool doorQuoteInWall)
    {
        if (!doorQuoteInWall || innerWidth < 1)
            return leftCorner + new string('─', innerWidth) + rightCorner;
        int rest = innerWidth - 1;
        int leftDashes = rest / 2;
        int rightDashes = rest - leftDashes;
        return leftCorner
            + new string('─', leftDashes)
            + '"'
            + new string('─', rightDashes)
            + rightCorner;
    }

    /// <summary>Inner rows: │ walls; = only on the title row for west/east.</summary>
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

    private static string[] BuildRoomPanel(Room room, int outerWidth)
    {
        int inner = outerWidth - 2;

        bool n = HasExit(room, 'n');
        bool e = HasExit(room, 'e');
        bool s = HasExit(room, 's');
        bool w = HasExit(room, 'w');

        string title = Truncate(room.Name, Math.Max(1, inner));

        string top = BuildHorizontalWall('┌', '┐', inner, doorQuoteInWall: n);
        string bottom = BuildHorizontalWall('└', '┘', inner, doorQuoteInWall: s);

        string blankInner = new string(' ', inner);
        string blank = BuildSideWallLine(blankInner, inner, w, e, useWestEastMarkers: false);
        string titleRow = BuildSideWallLine(PadInner(title, inner), inner, w, e, useWestEastMarkers: true);
        string playerRow = BuildSideWallLine(PadInner("●", inner), inner, w, e, useWestEastMarkers: false);

        return
        [
            Terminal.Border(top),
            Terminal.Border(blank),
            Terminal.Border(blank),
            Terminal.Border(titleRow),
            Terminal.Border(playerRow),
            Terminal.Border(blank),
            Terminal.Border(blank),
            Terminal.Border(bottom),
        ];
    }

    private static string PadInner(string content, int innerWidth)
    {
        if (content.Length > innerWidth)
            return content[..innerWidth];
        int pad = innerWidth - content.Length;
        int left = pad / 2;
        return new string(' ', left) + content + new string(' ', pad - left);
    }

    private void PauseForContinue()
    {
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(intercept: true);
        }
        else
        {
            Console.WriteLine("(press Enter to continue)");
            _ = Console.ReadLine();
        }
    }

    private void RunInventoryScreen(GameState state)
    {
        while (true)
        {
            ClearConsole();
            Console.WriteLine(Terminal.Title("== Inventory =="));
            Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
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
                Console.WriteLine(Terminal.Accent($"  {i + 1}. {state.Inventory[i]}"));
            Console.WriteLine();
            if (n <= 9 && !Console.IsInputRedirected)
                Console.WriteLine(Terminal.Muted("(1-9) Select item  (B)ack"));
            else
                Console.WriteLine(
                    Terminal.Muted($"Type item number (1-{n}) to select, or Enter to go back"));

            int? selectedIndex = ReadInventoryItemIndex(n);
            if (selectedIndex is null)
                return;

            RunSelectedInventoryItem(state, selectedIndex.Value);
        }
    }

    private void RunSelectedInventoryItem(GameState state, int index)
    {
        if (index < 0 || index >= state.Inventory.Count)
            return;

        string name = state.Inventory[index];
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Inventory =="));
        Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
        Console.WriteLine();
        Console.WriteLine(Terminal.Accent($"Selected: {name}"));
        Console.WriteLine();
        if (!Console.IsInputRedirected)
            Console.WriteLine(Terminal.Muted("(D)rop  (B)ack to list"));
        else
            Console.WriteLine(Terminal.Muted("Type d or drop to drop, or Enter to go back"));

        var action = ReadSelectedInventoryItemAction();
        if (action == SelectedInventoryItemAction.BackToList)
            return;

        var dropped = state.DropItemAt(index);
        Console.WriteLine();
        Console.WriteLine(Terminal.Muted($"You drop the {dropped}."));
        PauseForContinue();
    }

    private enum SelectedInventoryItemAction
    {
        BackToList,
        Drop,
    }

    private static SelectedInventoryItemAction ReadSelectedInventoryItemAction()
    {
        if (!Console.IsInputRedirected)
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                char c = char.ToLowerInvariant(key.KeyChar);
                if (c == 'b')
                    return SelectedInventoryItemAction.BackToList;
                if (c == 'd')
                    return SelectedInventoryItemAction.Drop;
            }
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null || string.IsNullOrWhiteSpace(line))
                return SelectedInventoryItemAction.BackToList;
            string t = line.Trim().ToLowerInvariant();
            if (t is "d" or "drop")
                return SelectedInventoryItemAction.Drop;
            Console.WriteLine(Terminal.Muted("Try d or drop, or Enter to go back."));
        }
    }

    /// <summary>Returns 0-based inventory index to act on, or null to leave inventory.</summary>
    private int? ReadInventoryItemIndex(int itemCount)
    {
        if (itemCount <= 0)
            return null;

        if (!Console.IsInputRedirected)
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                char c = char.ToLowerInvariant(key.KeyChar);
                if (c == 'b')
                    return null;
                if (c >= '1' && c <= '0' + itemCount)
                    return c - '1';
            }
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null || string.IsNullOrWhiteSpace(line))
                return null;
            if (int.TryParse(line.Trim(), out int num) && num >= 1 && num <= itemCount)
                return num - 1;
            Console.WriteLine(Terminal.Muted("Try a number from the list, or Enter to go back."));
        }
    }

    private void PrintCharacter(GameState state)
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Character =="));
        Console.WriteLine();
        Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
        Console.WriteLine(Terminal.Accent($"STR: {state.Strength}"));
        Console.WriteLine(Terminal.Accent($"DEX: {state.Dexterity}"));
        Console.WriteLine();
        PauseForContinue();
    }

    private List<MenuItem> BuildMenuItems(
        Room currentRoom,
        IReadOnlyDictionary<string, Room> roomsById,
        IReadOnlyList<Monster> monsters,
        GameState state,
        Action<Room> navigateTo,
        Action exit)
    {
        var items = new List<MenuItem>();
        foreach (var dir in "nesw")
        {
            if (currentRoom.Exits is null ||
                !currentRoom.Exits.TryGetValue(dir.ToString(), out var destId))
                continue;
            if (!roomsById.TryGetValue(destId.ToLowerInvariant(), out var destRoom))
                continue;
            items.Add(new MenuItem(
                FormatDirectionOption(dir, destRoom.Name),
                dir,
                () =>
                {
                    var oldPanel = BuildRoomPanel(state.CurrentRoom, 24);
                    navigateTo(destRoom);
                    AnimateRoomSlide(oldPanel, BuildRoomPanel(state.CurrentRoom, 24), state, dir);
                }));
        }

        items.Add(new MenuItem("(I)nventory", 'i', () => RunInventoryScreen(state)));
        items.Add(new MenuItem("(C)haracter", 'c', () => PrintCharacter(state)));
        items.Add(new MenuItem("(F)ight", 'f', () => RunFightEncounter(state, monsters)));
        items.Add(new MenuItem("e(X)it", 'x', exit));
        return items;
    }

    private void RunFightEncounter(GameState state, IReadOnlyList<Monster> monsters)
    {
        var monster = monsters[_random.Next(monsters.Count)];
        int monsterHp = monster.HitPoints;
        var portraitLines = MonsterImageStore.Lines(monster.Id).ToList();
        var battleLog = new List<string>();
        bool showIntro = true;

        while (monsterHp > 0 && state.HitPoints > 0)
        {
            ClearConsole();
            var left = BuildFightLeftColumn(monster, monsterHp, state, battleLog, showIntro);
            RenderFightScreen(left, portraitLines);

            var key = char.ToLowerInvariant(ReadInputChar());
            if (key == 'f')
            {
                Console.WriteLine();
                Console.WriteLine(Terminal.Muted("You slip away and put distance between you and the creature."));
                PauseForContinue();
                return;
            }

            if (key != 'a')
                continue;

            showIntro = false;

            var leftBeforeStrike = BuildFightLeftColumn(monster, monsterHp, state, battleLog, showIntro);
            int playerDamage = _random.Next(1, 4) + state.Strength / 6;
            monsterHp -= playerDamage;

            AnimatePlayerHit(leftBeforeStrike, portraitLines, playerDamage);

            AppendBattleLog(battleLog, $"You strike for {playerDamage} damage.");

            if (monsterHp <= 0)
            {
                ClearConsole();
                var victoryLeft = new List<string>
                {
                    Terminal.Title("== Fight =="),
                    "",
                    Terminal.Ok($"The {monster.Name} falls."),
                    "",
                };
                RenderFightScreen(victoryLeft, portraitLines);
                Console.WriteLine();
                PauseForContinue();
                return;
            }

            int enemyDamage = _random.Next(1, monster.MaxDamage + 1);
            state.HitPoints = Math.Max(0, state.HitPoints - enemyDamage);
            AppendBattleLog(battleLog, $"It strikes back for {enemyDamage} damage.");

            if (state.HitPoints <= 0)
            {
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
                return;
            }
        }
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
        left.Add(Terminal.Muted("(A)ttack  (F)lee"));
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
        const int gap = 2;
        const int minLeftWidth = 46;

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
            Console.WriteLine(PadRightVisual(left, leftW) + new string(' ', gap) + right);
        }
    }

    private static string FormatDirectionOption(char direction, string destinationName) =>
        direction switch
        {
            'n' => $"(N)orth - {destinationName}",
            'e' => $"(E)ast - {destinationName}",
            's' => $"(S)outh - {destinationName}",
            'w' => $"(W)est - {destinationName}",
            _ => $"({char.ToUpperInvariant(direction)}) - {destinationName}",
        };

    private static char ReadInputChar()
    {
        if (!Console.IsInputRedirected)
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.KeyChar != '\0' && !char.IsWhiteSpace(key.KeyChar))
                    return key.KeyChar;
            }
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null)
                return 'x';
            if (line.Length > 0)
                return line[0];
        }
    }

    private sealed class GameState
    {
        public Room CurrentRoom { get; set; }
        public bool ShouldExit { get; set; }
        public int HitPoints { get; set; }
        public int MaxHitPoints { get; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public List<string> Inventory { get; } = ["Torch", "Apple"];

        /// <summary>Items on the floor, keyed by lowercase room id.</summary>
        public Dictionary<string, List<string>> GroundItemsByRoomId { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> GroundItemsInCurrentRoom
        {
            get
            {
                var id = CurrentRoom.Id.ToLowerInvariant();
                return GroundItemsByRoomId.TryGetValue(id, out var list)
                    ? list
                    : Array.Empty<string>();
            }
        }

        public GameState(Room start)
        {
            CurrentRoom = start;
            MaxHitPoints = 20;
            HitPoints = MaxHitPoints;
            Strength = 12;
            Dexterity = 14;
        }

        /// <summary>Removes the item from inventory and places it in the current room. Returns the item name.</summary>
        public string DropItemAt(int index)
        {
            string name = Inventory[index];
            Inventory.RemoveAt(index);
            var roomId = CurrentRoom.Id.ToLowerInvariant();
            if (!GroundItemsByRoomId.TryGetValue(roomId, out var ground))
            {
                ground = [];
                GroundItemsByRoomId[roomId] = ground;
            }
            ground.Add(name);
            return name;
        }
    }
}

internal sealed record Room(string Id, string Name, string Description, Dictionary<string, string>? Exits);

internal sealed record Monster(string Id, string Name, string Blurb, int HitPoints, int MaxDamage);

internal sealed record MenuItem(string Text, char Key, Action Action);

internal static class RoomStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<Room> LoadAll()
    {
        var assembly = typeof(RoomStore).Assembly;
        var name = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith("rooms.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Missing embedded resource rooms.json");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing embedded resource rooms.json");
        return JsonSerializer.Deserialize<List<Room>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("rooms.json was empty or invalid");
    }
}

internal static class MonsterStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<Monster> LoadAll()
    {
        var assembly = typeof(MonsterStore).Assembly;
        var name = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith("monsters.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Missing embedded resource monsters.json");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing embedded resource monsters.json");
        return JsonSerializer.Deserialize<List<Monster>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("monsters.json was empty or invalid");
    }
}

internal static class MonsterImageStore
{
    /// <summary>Lines of the portrait file, or empty when missing.</summary>
    public static IEnumerable<string> Lines(string monsterId)
    {
        var assembly = typeof(MonsterImageStore).Assembly;
        var suffix = $"{monsterId}.img.txt";
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            yield break;

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
            yield break;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }
}
