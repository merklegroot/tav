using System.Threading;
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
/// Members are still being wired to drawing and input; until then, output methods are no-ops and input may
/// block or stub. The host runs the game loop off the main thread so the process can keep presenting frames
/// on the thread that owns the Raylib window.
/// </remarks>
public class RayConsoleWrapper : IConsoleWrapper
{
    public bool IsOutputRedirected => false;

    public bool IsInputRedirected => false;

    public int WindowWidth
    {
        get
        {
            if (Raylib.IsWindowReady())
            {
                int w = Raylib.GetScreenWidth();
                if (w > 0)
                    return w;
            }

            return AdventureLayout.ScreenWidth + 1;
        }
    }

    public void SetCursorVisible(bool visible)
    {
    }

    public void Write(string? value)
    {
    }

    public void WriteLine(string? value)
    {
    }

    public void WriteLine()
    {
    }

    public string? ReadLine() => null;

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        Thread.Sleep(Timeout.Infinite);
        return default;
    }

    public void FlushOutput()
    {
    }
}
