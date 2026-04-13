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

