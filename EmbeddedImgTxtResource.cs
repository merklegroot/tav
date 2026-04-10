namespace Tav;

/// <summary>Reads line-based ASCII art from embedded <c>{stem}.img.txt</c> files under <c>res/</c>.</summary>
public static class EmbeddedImgTxtResource
{
    public static IEnumerable<string> ReadLines(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
            yield break;

        var assembly = typeof(EmbeddedImgTxtResource).Assembly;
        var suffix = $"{stem.Trim()}.img.txt";
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            yield break;

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
            yield break;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }
}
