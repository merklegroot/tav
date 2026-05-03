using Spectre.Console;

namespace Tav;

/// <summary>
/// Spectre.Console-backed <see cref="IConsoleWrapper"/> for the terminal build. Writes go through
/// <see cref="IAnsiConsoleOutput.Writer"/> so existing ANSI SGR output is not interpreted as Spectre markup.
/// </summary>
public sealed class SpectreConsoleWrapper : IConsoleWrapper
{
    public bool IsOutputRedirected => !AnsiConsole.Profile.Out.IsTerminal;

    public bool IsInputRedirected => Console.IsInputRedirected;

    public int WindowWidth => Math.Max(1, AnsiConsole.Profile.Out.Width);

    public void SetCursorVisible(bool visible) => AnsiConsole.Cursor.Show(visible);

    public void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        AnsiConsole.Profile.Out.Writer.Write(value);
    }

    public void WriteLine(string? value) => AnsiConsole.Profile.Out.Writer.WriteLine(value);

    public void WriteLine() => AnsiConsole.Profile.Out.Writer.WriteLine();

    public string? ReadLine() => Console.ReadLine();

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        ConsoleKeyInfo? key = AnsiConsole.Console.Input.ReadKey(intercept);
        if (key is { } k)
            return k;

        return Console.ReadKey(intercept);
    }

    public void FlushOutput() => AnsiConsole.Profile.Out.Writer.Flush();
}
