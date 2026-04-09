using System.Text.Json;

namespace Tav;

public static class EmbeddedJsonResource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<T> DeserializeList<T>(string resourceFileSuffix, string displayName)
    {
        var assembly = typeof(EmbeddedJsonResource).Assembly;
        var name = assembly
            .GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith(resourceFileSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing embedded resource {displayName}");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {displayName}");
        return JsonSerializer.Deserialize<List<T>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"{displayName} was empty or invalid");
    }
}
