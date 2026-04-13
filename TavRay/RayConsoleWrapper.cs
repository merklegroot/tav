using Tav;

namespace TavRay;

public class RayConsoleWrapper : IConsoleWrapper
{
    public bool IsOutputRedirected => throw new NotImplementedException();

    public bool IsInputRedirected => throw new NotImplementedException();

    public int WindowWidth => throw new NotImplementedException();

    public void SetCursorVisible(bool visible) => throw new NotImplementedException();

    public void Write(string? value) => throw new NotImplementedException();

    public void WriteLine(string? value) => throw new NotImplementedException();

    public void WriteLine() => throw new NotImplementedException();

    public string? ReadLine() => throw new NotImplementedException();

    public ConsoleKeyInfo ReadKey(bool intercept) => throw new NotImplementedException();

    public void FlushOutput() => throw new NotImplementedException();
}
