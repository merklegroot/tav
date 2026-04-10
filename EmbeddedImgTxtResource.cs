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
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
            lines.Add(line);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        foreach (string l in lines)
            yield return l;
    }
}
