using System.Text;

namespace Tav;

public static class Terminal
{
    public static bool UseAnsi => !Console.IsOutputRedirected;

    public const string Reset = "\x1b[0m";

    public static string Title(string text) => Wrap(text, "\x1b[1m\x1b[93m");

    public static string Accent(string text) => Wrap(text, "\x1b[96m");

    public static string Muted(string text) => Wrap(text, "\x1b[2m\x1b[37m");

    public static string Border(string text) => Wrap(text, "\x1b[36m");

    /// <summary>Map overview: the room you stand in (yellow foreground; neighbors use <see cref="Border"/>).</summary>
    public static string MapHere(string text) => Wrap(text, "\x1b[1m\x1b[93m");

    public static string Combat(string text) => Wrap(text, "\x1b[91m");

    public static string Ok(string text) => Wrap(text, "\x1b[92m");

    public static string Warn(string text) => Wrap(text, "\x1b[93m");

    /// <summary>Player gold totals — bold 256-color gold, distinct from <see cref="Warn"/>.</summary>
    public static string Gold(string text) => Wrap(text, "\x1b[1;38;5;220m");

    public static string HpStatus(int hp, int max)
    {
        string plain = $"HP: {hp}/{max}";
        if (!UseAnsi || max <= 0)
            return plain;
        double ratio = (double)hp / max;
        string open = ratio <= 0.25 ? "\x1b[91m" : ratio <= 0.5 ? "\x1b[93m" : "\x1b[92m";
        return open + plain + Reset;
    }

    public static string HpFraction(int hp, int max)
    {
        string plain = $"{hp}/{max}";
        if (!UseAnsi || max <= 0)
            return plain;
        double ratio = (double)hp / max;
        string open = ratio <= 0.25 ? "\x1b[91m" : ratio <= 0.5 ? "\x1b[93m" : "\x1b[92m";
        return open + plain + Reset;
    }

    public static string DamageNumber(int damage) =>
        UseAnsi ? $"\x1b[1m\x1b[93m     -{damage}{Reset}" : $"     -{damage}";

    /// <summary>Styled <c>(X)</c> / <c>(ESC)</c>: white parentheses, bright green action text.</summary>
    public static string MenuParenKey(char key) =>
        MenuParenKey(char.ToUpperInvariant(key).ToString());

    /// <summary>Styled <c>(TEXT)</c> e.g. <c>(ESC)</c>: white parentheses, bright green inner text.</summary>
    public static string MenuParenKey(string inner)
    {
        if (!UseAnsi)
            return $"({inner})";
        return $"\x1b[37m(\x1b[92m{inner}\x1b[37m){Reset}";
    }

    public static void WriteMenuLine(string text, char key)
    {
        if (!UseAnsi)
        {
            Console.WriteLine(text);
            return;
        }

        char ku = char.ToUpperInvariant(key);
        string needle = $"({ku})";
        int i = text.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0)
        {
            Console.WriteLine(text);
            return;
        }

        Console.Write(Muted(text[..i]));
        Console.Write(MenuParenKey(ku));
        Console.Write(Muted(text[(i + needle.Length)..]));
        Console.WriteLine();
    }

    /// <summary>Footer line: <c>(ESC)</c> matches <see cref="WriteMenuLine"/> styling.</summary>
    public static string EscBackHint()
    {
        if (!UseAnsi)
            return "(ESC) Back";
        return MenuParenKey("ESC") + Muted(" Back");
    }

    public static int VisibleLength(string s)
    {
        int len = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                i += 2;
                while (i < s.Length && s[i] != 'm')
                    i++;
                continue;
            }

            len++;
        }

        return len;
    }

    /// <summary>Removes ANSI SGR sequences so string indices match terminal columns (for slice/substring layout).</summary>
    public static string StripAnsi(string s)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains('\x1b', StringComparison.Ordinal))
            return s;

        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length;)
        {
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                i += 2;
                while (i < s.Length && s[i] != 'm')
                    i++;
                if (i < s.Length)
                    i++;
                continue;
            }

            sb.Append(s[i]);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>Truncates to a maximum visible (on-screen) length, preserving leading ANSI sequences.</summary>
    public static string TruncateVisible(string s, int maxVisible)
    {
        if (maxVisible <= 0)
            return "";

        var sb = new StringBuilder();
        int vis = 0;
        bool cutShort = false;
        for (int i = 0; i < s.Length;)
        {
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                int start = i;
                i += 2;
                while (i < s.Length && s[i] != 'm')
                    i++;
                if (i < s.Length)
                {
                    sb.Append(s, start, i - start + 1);
                    i++;
                    continue;
                }
            }

            if (vis >= maxVisible)
            {
                cutShort = true;
                break;
            }

            sb.Append(s[i]);
            vis++;
            i++;
        }

        if (UseAnsi && cutShort)
            sb.Append(Reset);

        return sb.ToString();
    }

    private static string Wrap(string text, string open)
    {
        if (!UseAnsi || text.Length == 0)
            return text;
        return open + text + Reset;
    }
}
