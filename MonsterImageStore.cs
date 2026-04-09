namespace Tav;

public static class MonsterImageStore
{
    /// <summary>Lines of the portrait file, or empty when missing.</summary>
    public static IEnumerable<string> Lines(string monsterId)
    {
        var assembly = typeof(MonsterImageStore).Assembly;
        var suffix = $"{monsterId}.img.txt";
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
