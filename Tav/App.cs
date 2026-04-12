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
    IManipulativeImageStore manipulativeImageStore,
    ITerminal terminal,
    IConsoleWrapper console) : IApp
{
    private readonly Random _random = new();

    public void Run()
    {
        var rooms = roomStore.LoadAll();
        var roomsById = rooms.ToDictionary(r => r.Id.ToLowerInvariant());
        var monsters = monsterStore.LoadAll();
        bool snapshotExitAfterFirstFrame = string.Equals(
            Environment.GetEnvironmentVariable("TAV_SNAPSHOT"),
            "1",
            StringComparison.Ordinal);

        while (!state.ShouldExit)
        {
            var menuItems = BuildMenuItems(monsters, state, () => state.ShouldExit = true);

            PrintScreen(state, menuItems);

            if (snapshotExitAfterFirstFrame)
            {
                state.ShouldExit = true;
                console.FlushOutput();
                continue;
            }

            var input = ReadInputChar();
            var normalized = char.ToLowerInvariant(input);
            if (TryParseCompassMovementKey(normalized, out char compassExit) &&
                TryNavigateCompass(compassExit, roomsById, state))
                continue;

            menuItems.FirstOrDefault(m => m.Key == normalized)?.Action.Invoke();
        }
    }

    private void ClearConsole()
    {
        if (console.IsOutputRedirected)
            return;

        console.Write("\u001b[H\u001b[2J");
        if (terminal.UseAnsi)
            console.Write(terminal.Reset);
        console.FlushOutput();
    }

    /// <summary>
    /// Full-width title bar plus blank line(s) plus wide or stacked two-column body (same rules as legacy
    /// <c>WriteFullWidthTitleBar</c> + <c>WriteTextAndRightImagePanel</c>).
    /// </summary>
    private void PresentTitleAndTwoColumnPanel(
        string title,
        GameState state,
        IReadOnlyList<string> leftLines,
        IReadOnlyList<string> portraitLines,
        int rightPanelTopOffset,
        int blankLinesAfterTitle,
        bool trailingBlankLine)
    {
        int panelOuter = AdventureLayout.PortraitCardOuterWidth;
        string[] panel = portraitLines.Count > 0
            ? AdventureLayout.BuildPortraitPanelCells(terminal, portraitLines, panelOuter)
            : [];

        bool wide = !console.IsOutputRedirected
                     && AdventureLayout.CanUseWideLayout(console, AdventureLayout.ScreenWidth);
        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int screenWidth = AdventureLayout.ScreenWidth;
        int arm = EquippedArmorRating(state);

        int bodyRows;
        if (wide)
        {
            int imageH = panel.Length;
            int leftCount = leftLines.Count;
            bodyRows = Math.Max(leftCount, rightPanelTopOffset + imageH);
        }
        else
            bodyRows = AdventureLayout.CountStackedContentRows(leftLines.Count, panel.Length, rightPanelTopOffset);

        int rowCount = 1 + blankLinesAfterTitle + bodyRows + (trailingBlankLine ? 1 : 0);
        var buffer = ScreenBuffer.ForGameLayout(rowCount, terminal, console);
        buffer.DrawText(0, 0, AdventureLayout.BuildTitleBar(terminal, title, state, screenWidth, arm));
        for (int b = 0; b < blankLinesAfterTitle; b++)
            buffer.DrawText(0, 1 + b, "");

        int bodyY = 1 + blankLinesAfterTitle;
        if (wide)
        {
            AdventureLayout.DrawTwoColumnRegion(
                terminal,
                buffer,
                bodyY,
                leftColWidth,
                AdventureLayout.Gap,
                panelOuter,
                screenWidth,
                leftLines,
                panel,
                rightPanelTopOffset);
        }
        else
            AdventureLayout.DrawStackedTwoColumnFallback(buffer, bodyY, leftLines, panel, rightPanelTopOffset);

        if (trailingBlankLine)
            buffer.DrawText(0, bodyY + bodyRows, "");

        buffer.RenderToConsole();
    }

    private void PrintScreen(GameState state, IReadOnlyList<MenuItem> menuItems)
    {
        int equipped = EquippedArmorRating(state);
        int leftCol = AdventureLayout.LeftColumnWidth;
        var leftLines = AdventureLayout.BuildMainViewLeftPanelLines(terminal, state, menuItems, leftCol);
        bool showCompass = menuItems.Count > 0;
        var panel = showCompass
            ? AdventureLayout.BuildMainViewRightPanel(terminal, state.CurrentRoom, AdventureLayout.MapPanelOuterWidth, true)
            : AdventureLayout.BuildRoomPanel(terminal, state.CurrentRoom, AdventureLayout.MapPanelOuterWidth, true);

        int rowCount;
        if (AdventureLayout.CanUseWideLayout(console, AdventureLayout.ScreenWidth))
        {
            int h = Math.Max(leftLines.Count, AdventureLayout.MainViewRightPanelTopOffset + panel.Length);
            rowCount = 3 + h;
        }
        else
        {
            rowCount = 4 + leftLines.Count + panel.Length;
        }

        var buffer = ScreenBuffer.ForGameLayout(rowCount, terminal, console);
        AdventureLayout.DrawInto(terminal, console, buffer, state, menuItems, equipped);
        buffer.RenderToConsole();
    }

    private void PauseForContinue()
    {
        if (console.IsInputRedirected)
        {
            console.WriteLine("(press Enter to continue)");
            _ = console.ReadLine();
            return;
        }

        console.WriteLine("Press any key to continue...");
        console.ReadKey(intercept: true);
    }

    private void PrintVictoryScreen(GameState state)
    {
        var left = new List<string>
        {
            terminal.Ok("The crown fits cold and sure."),
            terminal.Ok(
                "Banners you never hung stir in a wind that has waited ages for an heir."),
            "",
            terminal.Muted(
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

            left.Add(terminal.Muted(string.Join(" · ", parts) + " — regalia and reach."));
        }

        left.Add("");
        left.Add(
            terminal.Muted(
                $"You stand crowned. Level {PlayerLeveling.GetLevelFromTotalExperience(state.Experience)}, HP {state.HitPoints}/{state.MaxHitPoints}, gold {state.Gold}, XP {state.Experience}. The story ends here."));

        string? helmetId = state.EquippedHelmetId;
        var def = helmetId is not null ? manipulativeStore.Get(helmetId) : null;
        List<string> portrait = [];
        if (def?.Image is { Length: > 0 } stem)
            portrait.AddRange(manipulativeImageStore.Lines(stem).Select(terminal.PortraitArt));

        PresentTitleAndTwoColumnPanel(
            "== Victory ==",
            state,
            left,
            portrait,
            rightPanelTopOffset: 1,
            blankLinesAfterTitle: 1,
            trailingBlankLine: true);
    }

    /// <summary>Word-wraps one description line to the adventure left column width (plain-word wrap; re-applies muted style).</summary>
    private List<string> WrapInventoryDescriptionLineToColumn(string line, int columnWidth)
    {
        if (terminal.VisibleLength(line) <= columnWidth)
            return [line];

        string plain = terminal.StripAnsi(line);
        return AdventureLayout.WrapText(plain, columnWidth).Select(terminal.Muted).ToList();
    }

    /// <summary>
    /// Slides only the room box (five lines); compass is omitted here — same as when the main menu is hidden.
    /// Inserts one hallway row (N/S) or one E/W connector column (a single space between rooms; <c>-</c> one row above and below center) for the duration of the animation.
    /// Left column uses description only (no menu), matching that layout.
    /// </summary>
    private void AnimateRoomSlide(string[] oldPanel, string[] newPanel, GameState afterNavigate, char direction)
    {
        if (console.IsOutputRedirected)
            return;

        int leftColWidth = AdventureLayout.LeftColumnWidth;
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        int screenWidth = AdventureLayout.ScreenWidth;

        if (!AdventureLayout.CanUseWideLayout(console, screenWidth))
            return;

        var newLeft = AdventureLayout.BuildLeftColumnLines(terminal, afterNavigate, leftColWidth);
        int panelRows = oldPanel.Length;
        const int rightPanelTopOffset = 1; // fixed: match main view
        int H = Math.Max(newLeft.Count, rightPanelTopOffset + panelRows);

        string titleBar = AdventureLayout.BuildTitleBar(
            terminal,
            "== Adventure Game ==",
            afterNavigate,
            screenWidth,
            EquippedArmorRating(afterNavigate));
        string blankRight = new string(' ', panelOuter);

        var oldRows = PadPanelRows(oldPanel, panelOuter);
        var newRows = PadPanelRows(newPanel, panelOuter);
        // East/west sliding uses Substring on a concatenation; indices must match visible columns, not raw bytes (ANSI breaks alignment).
        var oldRowsPlain = PadPanelRows(oldPanel.Select(terminal.StripAnsi).ToArray(), panelOuter);
        var newRowsPlain = PadPanelRows(newPanel.Select(terminal.StripAnsi).ToArray(), panelOuter);

        int rowCount = 2 + H;
        const int frames = 22;
        bool northSouth = direction is not 'e' and not 'w';
        int frameDelayMs = northSouth ? 22 : 28; // north/south ~20% shorter per frame than previous 28ms
        int verticalStripLength = panelRows + 1 + panelRows;
        int maxVerticalScroll = verticalStripLength - panelRows;
        const int eastWestHallConnectorLen = 1;
        int eastWestHallCenterRow = panelRows / 2;
        int horizontalCombinedLen = panelOuter + eastWestHallConnectorLen + panelOuter;
        int maxHorizontalOffset = horizontalCombinedLen - panelOuter;

        for (int f = 0; f < frames; f++)
        {
            double t = frames <= 1 ? 1 : f / (double)(frames - 1);
            var buffer = ScreenBuffer.ForGameLayout(rowCount, terminal, console);
            buffer.DrawText(0, 0, titleBar);
            buffer.DrawText(0, 1, "");

            // North: new map above old in the strip; scroll down. South: old above new; scroll down.
            // One hallway row sits between the two room panels (only during this animation).
            if (direction is not 'e' and not 'w')
            {
                var strip = direction == 'n'
                    ? BuildVerticalStripWithHallway(terminal, newRows, oldRows, panelOuter)
                    : BuildVerticalStripWithHallway(terminal, oldRows, newRows, panelOuter);
                int scroll = direction == 'n'
                    ? (int)Math.Round((1 - t) * maxVerticalScroll)
                    : (int)Math.Round(t * maxVerticalScroll);
                scroll = Math.Clamp(scroll, 0, maxVerticalScroll);

                for (int r = 0; r < H; r++)
                {
                    string left = r < newLeft.Count
                        ? AdventureLayout.PadRightVisual(terminal, newLeft[r], leftColWidth)
                        : new string(' ', leftColWidth);

                    int pi = r - rightPanelTopOffset;
                    string right = blankRight;
                    if (pi >= 0 && pi < panelRows)
                        right = strip[scroll + pi];

                    AdventureLayout.DrawWideCompositeRow(
                        terminal,
                        buffer,
                        2 + r,
                        left,
                        right,
                        leftColWidth,
                        AdventureLayout.Gap,
                        panelOuter,
                        screenWidth);
                }
            }

            // East: [old|new], window slides right (offset ↑) — old leaves left, new enters from the right.
            // West: [new|old], window slides left (offset ↓) — old leaves right, new enters from the left.
            if (direction is 'e' or 'w')
            {
                bool east = direction == 'e';
                int offset = east
                    ? (int)Math.Round(t * maxHorizontalOffset)
                    : (int)Math.Round((1 - t) * maxHorizontalOffset);
                offset = Math.Clamp(offset, 0, maxHorizontalOffset);
                for (int r = 0; r < H; r++)
                {
                    string left = r < newLeft.Count
                        ? AdventureLayout.PadRightVisual(terminal, newLeft[r], leftColWidth)
                        : new string(' ', leftColWidth);

                    int pi = r - rightPanelTopOffset;
                    string right = blankRight;
                    if (pi >= 0 && pi < panelRows)
                    {
                        char hallChar = pi == eastWestHallCenterRow - 1 || pi == eastWestHallCenterRow + 1
                            ? '-'
                            : ' ';
                        string hallSegment = hallChar.ToString();
                        string combined = east
                            ? oldRowsPlain[pi] + hallSegment + newRowsPlain[pi]
                            : newRowsPlain[pi] + hallSegment + oldRowsPlain[pi];
                        string rightPlain = combined.Substring(offset, panelOuter);
                        right = terminal.Border(rightPlain);
                    }

                    AdventureLayout.DrawWideCompositeRow(
                        terminal,
                        buffer,
                        2 + r,
                        left,
                        right,
                        leftColWidth,
                        AdventureLayout.Gap,
                        panelOuter,
                        screenWidth);
                }
            }

            buffer.RenderToConsole();
            Thread.Sleep(frameDelayMs);
        }
    }

    private List<string> PadPanelRows(string[] panel, int panelOuter)
    {
        var list = new List<string>(panel.Length);
        foreach (string line in panel)
            list.Add(AdventureLayout.PadRightVisual(terminal, line, panelOuter));
        return list;
    }

    private static List<string> BuildVerticalStripWithHallway(
        ITerminal terminal,
        IReadOnlyList<string> top,
        IReadOnlyList<string> bottom,
        int panelOuter)
    {
        var strip = new List<string>(top.Count + 1 + bottom.Count);
        strip.AddRange(top);
        strip.Add(terminal.Border(AdventureLayout.BuildHallwayConnectorRow(terminal, panelOuter)));
        strip.AddRange(bottom);
        return strip;
    }

    /// <summary>Full-width title line: screen title (left) and HP, gold, armor (right), matching the adventure view.</summary>
    private void WriteFullWidthTitleBar(string screenTitle, GameState state)
    {
        console.WriteLine(
            AdventureLayout.BuildTitleBar(
                terminal,
                screenTitle,
                state,
                AdventureLayout.ScreenWidth,
                EquippedArmorRating(state)));
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
        if (state.EquippedBodyArmorId is not null
            && string.Equals(state.EquippedBodyArmorId, manipulativeId, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Inventory list line: optional yellow <c>E</c> for equipped rows, muted padding when another row is equipped.</summary>
    private void WriteInventoryListLine(
        bool showEquippedYellowE,
        bool padWhenUnequipped,
        int slotNumber,
        string displayName,
        char hotkey)
    {
        string menuText = $"({slotNumber}) {displayName}";
        if (!terminal.UseAnsi)
        {
            if (showEquippedYellowE)
                console.Write("E ");
            else if (padWhenUnequipped)
                console.Write("  ");
            console.WriteLine(menuText);
            return;
        }

        if (showEquippedYellowE)
        {
            console.Write(terminal.Warn("E"));
            console.Write(terminal.Muted(" "));
        }
        else if (padWhenUnequipped)
        {
            console.Write(terminal.Muted("  "));
        }

        char ku = char.ToUpperInvariant(hotkey);
        string needle = $"({ku})";
        int i = menuText.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0)
        {
            console.WriteLine(menuText);
            return;
        }

        console.Write(terminal.Muted(menuText[..i]));
        console.Write(terminal.MenuParenKey(ku));
        console.Write(terminal.Muted(menuText[(i + needle.Length)..]));
        console.WriteLine();
    }

    private void RunInventoryScreen(GameState state)
    {
        string? listFeedback = null;
        while (true)
        {
            ClearConsole();
            WriteFullWidthTitleBar("== Inventory ==", state);
            console.WriteLine();
            if (!string.IsNullOrEmpty(listFeedback))
            {
                foreach (string line in listFeedback.Split(Environment.NewLine))
                    console.WriteLine(terminal.Muted(line));
                console.WriteLine();
                listFeedback = null;
            }

            int n = state.Inventory.Count;
            if (n == 0)
            {
                console.WriteLine(terminal.Muted("  (nothing)"));
                console.WriteLine();
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
            console.WriteLine();
            console.WriteLine(terminal.EscBackHint());

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

    private void AddInventoryItemDetailMenuLines(
        List<string> left,
        bool canEat,
        bool offerEquip,
        bool offerUnequip)
    {
        int w = AdventureLayout.LeftColumnWidth;
        if (canEat)
            left.Add(AdventureLayout.FormatMenuLine(terminal, "(E)at", 'e', w));
        if (offerEquip)
            left.Add(AdventureLayout.FormatMenuLine(terminal, "(E)quip", 'e', w));
        if (offerUnequip)
            left.Add(AdventureLayout.FormatMenuLine(terminal, "(U)nequip", 'u', w));
        left.Add(AdventureLayout.FormatMenuLine(terminal, "(D)rop", 'd', w));
        left.Add("");
        left.Add(terminal.EscBackHint());
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
        bool canEquipBodyArmor = def is { IsEquippableBodyArmor: true };
        bool isWeaponEquipped = canEquipWeapon
            && state.EquippedWeaponId is not null
            && string.Equals(state.EquippedWeaponId, id, StringComparison.OrdinalIgnoreCase);
        bool isHelmetEquipped = canEquipHelmet
            && state.EquippedHelmetId is not null
            && string.Equals(state.EquippedHelmetId, id, StringComparison.OrdinalIgnoreCase);
        bool isBodyArmorEquipped = canEquipBodyArmor
            && state.EquippedBodyArmorId is not null
            && string.Equals(state.EquippedBodyArmorId, id, StringComparison.OrdinalIgnoreCase);
        bool offerEquip = (canEquipWeapon && !isWeaponEquipped)
            || (canEquipHelmet && !isHelmetEquipped)
            || (canEquipBodyArmor && !isBodyArmorEquipped);
        bool offerUnequip = (canEquipWeapon && isWeaponEquipped)
            || (canEquipHelmet && isHelmetEquipped)
            || (canEquipBodyArmor && isBodyArmorEquipped);

        ClearConsole();

        bool withImage = def?.Image is { Length: > 0 };

        var left = new List<string>
        {
            terminal.Accent(manipulativeDescriber.GetDisplayName(id)),
        };
        left.Add("");
        int descCol = AdventureLayout.LeftColumnWidth;
        if (canEat && def is not null)
        {
            foreach (string d in manipulativeDescriber.GetEdibleEffectDescriptionLines(def, state))
                left.AddRange(WrapInventoryDescriptionLineToColumn(d, descCol));
        }

        if (canEquipHelmet && def is not null)
        {
            foreach (string d in manipulativeDescriber.GetHelmetEffectDescriptionLines(def))
                left.AddRange(WrapInventoryDescriptionLineToColumn(d, descCol));
        }

        if (canEquipBodyArmor && def is not null)
        {
            foreach (string d in manipulativeDescriber.GetBodyArmorEffectDescriptionLines(def))
                left.AddRange(WrapInventoryDescriptionLineToColumn(d, descCol));
        }

        if (canEquipWeapon && def is not null)
        {
            foreach (string d in manipulativeDescriber.GetWeaponEffectDescriptionLines(def))
                left.AddRange(WrapInventoryDescriptionLineToColumn(d, descCol));
        }

        List<string> portrait = [];
        if (withImage && def is not null && def.Image is { Length: > 0 } stem)
        {
            portrait.AddRange(
                InventoryManipulativePortraitPanelBuilder.Build(
                    terminal,
                    manipulativeImageStore,
                    manipulativeDescriber.GetDisplayName(id),
                    stem,
                    manipulativeDescriber.GetInventoryPortraitEffectSummaryLine(def, state)));
        }

        left.Add("");
        AddInventoryItemDetailMenuLines(left, canEat, offerEquip, offerUnequip);

        PresentTitleAndTwoColumnPanel(
            "== Inventory ==",
            state,
            left,
            portrait,
            rightPanelTopOffset: 0,
            blankLinesAfterTitle: 1,
            trailingBlankLine: false);

        var action = ReadInventoryItemDetailAction(console, canEat, offerEquip: offerEquip, offerUnequip: offerUnequip);
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
            if (def.IsEquippableBodyArmor)
                state.EquippedBodyArmorId = def.Id;

            if (def.IsEquippableHelmet
                && string.Equals(def.Id, KnownManipulativeIds.Crown, StringComparison.OrdinalIgnoreCase))
                state.GameWon = true;

            bool putOn = (def.IsEquippableHelmet || def.IsEquippableBodyArmor) && !def.IsEquippableWeapon;
            var lines = new List<string>
            {
                putOn
                    ? $"You put on the {def.Name.ToLowerInvariant()}."
                    : $"You equip the {def.Name.ToLowerInvariant()}.",
            };
            if ((def.IsEquippableHelmet || def.IsEquippableBodyArmor) && (def.Armor ?? 0) > 0)
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
            if (canEquipBodyArmor && isBodyArmorEquipped)
                state.EquippedBodyArmorId = null;

            var defForUnequip = def!;
            string unequipNote = defForUnequip switch
            {
                { IsEquippableHelmet: true, IsEquippableWeapon: false } =>
                    $"You take off the {defForUnequip.Name.ToLowerInvariant()}.",
                { IsEquippableBodyArmor: true, IsEquippableWeapon: false } =>
                    $"You remove the {defForUnequip.Name.ToLowerInvariant()}.",
                _ => "You put the weapon away.",
            };
            var lines = new List<string> { unequipNote };
            if ((canEquipHelmet && isHelmetEquipped) || (canEquipBodyArmor && isBodyArmorEquipped))
            {
                if ((defForUnequip.Armor ?? 0) > 0)
                {
                    int ar = EquippedArmorRating(state);
                    lines.Add($"Your Armor is now {ar}.");
                }

                if (canEquipHelmet && isHelmetEquipped && (defForUnequip.AttackBonus ?? 0) != 0)
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

    // Redirected stdin: console.ReadKey is not supported — use ReadLine in those branches.
    private static InventoryItemDetailAction ReadInventoryItemDetailAction(
        IConsoleWrapper systemConsole,
        bool offerEat,
        bool offerEquip,
        bool offerUnequip)
    {
        if (systemConsole.IsInputRedirected)
        {
            while (true)
            {
                var line = systemConsole.ReadLine();
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
            var key = systemConsole.ReadKey(intercept: true);
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

        if (console.IsInputRedirected)
        {
            while (true)
            {
                var line = console.ReadLine();
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
            var key = console.ReadKey(intercept: true);
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
                WriteFullWidthTitleBar("== Ground ==", state);
                console.WriteLine();
                console.WriteLine(terminal.Muted("Nothing on the ground."));
                console.WriteLine();
                PauseForContinue();
                return;
            }

            ClearConsole();
            WriteFullWidthTitleBar("== Ground ==", state);
            console.WriteLine();
            for (int i = 0; i < n; i++)
            {
                int num = i + 1;
                char key = (char)('0' + num);
                terminal.WriteMenuLine(
                    $"({num}) {FormatGroundStackLine(GameStateGroundOps.GetStacksInCurrentRoom(state)[i])}",
                    key);
            }
            console.WriteLine();
            console.WriteLine(terminal.EscBackHint());

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
        WriteFullWidthTitleBar("== Ground ==", state);
        console.WriteLine();
        console.WriteLine(terminal.Accent($"Selected: {FormatGroundStackLine(stack)}"));
        console.WriteLine();
        terminal.WriteMenuLine("(T)ake", 't');
        console.WriteLine();
        console.WriteLine(terminal.EscBackHint());

        var action = ReadSelectedGroundItemAction(console);
        if (action == SelectedGroundItemAction.BackToList)
            return false;

        var taken = GameStateGroundOps.PickUpGroundItemAt(state, index);
        if (taken is null)
            return false;
        console.WriteLine();
        console.WriteLine(
            terminal.Muted($"You pick up the {manipulativeDescriber.GetDisplayName(taken)}."));
        return true;
    }

    private enum SelectedGroundItemAction
    {
        BackToList,
        Take,
    }

    private static SelectedGroundItemAction ReadSelectedGroundItemAction(IConsoleWrapper systemConsole)
    {
        if (systemConsole.IsInputRedirected)
        {
            while (true)
            {
                var line = systemConsole.ReadLine();
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
            var key = systemConsole.ReadKey(intercept: true);
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
        WriteFullWidthTitleBar("== Character ==", state);
        console.WriteLine();
        int level = PlayerLeveling.GetLevelFromTotalExperience(state.Experience);
        int xpInto = PlayerLeveling.ExperienceIntoCurrentLevel(state.Experience);
        int xpSpan = PlayerLeveling.ExperienceSpanForCurrentLevel(state.Experience);
        console.WriteLine(terminal.Accent($"Level: {level}"));
        if (level >= PlayerLeveling.MaxLevel)
        {
            console.WriteLine(
                terminal.Muted(
                    $"  {xpInto} XP past the level cap (total {state.Experience})."));
        }
        else
        {
            console.WriteLine(
                terminal.Muted(
                    $"  {xpInto} / {xpSpan} XP toward level {level + 1} ({state.Experience} total)."));
        }
        console.WriteLine(terminal.Accent($"STR: {state.Strength}"));
        console.WriteLine(terminal.Accent($"DEX: {state.Dexterity}"));
        console.WriteLine(
            terminal.Muted(
                "Higher DEX helps you act first, dodge, and land cleaner hits in a fight."));
        int combatArmor = EquippedArmorRating(state);
        console.WriteLine(
            terminal.Accent($"Armor: {combatArmor}")
            + terminal.Muted(
                " — strips up to that much from each enemy hit (min. 1 damage per hit)."));
        console.WriteLine();
        WriteCharacterEquippedSection(state);
        console.WriteLine();
        PauseForContinue();
    }

    private void WriteCharacterEquippedSection(GameState state)
    {
        console.WriteLine(terminal.Accent("Equipped"));
        if (state.EquippedWeaponId is null)
        {
            console.WriteLine(terminal.Muted("  Weapon: none"));
        }
        else
        {
            var weaponDef = manipulativeStore.Get(state.EquippedWeaponId);
            string weaponName = weaponDef?.Name ?? manipulativeDescriber.GetDisplayName(state.EquippedWeaponId);
            console.WriteLine(terminal.Accent($"  Weapon: {weaponName}"));

            if (weaponDef is null)
            {
                console.WriteLine(terminal.Muted("  No effect data for this item."));
            }
            else
            {
                bool wroteWeaponBonus = false;
                if (weaponDef.AttackBonus is int bonus && bonus != 0)
                {
                    string sign = bonus > 0 ? "+" : "";
                    console.WriteLine(
                        terminal.Muted($"  Attack {sign}{bonus} on each strike in a fight."));
                    wroteWeaponBonus = true;
                }

                if (!wroteWeaponBonus && weaponDef.IsEquippableWeapon)
                    console.WriteLine(terminal.Muted("  No combat bonuses from this weapon."));
            }
        }

        if (state.EquippedHelmetId is null)
        {
            console.WriteLine(terminal.Muted("  Helmet: none"));
        }
        else
        {
            var helmetDef = manipulativeStore.Get(state.EquippedHelmetId);
            string helmetName = helmetDef?.Name ?? manipulativeDescriber.GetDisplayName(state.EquippedHelmetId);
            console.WriteLine(terminal.Accent($"  Helmet: {helmetName}"));

            if (helmetDef is null)
            {
                console.WriteLine(terminal.Muted("  No effect data for this item."));
            }
            else
            {
                bool wroteHelmet = false;
                if (helmetDef.Armor is int ar && ar > 0)
                {
                    console.WriteLine(
                        terminal.Muted($"  Armor {ar} from this piece — stacks with body armor (min. 1 damage per hit)."));
                    wroteHelmet = true;
                }
                else if (helmetDef.IsEquippableHelmet)
                {
                    console.WriteLine(terminal.Muted("  Armor 0 — no reduction from this helmet."));
                    wroteHelmet = true;
                }

                if (helmetDef.AttackBonus is int hb && hb != 0)
                {
                    string sign = hb > 0 ? "+" : "";
                    console.WriteLine(
                        terminal.Muted($"  Attack {sign}{hb} from helmet — stacks with weapon on each hit you land."));
                    wroteHelmet = true;
                }

                if (!wroteHelmet && helmetDef.IsEquippableHelmet)
                    console.WriteLine(terminal.Muted("  No combat bonuses from this helmet."));
            }
        }

        if (state.EquippedBodyArmorId is null)
        {
            console.WriteLine(terminal.Muted("  Body armor: none"));
            return;
        }

        var bodyDef = manipulativeStore.Get(state.EquippedBodyArmorId);
        string bodyName = bodyDef?.Name ?? manipulativeDescriber.GetDisplayName(state.EquippedBodyArmorId);
        console.WriteLine(terminal.Accent($"  Body armor: {bodyName}"));

        if (bodyDef is null)
        {
            console.WriteLine(terminal.Muted("  No effect data for this item."));
            return;
        }

        if (bodyDef.Armor is int bar && bar > 0)
        {
            console.WriteLine(
                terminal.Muted($"  Armor {bar} from this piece — stacks with helmet (min. 1 damage per hit)."));
            return;
        }

        console.WriteLine(terminal.Muted("  Armor 0 — no reduction from this piece."));
    }

    private void RunDebugScreen(GameState state)
    {
        ClearConsole();
        WriteFullWidthTitleBar("== Debug ==", state);
        console.WriteLine();
        console.WriteLine(terminal.EscBackHint());
        WaitForEscBack();
    }

    /// <summary>Waits for Esc (or <c>esc</c> / <c>escape</c> on redirected stdin), same as other list-style sub-screens.</summary>
    private static void WaitForEscBack()
    {
        if (Console.IsInputRedirected)
        {
            while (true)
            {
                var line = Console.ReadLine();
                if (line is null)
                    return;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string t = line.Trim().ToLowerInvariant();
                if (t is "esc" or "escape")
                    return;
            }
        }

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
                return;
        }
    }

    private void PrintHelp()
    {
        ClearConsole();
        WriteFullWidthTitleBar("== Help ==", state);
        console.WriteLine();
        console.WriteLine(
            terminal.Muted(
                "Move with WASD (see compass). N, E, and S also move north, east, and south; west is A only."));
        int helpW = HelpScreenMenuLineWidth();
        console.WriteLine(
            AdventureLayout.FormatMenuLine(
                terminal,
                "(I)nventory: select an item. Edible gear shows healing; helmets and body armor show Armor; then Eat, Equip, Drop, or Esc.",
                'i',
                helpW));
        console.WriteLine(
            AdventureLayout.FormatMenuLine(terminal, "(G)round appears when something lies on the ground here.", 'g', helpW));
        console.WriteLine(
            AdventureLayout.FormatMenuLine(terminal, "(M)ap: overview of how the areas connect.", 'm', helpW));
        console.WriteLine(
            AdventureLayout.FormatMenuLine(
                terminal,
                "(F)ight: Attack, Run, or Die (give up the fight). Wins yield gold; sometimes a find.", 'f', helpW));
        console.WriteLine(
            terminal.Muted(
                "Defeating monsters grants XP; level-ups raise Strength, Dexterity on even levels, and maximum HP."));
        console.WriteLine();
        PauseForContinue();
    }

    /// <summary>Width for <see cref="AdventureLayout.FormatMenuLine"/> on the help screen so long lines are not clipped too aggressively.</summary>
    private int HelpScreenMenuLineWidth()
    {
        try
        {
            int window = console.WindowWidth;
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
        WriteFullWidthTitleBar("== Map ==", state);
        console.WriteLine();
        console.WriteLine(
            terminal.Muted("Rough layout of the grounds. Your room is drawn in yellow."));
        console.WriteLine();

        // Map overview: a 3×3 window centered on the current room, drawn using the same room box as the right panel.
        // Coordinates: (0,0) is current. North is y=-1, south is y=+1.
        var allRooms = roomStore.LoadAll();
        var roomsById = allRooms.ToDictionary(r => r.Id.ToLowerInvariant());

        int outerW = AdventureLayout.MapPanelOuterWidth;
        int outerH = AdventureLayout.BuildRoomPanel(terminal, state.CurrentRoom, outerW, isCurrentRoom: true, forMapOverview: true)
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
            return AdventureLayout.BuildRoomPanel(terminal, room, outerW, isCurrentRoom: isCurrent, forMapOverview: true);
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
                console.Write(new string(' ', indent));
                console.Write(rowPanels[0][line]);
                console.Write(new string(' ', gap));
                console.Write(rowPanels[1][line]);
                console.Write(new string(' ', gap));
                console.Write(rowPanels[2][line]);
                console.WriteLine();
            }

            if (y != maxRadius)
                console.WriteLine();
        }

        console.WriteLine();
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
            Text = "(D)ebug",
            Key = 'd',
            Action = () => RunDebugScreen(state),
        });
        items.Add(new MenuItem
        {
            Text = "e(X)it",
            Key = 'x',
            Action = exit,
        });
        return items;
    }

    /// <summary>
    /// Maps player movement keys to exit keys on <see cref="Room.Exits"/> (n/e/s/w). WASD: W north, A west, S south, D east.
    /// Also accepts N, E, S as compass initials; west is A only (W is north).
    /// </summary>
    private static bool TryParseCompassMovementKey(char input, out char compassExitKey)
    {
        compassExitKey = default;
        switch (char.ToLowerInvariant(input))
        {
            case 'w':
            case 'n':
                compassExitKey = 'n';
                return true;
            case 'd':
            case 'e':
                compassExitKey = 'e';
                return true;
            case 's':
                compassExitKey = 's';
                return true;
            case 'a':
                compassExitKey = 'w';
                return true;
            default:
                return false;
        }
    }

    private bool TryNavigateCompass(
        char internalDirection,
        IReadOnlyDictionary<string, Room> roomsById,
        GameState state)
    {
        if (internalDirection is not ('n' or 'e' or 's' or 'w'))
            return false;

        var room = state.CurrentRoom;
        if (room.Exits is null ||
            !room.Exits.TryGetValue(internalDirection.ToString(), out var destId))
            return false;

        if (!roomsById.TryGetValue(destId.ToLowerInvariant(), out var destRoom))
            return false;

        var oldRoomPanel = AdventureLayout.BuildRoomPanel(terminal, state.CurrentRoom, AdventureLayout.MapPanelOuterWidth, isCurrentRoom: true);
        state.CurrentRoom = destRoom;
        AnimateRoomSlide(
            oldRoomPanel,
            AdventureLayout.BuildRoomPanel(terminal, state.CurrentRoom, AdventureLayout.MapPanelOuterWidth, isCurrentRoom: true),
            state,
            internalDirection);
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
        int sum = 0;
        if (state.EquippedHelmetId is not null)
            sum += manipulativeStore.Get(state.EquippedHelmetId)?.Armor ?? 0;
        if (state.EquippedBodyArmorId is not null)
            sum += manipulativeStore.Get(state.EquippedBodyArmorId)?.Armor ?? 0;
        return sum;
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
        var battleLog = new List<string>();
        bool showIntro = true;

        while (monsterHp > 0 && state.HitPoints > 0)
        {
            var left = BuildFightLeftColumn(
                monster,
                state,
                battleLog,
                showIntro,
                EquippedArmorRating(state),
                EquippedHelmetSlotAttackBonus(state));
            PresentTitleAndTwoColumnPanel(
                "== Fight ==",
                state,
                left,
                FightMonsterPortraitPanelBuilder.Build(terminal, monsterImageStore, monsterHp, monster),
                rightPanelTopOffset: 0,
                blankLinesAfterTitle: 1,
                trailingBlankLine: false);

            var key = char.ToLowerInvariant(ReadInputChar());
            if (key == 'r')
            {
                ClearConsole();
                WriteFullWidthTitleBar("== Fight ==", state);
                console.WriteLine();
                console.WriteLine(terminal.Muted("You slip away and put distance between you and the creature."));
                PauseForContinue();
                return;
            }

            if (key == 'w')
            {
                PresentFightVictory(state, monster);
                return;
            }

            if (key == 'd')
            {
                showIntro = false;
                AppendBattleLog(battleLog, "You give in.");
                state.HitPoints = 0;
                PresentFightDefeat(state, monster, monsterHp, battleLog);
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
                if (TryCompletePlayerAttack(state, monster, ref monsterHp, battleLog))
                    return;
                if (TryCompleteMonsterAttack(state, monster, monsterHp, battleLog))
                    return;
            }
            else
            {
                if (TryCompleteMonsterAttack(state, monster, monsterHp, battleLog))
                    return;
                if (TryCompletePlayerAttack(state, monster, ref monsterHp, battleLog))
                    return;
            }

            AppendBattleLog(battleLog, "");
        }
    }

    /// <summary>Brief silhouette flash on portrait art only (HP/name stay normal); ends on full color.</summary>
    private void FlashMonsterPortraitOnHit(
        IReadOnlyList<string> leftAfterStrike,
        int monsterHp,
        Monster monster,
        GameState fightState)
    {
        void Frame(bool silhouetteArt)
        {
            PresentTitleAndTwoColumnPanel(
                "== Fight ==",
                fightState,
                leftAfterStrike,
                FightMonsterPortraitPanelBuilder.Build(terminal, monsterImageStore, monsterHp, monster, silhouetteArt),
                rightPanelTopOffset: 0,
                blankLinesAfterTitle: 1,
                trailingBlankLine: false);
            console.FlushOutput();
        }

        if (!terminal.UseAnsi)
        {
            Frame(silhouetteArt: false);
            return;
        }

        Frame(silhouetteArt: true);
        Thread.Sleep(85);
        Frame(silhouetteArt: false);
        Thread.Sleep(55);
        Frame(silhouetteArt: true);
        Thread.Sleep(85);
        Frame(silhouetteArt: false);
    }

    /// <summary>Player attacks; returns true if the fight ended (victory).</summary>
    private bool TryCompletePlayerAttack(
        GameState state,
        Monster monster,
        ref int monsterHp,
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

        if (!console.IsOutputRedirected)
        {
            var leftAfterStrike = BuildFightLeftColumn(
                monster,
                state,
                battleLog,
                showIntro: false,
                EquippedArmorRating(state),
                EquippedHelmetSlotAttackBonus(state));
            FlashMonsterPortraitOnHit(leftAfterStrike, monsterHp, monster, state);
        }

        if (monsterHp > 0)
            return false;

        return PresentFightVictory(state, monster);
    }

    /// <summary>Award loot and show the victory panel; returns true so callers can exit the fight loop.</summary>
    private bool PresentFightVictory(GameState state, Monster monster)
    {
        int goldFound = _random.Next(3, 11);
        state.Gold += goldFound;
        int xpGain = monster.GetExperienceReward();
        IReadOnlyList<string> levelUpLines = PlayerLeveling.GainExperience(state, xpGain, terminal);
        var victoryLeft = new List<string>
        {
            terminal.Ok($"The {monster.Name} falls."),
            "",
            terminal.Muted($"You scrape up {goldFound} gold among the debris."),
            terminal.Muted($"You gain {xpGain} experience."),
        };
        foreach (string line in levelUpLines)
            victoryLeft.Add(line);

        if (_random.NextDouble() < 0.35)
        {
            state.Inventory.Add(KnownManipulativeIds.Apple);
            victoryLeft.Add(terminal.Ok("The monster has dropped an apple."));
        }

        victoryLeft.Add("");
        int victoryArtRows = monsterImageStore.Lines(monster.Id).Count();
        PresentTitleAndTwoColumnPanel(
            "== Fight ==",
            state,
            victoryLeft,
            FightMonsterPortraitPanelBuilder.Build(terminal, monsterImageStore, 0, monster, deathCrossPortraitArtRows: victoryArtRows),
            rightPanelTopOffset: 0,
            blankLinesAfterTitle: 1,
            trailingBlankLine: true);
        PauseForContinue();
        return true;
    }

    /// <summary>Monster attacks; returns true if the fight ended (defeat).</summary>
    private bool TryCompleteMonsterAttack(
        GameState state,
        Monster monster,
        int monsterHp,
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

        PresentFightDefeat(state, monster, monsterHp, battleLog);
        return true;
    }

    private void PresentFightDefeat(GameState state, Monster monster, int monsterHp, List<string> battleLog)
    {
        int defeatLogW = AdventureLayout.LeftColumnWidth;
        var defeatLeft = new List<string>();
        defeatLeft.AddRange(BuildFightLogDisplayLines(showIntro: false, monster, defeatLogW, battleLog));
        if (battleLog.Count > 0)
            defeatLeft.Add("");
        defeatLeft.Add("");
        PresentTitleAndTwoColumnPanel(
            "== Fight ==",
            state,
            defeatLeft,
            FightMonsterPortraitPanelBuilder.Build(terminal, monsterImageStore, monsterHp, monster),
            rightPanelTopOffset: 0,
            blankLinesAfterTitle: 1,
            trailingBlankLine: false);
        console.WriteLine();
        console.WriteLine(terminal.Combat("Everything goes dark…"));
        console.WriteLine(terminal.Muted("You wake later, bruised and alone. Someone dragged you clear."));

        int goldLost = state.Gold / 2;
        state.Gold -= goldLost;
        state.CurrentRoom = state.InitialRoom;
        string place = state.InitialRoom.Name;
        if (goldLost > 0)
        {
            console.WriteLine(
                terminal.Muted(
                    $"You find yourself back at {place}. Half your gold is gone ({goldLost} lost, {state.Gold} left)."));
        }
        else
        {
            console.WriteLine(terminal.Muted($"You find yourself back at {place}, empty-pursed as before."));
        }

        state.HitPoints = Math.Max(1, state.MaxHitPoints / 4);
        console.WriteLine();
        PauseForContinue();
    }

    /// <summary>Max number of <em>display</em> lines for intro + battle log; oldest lines drop from the top.</summary>
    private const int FightBattleLogMaxVisibleLines = 8;

    /// <summary>Intro (optional) plus battle log entries as screen lines (wrap + round blanks), trimmed from the top when over <see cref="FightBattleLogMaxVisibleLines"/>.</summary>
    private List<string> BuildFightLogDisplayLines(
        bool showIntro,
        Monster monster,
        int wrapWidth,
        IReadOnlyList<string> battleLog)
    {
        var buffer = new List<string>();
        if (showIntro)
        {
            string nameForWrap = monster.Name.Replace(" ", "\u00A0", StringComparison.Ordinal);
            string plainIntro = $"Something stirs — a {nameForWrap}! {monster.Blurb}";
            foreach (string rawLine in AdventureLayout.WrapText(plainIntro, wrapWidth))
            {
                string line = rawLine.Replace("\u00A0", " ", StringComparison.Ordinal);
                int idx = line.IndexOf(monster.Name, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    buffer.Add(
                        terminal.Muted(line[..idx])
                        + terminal.Combat(monster.Name)
                        + terminal.Muted(line[(idx + monster.Name.Length)..]));
                    continue;
                }

                buffer.Add(terminal.Muted(line));
            }

            buffer.AddRange(
                AdventureLayout.WrapText(monster.FormatThreatSummary(), wrapWidth).Select(terminal.Muted));
        }

        foreach (string entry in battleLog)
        {
            if (entry.Length == 0)
            {
                buffer.Add("");
                continue;
            }

            buffer.AddRange(AdventureLayout.WrapText(entry, wrapWidth).Select(terminal.Muted));
        }

        if (buffer.Count > FightBattleLogMaxVisibleLines)
            buffer.RemoveRange(0, buffer.Count - FightBattleLogMaxVisibleLines);

        return buffer;
    }

    private List<string> BuildFightLeftColumn(
        Monster monster,
        GameState state,
        List<string> battleLog,
        bool showIntro,
        int defenderArmorRating,
        int helmetSlotAttackBonus)
    {
        int w = AdventureLayout.LeftColumnWidth;
        var left = new List<string>();
        if (defenderArmorRating > 0)
        {
            string armorPlain =
                $"Armor {defenderArmorRating}: each enemy hit loses up to {defenderArmorRating} damage (min. 1 per hit).";
            left.AddRange(AdventureLayout.WrapText(armorPlain, w).Select(terminal.Muted));
        }

        if (helmetSlotAttackBonus != 0)
        {
            string sign = helmetSlotAttackBonus > 0 ? "+" : "";
            string helmetPlain =
                $"Attack {sign}{helmetSlotAttackBonus} from helmet — stacks with weapon on each hit you land.";
            left.AddRange(AdventureLayout.WrapText(helmetPlain, w).Select(terminal.Muted));
        }

        left.Add("");
        left.Add(AdventureLayout.FormatMenuLine(terminal, "(A)ttack", 'a', w));
        left.Add(AdventureLayout.FormatMenuLine(terminal, "(R)un", 'r', w));
        left.Add(AdventureLayout.FormatMenuLine(terminal, "(D)ie", 'd', w));
        left.Add(AdventureLayout.FormatMenuLine(terminal, "(W)in", 'w', w));
        if (showIntro || battleLog.Count > 0)
        {
            left.Add("");
            left.AddRange(BuildFightLogDisplayLines(showIntro, monster, w, battleLog));
        }

        left.Add("");
        return left;
    }

    private static void AppendBattleLog(List<string> battleLog, string line) => battleLog.Add(line);

    private char ReadInputChar()
    {
        if (console.IsInputRedirected)
        {
            while (true)
            {
                var line = console.ReadLine();
                if (line is null)
                    return 'x';
                if (line.Length > 0)
                    return line[0];
            }
        }

        while (true)
        {
            var key = console.ReadKey(intercept: true);
            if (key.KeyChar != '\0' && !char.IsWhiteSpace(key.KeyChar))
                return key.KeyChar;
        }
    }
}
