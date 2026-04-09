using System.Text;
using System.Text.Json;

namespace Tav;

internal static class Program
{
    public static void Main()
    {
        var rooms = RoomStore.LoadAll();
        var roomsById = rooms.ToDictionary(r => r.Id.ToLowerInvariant());

        var state = new GameState(roomsById["castle_entrance"]);

        while (!state.ShouldExit)
        {
            PrintScreen(state);

            var menuItems = BuildMenuItems(
                state.CurrentRoom,
                roomsById,
                state,
                r => state.CurrentRoom = r,
                () => state.ShouldExit = true);

            foreach (var item in menuItems)
                Console.WriteLine(item.Text);
            Console.WriteLine();

            var input = ReadInputChar();
            var normalized = char.ToLowerInvariant(input);
            menuItems.FirstOrDefault(m => m.Key == normalized)?.Action.Invoke();
        }
    }

    private static void ClearConsole()
    {
        if (Console.IsOutputRedirected == false)
        {
            Console.Write("\u001b[H\u001b[2J");
            Console.Out.Flush();
        }
    }

    private static void PrintScreen(GameState state)
    {
        ClearConsole();

        const int gap = 2;
        const int leftColWidth = 46;
        const int panelOuter = 24;

        var leftLines = new List<string>
        {
            "== Adventure Game ==",
            $"HP: {state.HitPoints}/{state.MaxHitPoints}",
            "",
            Truncate(state.CurrentRoom.Name, leftColWidth),
        };
        leftLines.AddRange(WrapText(state.CurrentRoom.Description, leftColWidth));

        var panel = BuildRoomPanel(state.CurrentRoom, panelOuter);

        int minWidth = leftColWidth + gap + panelOuter;
        if (CanUseWideLayout(minWidth))
        {
            for (int i = 0; i < panel.Length; i++)
            {
                string left = i < leftLines.Count ? leftLines[i] : "";
                Console.WriteLine(PadRightVisual(left, leftColWidth) + new string(' ', gap) + panel[i]);
            }

            for (int i = panel.Length; i < leftLines.Count; i++)
                Console.WriteLine(leftLines[i]);
        }
        else
        {
            foreach (var line in leftLines)
                Console.WriteLine(line);
            Console.WriteLine();
            foreach (var line in panel)
                Console.WriteLine(line);
        }

        Console.WriteLine();
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
        if (s.Length >= totalWidth)
            return s[..totalWidth];
        return s + new string(' ', totalWidth - s.Length);
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

    private static string[] BuildRoomPanel(Room room, int outerWidth)
    {
        int inner = outerWidth - 2;
        string title = Truncate(room.Name, Math.Max(1, inner - 4));
        string start = "─ " + title + " ";
        if (start.Length > inner)
            start = start[..inner];
        string top = "┌" + start + new string('─', inner - start.Length) + "┐";

        bool n = HasExit(room, 'n');
        bool e = HasExit(room, 'e');
        bool s = HasExit(room, 's');
        bool w = HasExit(room, 'w');

        string rowN = PadInner(n ? "N" : "·", inner);
        string rowMid = PadInner($"{(w ? "W" : "·")}   ●   {(e ? "E" : "·")}", inner);
        string rowS = PadInner(s ? "S" : "·", inner);

        string blank = "│" + new string(' ', inner) + "│";
        string bottom = "└" + new string('─', inner) + "┘";

        return
        [
            top,
            blank,
            "│" + rowN + "│",
            blank,
            "│" + rowMid + "│",
            blank,
            "│" + rowS + "│",
            blank,
            bottom,
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

    private static void PauseForContinue()
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

    private static void PrintInventory(GameState state)
    {
        ClearConsole();
        Console.WriteLine("== Inventory ==");
        Console.WriteLine($"HP: {state.HitPoints}/{state.MaxHitPoints}");
        Console.WriteLine();
        if (state.Inventory.Count == 0)
            Console.WriteLine("  (nothing)");
        else
            foreach (var name in state.Inventory)
                Console.WriteLine($"  - {name}");
        Console.WriteLine();
        PauseForContinue();
    }

    private static void PrintCharacter(GameState state)
    {
        ClearConsole();
        Console.WriteLine("== Character ==");
        Console.WriteLine();
        Console.WriteLine($"HP:  {state.HitPoints}/{state.MaxHitPoints}");
        Console.WriteLine($"STR: {state.Strength}");
        Console.WriteLine($"DEX: {state.Dexterity}");
        Console.WriteLine();
        PauseForContinue();
    }

    private static List<MenuItem> BuildMenuItems(
        Room currentRoom,
        IReadOnlyDictionary<string, Room> roomsById,
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
            items.Add(new MenuItem(FormatDirectionOption(dir, destRoom.Name), dir, () => navigateTo(destRoom)));
        }

        items.Add(new MenuItem("(I)nventory", 'i', () => PrintInventory(state)));
        items.Add(new MenuItem("(C)haracter", 'c', () => PrintCharacter(state)));
        items.Add(new MenuItem("e(X)it", 'x', exit));
        return items;
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

        public GameState(Room start)
        {
            CurrentRoom = start;
            MaxHitPoints = 20;
            HitPoints = MaxHitPoints;
            Strength = 12;
            Dexterity = 14;
        }
    }
}

internal sealed record Room(string Id, string Name, string Description, Dictionary<string, string>? Exits);

internal sealed record MenuItem(string Text, char Key, Action Action);

internal static class RoomStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<Room> LoadAll()
    {
        var assembly = typeof(Program).Assembly;
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
