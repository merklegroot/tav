using System.Threading;
using Raylib_cs;
using Tav;

namespace TavRay;

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
