using System.Text;
using Raylib_cs;
using Tav;

namespace TavRay;

/// <summary>
/// Raylib-backed implementation of <see cref="IConsoleWrapper"/>. The goal is the same contract as
/// <see cref="ConsoleWrapper"/>—the rest of the game keeps driving the adventure through writes, reads, and
/// terminal width—while output is ultimately shown inside a Raylib window so the experience looks and feels
/// like a classic console app even though there is no real terminal.
/// </summary>
/// <remarks>
/// Game logic may run on a worker thread. On the main thread, call <see cref="DrawPresentedFrame"/> after
/// <c>ClearBackground</c>, then <see cref="CollectInputAfterPresent"/> immediately after <c>EndDrawing</c>.
/// Call <see cref="CollectInputAfterPresent"/> right after <c>EndDrawing</c> (after raylib’s <c>PollInputEvents</c>).
/// Keys are read with <see cref="Raylib.IsKeyPressed"/> the same way Starflight does, plus <see cref="Raylib.GetCharPressed"/>
/// for punctuation and non‑US layouts. (The <c>GetKeyPressed</c> queue is not used; it was unreliable with this host.)
/// Output is accumulated from <see cref="Write"/> / <see cref="WriteLine"/> and committed on <see cref="FlushOutput"/>.
/// <see cref="ReadLine"/> is still a stub (redirected stdin paths).
/// </remarks>
public class RayConsoleWrapper : IConsoleWrapper
{
    private const string HomeClearSequence = "\u001b[H\u001b[2J";

    /// <summary>Raylib order for A–Z; do not assume <c>KEY_B == KEY_A + 1</c> at runtime.</summary>
    private static readonly KeyboardKey[] LetterKeys =
    [
        KeyboardKey.KEY_A, KeyboardKey.KEY_B, KeyboardKey.KEY_C, KeyboardKey.KEY_D, KeyboardKey.KEY_E,
        KeyboardKey.KEY_F, KeyboardKey.KEY_G, KeyboardKey.KEY_H, KeyboardKey.KEY_I, KeyboardKey.KEY_J,
        KeyboardKey.KEY_K, KeyboardKey.KEY_L, KeyboardKey.KEY_M, KeyboardKey.KEY_N, KeyboardKey.KEY_O,
        KeyboardKey.KEY_P, KeyboardKey.KEY_Q, KeyboardKey.KEY_R, KeyboardKey.KEY_S, KeyboardKey.KEY_T,
        KeyboardKey.KEY_U, KeyboardKey.KEY_V, KeyboardKey.KEY_W, KeyboardKey.KEY_X, KeyboardKey.KEY_Y,
        KeyboardKey.KEY_Z,
    ];

    private static readonly KeyboardKey[] DigitKeys =
    [
        KeyboardKey.KEY_ZERO, KeyboardKey.KEY_ONE, KeyboardKey.KEY_TWO, KeyboardKey.KEY_THREE,
        KeyboardKey.KEY_FOUR, KeyboardKey.KEY_FIVE, KeyboardKey.KEY_SIX, KeyboardKey.KEY_SEVEN,
        KeyboardKey.KEY_EIGHT, KeyboardKey.KEY_NINE,
    ];

    private readonly object _ioLock = new();
    private readonly object _keyLock = new();
    private readonly StringBuilder _buffer = new();
    private string _presented = "";
    private volatile int _cachedScreenWidth = 800;
    private bool? _cursorVisibleRequest;
    private readonly Queue<ConsoleKeyInfo> _keys = new();

    public bool IsOutputRedirected => false;

    public bool IsInputRedirected => false;

    public int WindowWidth
    {
        get
        {
            int w = _cachedScreenWidth;
            return w > 0 ? w : AdventureLayout.ScreenWidth + 1;
        }
    }

    public void SetCursorVisible(bool visible)
    {
        lock (_ioLock)
            _cursorVisibleRequest = visible;
    }

    public void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        lock (_ioLock)
            AppendWithClearReset(value);
    }

    public void WriteLine(string? value)
    {
        lock (_ioLock)
        {
            AppendWithClearReset(value ?? "");
            _buffer.Append('\n');
        }
    }

    public void WriteLine()
    {
        lock (_ioLock)
            _buffer.Append('\n');
    }

    public string? ReadLine() => null;

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        lock (_keyLock)
        {
            while (_keys.Count == 0)
                Monitor.Wait(_keyLock);

            return _keys.Dequeue();
        }
    }

    public void FlushOutput()
    {
        lock (_ioLock)
            _presented = _buffer.ToString();
    }

    /// <summary>Main-thread only: call right after <c>EndDrawing</c> (after raylib’s internal <c>PollInputEvents</c>).</summary>
    public void CollectInputAfterPresent()
    {
        if (Raylib.IsWindowReady())
            _cachedScreenWidth = Raylib.GetScreenWidth();

        EnqueueKeyboardInput();
    }

    /// <summary>Main-thread only: after <c>ClearBackground</c>, apply cursor requests and draw the last flushed screen.</summary>
    public void DrawPresentedFrame(Rectangle contentArea)
    {
        bool? vis;
        lock (_ioLock)
        {
            vis = _cursorVisibleRequest;
            _cursorVisibleRequest = null;
        }

        if (vis is { } v && Raylib.IsWindowReady())
        {
            if (v)
                Raylib.ShowCursor();
            else
                Raylib.HideCursor();
        }

        string surface;
        lock (_ioLock)
            surface = _presented;

        Font font = Raylib.GetFontDefault();
        AnsiConsoleRenderer.DrawScreen(surface, font, contentArea);
    }

    private void AppendWithClearReset(string value)
    {
        while (true)
        {
            int idx = value.IndexOf(HomeClearSequence, StringComparison.Ordinal);
            if (idx < 0)
            {
                _buffer.Append(value);
                return;
            }

            _buffer.Clear();
            value = string.Concat(value.AsSpan(0, idx), value.AsSpan(idx + HomeClearSequence.Length));
            if (value.Length == 0)
                return;
        }
    }

    private void EnqueueKeyboardInput()
    {
        bool shift = Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_SHIFT) ||
                     Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_SHIFT);
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_CONTROL) ||
                    Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_CONTROL);
        bool alt = Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_ALT) ||
                   Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_ALT);

        bool capsLock = Raylib.IsKeyDown(KeyboardKey.KEY_CAPS_LOCK);

        void Press(KeyboardKey kb, ConsoleKeyInfo ki)
        {
            if (Raylib.IsKeyPressed(kb))
                EnqueueKey(ki);
        }

        Press(KeyboardKey.KEY_ESCAPE, new ConsoleKeyInfo('\0', ConsoleKey.Escape, shift, alt, ctrl));
        Press(KeyboardKey.KEY_ENTER, new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift, alt, ctrl));
        Press(KeyboardKey.KEY_KP_ENTER, new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift, alt, ctrl));
        Press(KeyboardKey.KEY_TAB, new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl));
        Press(KeyboardKey.KEY_BACKSPACE, new ConsoleKeyInfo('\0', ConsoleKey.Backspace, shift, alt, ctrl));
        Press(KeyboardKey.KEY_UP, new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift, alt, ctrl));
        Press(KeyboardKey.KEY_DOWN, new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift, alt, ctrl));
        Press(KeyboardKey.KEY_LEFT, new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, shift, alt, ctrl));
        Press(KeyboardKey.KEY_RIGHT, new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, shift, alt, ctrl));
        Press(KeyboardKey.KEY_DELETE, new ConsoleKeyInfo('\0', ConsoleKey.Delete, shift, alt, ctrl));
        Press(KeyboardKey.KEY_HOME, new ConsoleKeyInfo('\0', ConsoleKey.Home, shift, alt, ctrl));
        Press(KeyboardKey.KEY_END, new ConsoleKeyInfo('\0', ConsoleKey.End, shift, alt, ctrl));
        Press(KeyboardKey.KEY_PAGE_UP, new ConsoleKeyInfo('\0', ConsoleKey.PageUp, shift, alt, ctrl));
        Press(KeyboardKey.KEY_PAGE_DOWN, new ConsoleKeyInfo('\0', ConsoleKey.PageDown, shift, alt, ctrl));
        Press(KeyboardKey.KEY_SPACE, new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, shift, alt, ctrl));

        for (int i = 0; i < LetterKeys.Length; i++)
        {
            if (!Raylib.IsKeyPressed(LetterKeys[i]))
                continue;

            char upper = (char)('A' + i);
            bool wantUpper = shift ^ capsLock;
            char keyChar = wantUpper ? upper : char.ToLowerInvariant(upper);
            EnqueueKey(new ConsoleKeyInfo(keyChar, (ConsoleKey)upper, shift, alt, ctrl));
        }

        for (int i = 0; i < DigitKeys.Length; i++)
        {
            if (!Raylib.IsKeyPressed(DigitKeys[i]))
                continue;

            char d = (char)('0' + i);
            EnqueueKey(new ConsoleKeyInfo(d, (ConsoleKey)d, shift, alt, ctrl));
        }

        int ch;
        while ((ch = Raylib.GetCharPressed()) != 0)
        {
            if (ch is '\r' or '\n')
            {
                EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift, alt, ctrl));
                continue;
            }

            if (ch == '\b')
            {
                EnqueueKey(new ConsoleKeyInfo('\0', ConsoleKey.Backspace, shift, alt, ctrl));
                continue;
            }

            if (ch == '\t')
            {
                EnqueueKey(new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl));
                continue;
            }

            if (ch < 32)
                continue;

            if (ch > char.MaxValue)
                continue;

            char c = (char)ch;
            if (char.IsAsciiLetter(c) || char.IsAsciiDigit(c))
                continue;

            EnqueueKey(new ConsoleKeyInfo(c, KeyFromChar(c), shift, alt, ctrl));
        }
    }

    private static ConsoleKey KeyFromChar(char ch)
    {
        if (ch is >= 'a' and <= 'z')
            return (ConsoleKey)char.ToUpperInvariant(ch);
        if (ch is >= 'A' and <= 'Z')
            return (ConsoleKey)ch;
        if (ch is >= '0' and <= '9')
            return (ConsoleKey)ch;
        if (ch == ' ')
            return ConsoleKey.Spacebar;

        return ConsoleKey.Spacebar;
    }

    private void EnqueueKey(ConsoleKeyInfo ki)
    {
        lock (_keyLock)
        {
            _keys.Enqueue(ki);
            Monitor.PulseAll(_keyLock);
        }
    }
}
