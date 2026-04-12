using System.Text;

namespace Tav;

/// <summary>ANSI SGR helpers and string measurements; full-screen layout should compose via <see cref="ScreenBuffer"/>.</summary>
public interface ITerminal
{
    bool UseAnsi { get; }

    string Reset { get; }

    string Title(string text);

    string Accent(string text);

    string Muted(string text);

    /// <summary>Map and room walls; neighbor rooms on the overview (cyan).</summary>
    string Border(string text);

    /// <summary>Monster and item portrait sprite glyphs (bright white).</summary>
    string PortraitArt(string text);

    /// <summary>Thin portrait panel frame (<c>┌┐└┘─│</c>).</summary>
    string PortraitFrame(string text);

    /// <summary>Very dim gray for monster portrait hit feedback; stays visible, unlike hiding the art.</summary>
    string Silhouette(string text);

    /// <summary>Map overview: the room you stand in (yellow foreground; neighbors use <see cref="Border"/>).</summary>
    string MapHere(string text);

    string Combat(string text);

    /// <summary>Very dim dark red for combat marks (e.g. death cross on portraits).</summary>
    string CombatDark(string text);

    string Ok(string text);

    string Warn(string text);

    /// <summary>Player gold totals — bold 256-color gold, distinct from <see cref="Warn"/>.</summary>
    string Gold(string text);

    string HpStatus(int hp, int max);

    /// <summary>Styled <c>(X)</c> / <c>(ESC)</c>: white parentheses, bright green action text.</summary>
    string MenuParenKey(char key);

    /// <summary>Styled <c>(TEXT)</c> e.g. <c>(ESC)</c>: white parentheses, bright green inner text.</summary>
    string MenuParenKey(string inner);

    void WriteMenuLine(string text, char key);

    /// <summary>Footer line: <c>(ESC)</c> matches <see cref="WriteMenuLine"/> styling.</summary>
    string EscBackHint();

    int VisibleLength(string s);

    /// <summary>Removes ANSI SGR sequences so string indices match terminal columns (for slice/substring layout).</summary>
    string StripAnsi(string s);

    /// <summary>Truncates to a maximum visible (on-screen) length, preserving leading ANSI sequences.</summary>
    string TruncateVisible(string s, int maxVisible);
}

/// <summary>ANSI SGR helpers and string measurements; full-screen layout should compose via <see cref="ScreenBuffer"/>.</summary>
public class Terminal : ITerminal
{
    public bool UseAnsi => !Console.IsOutputRedirected;

    public string Reset => "\x1b[0m";

    public string Title(string text) => Wrap(text, "\x1b[1m\x1b[93m");

    public string Accent(string text) => Wrap(text, "\x1b[96m");

    public string Muted(string text) => Wrap(text, "\x1b[2m\x1b[37m");

    public string Border(string text) => Wrap(text, "\x1b[36m");

    public string PortraitArt(string text) => Wrap(text, "\x1b[97m");

    public string PortraitFrame(string text) => Wrap(text, "\x1b[90m");

    public string Silhouette(string text) => Wrap(text, "\x1b[2m\x1b[90m");

    public string MapHere(string text) => Wrap(text, "\x1b[1m\x1b[93m");

    public string Combat(string text) => Wrap(text, "\x1b[91m");

    public string CombatDark(string text) => Wrap(text, "\x1b[2m\x1b[38;5;52m");

    public string Ok(string text) => Wrap(text, "\x1b[92m");

    public string Warn(string text) => Wrap(text, "\x1b[93m");

    public string Gold(string text) => Wrap(text, "\x1b[1;38;5;220m");

    public string HpStatus(int hp, int max)
    {
        string plain = $"HP: {hp}/{max}";
        if (!UseAnsi || max <= 0)
            return plain;
        double ratio = (double)hp / max;
        string open = ratio <= 0.25 ? "\x1b[91m" : ratio <= 0.5 ? "\x1b[93m" : "\x1b[92m";
        return open + plain + Reset;
    }

    public string MenuParenKey(char key) =>
        MenuParenKey(char.ToUpperInvariant(key).ToString());

    public string MenuParenKey(string inner)
    {
        if (!UseAnsi)
            return $"({inner})";
        return $"\x1b[37m(\x1b[92m{inner}\x1b[37m){Reset}";
    }

    public void WriteMenuLine(string text, char key)
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

    public string EscBackHint()
    {
        if (!UseAnsi)
            return "(ESC) Back";
        return MenuParenKey("ESC") + Muted(" Back");
    }

    public int VisibleLength(string s)
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

    public string StripAnsi(string s)
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

    public string TruncateVisible(string s, int maxVisible)
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

    private string Wrap(string text, string open)
    {
        if (!UseAnsi || text.Length == 0)
            return text;
        return open + text + Reset;
    }
}
