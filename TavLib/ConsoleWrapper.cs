namespace Tav;

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
