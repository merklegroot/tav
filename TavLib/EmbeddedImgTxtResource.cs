namespace Tav;

/// <summary>Reads line-based ASCII art from embedded <c>.ans</c> files under <c>res/</c> (or <c>res/{subfolder}/</c> when <paramref name="resSubfolder"/> is set).</summary>
public static class EmbeddedImgTxtResource
{
    public static IEnumerable<string> ReadLines(string stem, string? resSubfolder = null)
    {
        if (string.IsNullOrWhiteSpace(stem))
            yield break;

        var assembly = typeof(EmbeddedImgTxtResource).Assembly;
        var trimmedStem = stem.Trim();
        var suffix = string.IsNullOrWhiteSpace(resSubfolder)
            ? $"{trimmedStem}.ans"
            : $"{ResourceSubfolderPrefix(resSubfolder)}{trimmedStem}.ans";
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

    /// <summary>Maps <c>res/foo/bar/</c> layout to dotted manifest suffix <c>foo.bar.</c></summary>
    private static string ResourceSubfolderPrefix(string resSubfolder)
    {
        var segments = resSubfolder.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? "" : string.Join('.', segments) + ".";
    }
}
