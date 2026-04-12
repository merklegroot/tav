namespace Tav;

/// <summary>Abstraction over <see cref="System.Console"/> for I/O and terminal geometry.</summary>
public interface IConsoleWrapper
{
    bool IsOutputRedirected { get; }

    bool IsInputRedirected { get; }

    int WindowWidth { get; }

    void SetCursorVisible(bool visible);

    void Write(string? value);

    void WriteLine(string? value);

    void WriteLine();

    string? ReadLine();

    ConsoleKeyInfo ReadKey(bool intercept);

    void FlushOutput();
}

public class ConsoleWrapper : IConsoleWrapper
{
    public bool IsOutputRedirected => Console.IsOutputRedirected;

    public bool IsInputRedirected => Console.IsInputRedirected;

    public int WindowWidth => Console.WindowWidth;

    public void SetCursorVisible(bool visible) => Console.CursorVisible = visible;

    public void Write(string? value) => Console.Write(value);

    public void WriteLine(string? value) => Console.WriteLine(value);

    public void WriteLine() => Console.WriteLine();

    public string? ReadLine() => Console.ReadLine();

    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

    public void FlushOutput() => Console.Out.Flush();
}
