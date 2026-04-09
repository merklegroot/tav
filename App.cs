using System.Text;
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

    /// <summary>Screen as lines. <paramref name="forceWide"/> builds the 72-column map+text layout even if the window is narrow (for slide snapshots).</summary>
    private static List<string> BuildScreenLines(GameState state, bool forceWide = false)
    {
        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        int screenWidth = AdventureLayout.ScreenWidth;

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
            string row = PadRightVisual(left, leftColWidth) + new string(' ', AdventureLayout.Gap) + panel[i];
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
            Terminal.Muted($"Coins: {state.Gold}"),
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

        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        int screenWidth = AdventureLayout.ScreenWidth;

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
                    Console.WriteLine(left + new string(' ', AdventureLayout.Gap) + right);
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

    private static void WritePlayerStatusHeader(string screenTitle, GameState state, bool includeCoins = true)
    {
        Console.WriteLine(Terminal.Title(screenTitle));
        Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
        if (includeCoins)
            Console.WriteLine(Terminal.Muted($"Coins: {state.Gold}"));
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
        while (true)
        {
            if (index < 0 || index >= state.Inventory.Count)
                return;

            string name = state.Inventory[index];
            ClearConsole();
            WritePlayerStatusHeader("== Inventory ==", state);
            Console.WriteLine();
            Console.WriteLine(Terminal.Accent($"Selected: {name}"));
            Console.WriteLine();
            if (!Console.IsInputRedirected)
                Console.WriteLine(Terminal.Muted("(U)se  (D)rop  (B)ack to list"));
            else
                Console.WriteLine(
                    Terminal.Muted("u / use / eat · d / drop · Enter = back to list"));

            var action = ReadInventoryItemDetailAction();
            if (action == InventoryItemDetailAction.BackToList)
                return;
            if (action == InventoryItemDetailAction.Drop)
            {
                var dropped = state.DropItemAt(index);
                Console.WriteLine();
                Console.WriteLine(Terminal.Muted($"You drop the {dropped}."));
                PauseForContinue();
                return;
            }

            TryUseInventoryItem(state, ref index, out bool consumed, out string message);
            Console.WriteLine();
            Console.WriteLine(Terminal.Muted(message));
            PauseForContinue();
            if (consumed)
                return;
        }
    }

    /// <summary>Applies use for known items. When the item is consumed, <paramref name="index"/> is unchanged but the list shrinks—caller should return to the list.</summary>
    private static void TryUseInventoryItem(GameState state, ref int index, out bool consumed, out string message)
    {
        consumed = false;
        message = "";
        string name = state.Inventory[index];
        if (name.Equals("Apple", StringComparison.OrdinalIgnoreCase))
        {
            if (state.HitPoints >= state.MaxHitPoints)
            {
                message = "You're not hungry right now.";
                return;
            }

            int heal = Math.Min(6, state.MaxHitPoints - state.HitPoints);
            state.HitPoints += heal;
            state.Inventory.RemoveAt(index);
            consumed = true;
            message = heal >= 6
                ? "You eat the apple. Sweet juice; warmth spreads through you."
                : $"You eat the apple and recover {heal} HP.";
            return;
        }

        if (name.Equals("Torch", StringComparison.OrdinalIgnoreCase))
        {
            message = "You lift the torch. The flame steadies; the shadows lean away.";
            return;
        }

        if (name.Equals("Bone shard", StringComparison.OrdinalIgnoreCase))
        {
            message = "The shard is jagged and cold. Not much use unless something needs cutting.";
            return;
        }

        message = "You can't think of a way to use that here.";
    }

    private enum InventoryItemDetailAction
    {
        BackToList,
        Drop,
        Use,
    }

    private static InventoryItemDetailAction ReadInventoryItemDetailAction()
    {
        if (!Console.IsInputRedirected)
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                char c = char.ToLowerInvariant(key.KeyChar);
                if (c == 'b')
                    return InventoryItemDetailAction.BackToList;
                if (c == 'd')
                    return InventoryItemDetailAction.Drop;
                if (c == 'u')
                    return InventoryItemDetailAction.Use;
            }
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null || string.IsNullOrWhiteSpace(line))
                return InventoryItemDetailAction.BackToList;
            string t = line.Trim().ToLowerInvariant();
            if (t is "d" or "drop")
                return InventoryItemDetailAction.Drop;
            if (t is "u" or "use" or "eat")
                return InventoryItemDetailAction.Use;
            Console.WriteLine(Terminal.Muted("Try u, d, or Enter to go back."));
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

    private void RunPickUpScreen(GameState state)
    {
        while (true)
        {
            int n = state.GroundItemsInCurrentRoom.Count;
            if (n == 0)
            {
                ClearConsole();
                Console.WriteLine(Terminal.Title("== Pick up =="));
                Console.WriteLine(Terminal.Muted("Nothing here to pick up."));
                Console.WriteLine();
                PauseForContinue();
                return;
            }

            ClearConsole();
            WritePlayerStatusHeader("== Pick up ==", state);
            Console.WriteLine();
            for (int i = 0; i < n; i++)
                Console.WriteLine(Terminal.Accent($"  {i + 1}. {state.GroundItemsInCurrentRoom[i]}"));
            Console.WriteLine();
            if (n <= 9 && !Console.IsInputRedirected)
                Console.WriteLine(Terminal.Muted("(1-9) Select item  (B)ack"));
            else
                Console.WriteLine(
                    Terminal.Muted($"Type item number (1-{n}) to select, or Enter to go back"));

            int? selectedIndex = ReadInventoryItemIndex(n);
            if (selectedIndex is null)
                return;

            RunSelectedGroundItem(state, selectedIndex.Value);
        }
    }

    private void RunSelectedGroundItem(GameState state, int index)
    {
        var ground = state.GroundItemsInCurrentRoom;
        if (index < 0 || index >= ground.Count)
            return;

        string name = ground[index];
        ClearConsole();
        WritePlayerStatusHeader("== Pick up ==", state, includeCoins: false);
        Console.WriteLine();
        Console.WriteLine(Terminal.Accent($"Selected: {name}"));
        Console.WriteLine();
        if (!Console.IsInputRedirected)
            Console.WriteLine(Terminal.Muted("(T)ake  (B)ack to list"));
        else
            Console.WriteLine(Terminal.Muted("Type t or take to pick up, or Enter to go back"));

        var action = ReadSelectedGroundItemAction();
        if (action == SelectedGroundItemAction.BackToList)
            return;

        var taken = state.PickUpGroundItemAt(index);
        if (taken is null)
            return;
        Console.WriteLine();
        Console.WriteLine(Terminal.Muted($"You pick up the {taken}."));
        PauseForContinue();
    }

    private enum SelectedGroundItemAction
    {
        BackToList,
        Take,
    }

    private static SelectedGroundItemAction ReadSelectedGroundItemAction()
    {
        if (!Console.IsInputRedirected)
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                char c = char.ToLowerInvariant(key.KeyChar);
                if (c == 'b')
                    return SelectedGroundItemAction.BackToList;
                if (c == 't')
                    return SelectedGroundItemAction.Take;
            }
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null || string.IsNullOrWhiteSpace(line))
                return SelectedGroundItemAction.BackToList;
            string t = line.Trim().ToLowerInvariant();
            if (t is "t" or "take")
                return SelectedGroundItemAction.Take;
            Console.WriteLine(Terminal.Muted("Try t or take, or Enter to go back."));
        }
    }

    private void PrintCharacter(GameState state)
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Character =="));
        Console.WriteLine();
        Console.WriteLine(Terminal.HpStatus(state.HitPoints, state.MaxHitPoints));
        Console.WriteLine(Terminal.Accent($"Coins: {state.Gold}"));
        Console.WriteLine(Terminal.Accent($"STR: {state.Strength}"));
        Console.WriteLine(Terminal.Accent($"DEX: {state.Dexterity}"));
        Console.WriteLine(Terminal.Muted("Higher DEX softens enemy hits in a fight."));
        Console.WriteLine();
        PauseForContinue();
    }

    private void PrintHelp()
    {
        ClearConsole();
        Console.WriteLine(Terminal.Title("== Help =="));
        Console.WriteLine();
        Console.WriteLine(Terminal.Muted("Move with compass keys shown in the menu."));
        Console.WriteLine(Terminal.Muted("(I)nventory: select an item, then Use, Drop, or Back."));
        Console.WriteLine(Terminal.Muted("(P)ick up appears when something lies on the ground here."));
        Console.WriteLine(Terminal.Muted("(F)ight: Attack or Flee. Wins yield coins; sometimes a find."));
        Console.WriteLine(Terminal.Muted("Apples can be eaten from the inventory (Use)."));
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
                    var oldPanel = BuildRoomPanel(state.CurrentRoom, AdventureLayout.MapPanelOuterWidth);
                    navigateTo(destRoom);
                    AnimateRoomSlide(
                        oldPanel,
                        BuildRoomPanel(state.CurrentRoom, AdventureLayout.MapPanelOuterWidth),
                        state,
                        dir);
                }));
        }

        if (state.GroundItemsInCurrentRoom.Count > 0)
            items.Add(new MenuItem("(P)ick up", 'p', () => RunPickUpScreen(state)));

        items.Add(new MenuItem("(I)nventory", 'i', () => RunInventoryScreen(state)));
        items.Add(new MenuItem("(C)haracter", 'c', () => PrintCharacter(state)));
        items.Add(new MenuItem("(F)ight", 'f', () => RunFightEncounter(state, monsters)));
        items.Add(new MenuItem("(H)elp", 'h', () => PrintHelp()));
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
                int coins = _random.Next(3, 11);
                state.Gold += coins;
                ClearConsole();
                var victoryLeft = new List<string>
                {
                    Terminal.Title("== Fight =="),
                    "",
                    Terminal.Ok($"The {monster.Name} falls."),
                    "",
                    Terminal.Muted($"You scrape up {coins} coins among the debris."),
                };
                if (_random.NextDouble() < 0.35)
                {
                    state.Inventory.Add("Bone shard");
                    victoryLeft.Add(Terminal.Ok("Something worth taking: a sharp bone shard."));
                }
                victoryLeft.Add("");
                RenderFightScreen(victoryLeft, portraitLines);
                Console.WriteLine();
                PauseForContinue();
                return;
            }

            int rawDamage = _random.Next(1, monster.MaxDamage + 1);
            int mitigated = Math.Max(1, rawDamage - state.Dexterity / 8);
            state.HitPoints = Math.Max(0, state.HitPoints - mitigated);
            AppendBattleLog(
                battleLog,
                mitigated < rawDamage
                    ? $"It strikes for {mitigated} damage (you slip part of the blow)."
                    : $"It strikes back for {mitigated} damage.");

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
}
