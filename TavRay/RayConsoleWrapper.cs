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
/// Game logic may run on a worker thread; <see cref="PumpFrame"/> must be called on the main thread each
/// frame (between <c>BeginDrawing</c> / <c>EndDrawing</c>) so Raylib can poll input, apply cursor visibility,
/// and paint the buffered screen. Output is accumulated from <see cref="Write"/> / <see cref="WriteLine"/> and
/// committed on <see cref="FlushOutput"/>; ANSI SGR coloring is interpreted when drawing. <see cref="ReadLine"/>
/// is still a stub (redirected stdin paths); line-based input can be added when needed.
/// </remarks>
public class RayConsoleWrapper : IConsoleWrapper
{
    private const string HomeClearSequence = "\u001b[H\u001b[2J";

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

    /// <summary>Main-thread only: poll input, apply cursor requests, enqueue keys, draw the last flushed screen.</summary>
    public void PumpFrame(Rectangle contentArea)
    {
        Raylib.PollInputEvents();
        if (Raylib.IsWindowReady())
            _cachedScreenWidth = Raylib.GetScreenWidth();

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

        EnqueueKeyboardInput();

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

        int key;
        while ((key = Raylib.GetKeyPressed()) != 0)
        {
            ConsoleKeyInfo? info = MapKeyPressed((KeyboardKey)key, shift, ctrl, alt);
            if (info is { } ki)
                EnqueueKey(ki);
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

    private static ConsoleKeyInfo? MapKeyPressed(KeyboardKey key, bool shift, bool alt, bool ctrl)
    {
        return key switch
        {
            KeyboardKey.KEY_ESCAPE => new ConsoleKeyInfo('\0', ConsoleKey.Escape, shift, alt, ctrl),
            KeyboardKey.KEY_KP_ENTER =>
                new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift, alt, ctrl),
            KeyboardKey.KEY_BACKSPACE => new ConsoleKeyInfo('\0', ConsoleKey.Backspace, shift, alt, ctrl),
            KeyboardKey.KEY_UP => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift, alt, ctrl),
            KeyboardKey.KEY_DOWN => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift, alt, ctrl),
            KeyboardKey.KEY_LEFT => new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, shift, alt, ctrl),
            KeyboardKey.KEY_RIGHT => new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, shift, alt, ctrl),
            KeyboardKey.KEY_DELETE => new ConsoleKeyInfo('\0', ConsoleKey.Delete, shift, alt, ctrl),
            KeyboardKey.KEY_HOME => new ConsoleKeyInfo('\0', ConsoleKey.Home, shift, alt, ctrl),
            KeyboardKey.KEY_END => new ConsoleKeyInfo('\0', ConsoleKey.End, shift, alt, ctrl),
            KeyboardKey.KEY_PAGE_UP => new ConsoleKeyInfo('\0', ConsoleKey.PageUp, shift, alt, ctrl),
            KeyboardKey.KEY_PAGE_DOWN => new ConsoleKeyInfo('\0', ConsoleKey.PageDown, shift, alt, ctrl),
            _ => null,
        };
    }

    private void EnqueueKey(ConsoleKeyInfo ki)
    {
        lock (_keyLock)
        {
            _keys.Enqueue(ki);
            Monitor.Pulse(_keyLock);
        }
    }
}
