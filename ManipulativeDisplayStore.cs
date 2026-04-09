namespace Tav;

internal static class ManipulativeDisplayStore
{
    private static readonly Lazy<Dictionary<string, string>> DisplayByIdLower = new(Load);

    public static string DisplayName(string manipulativeId)
    {
        if (DisplayByIdLower.Value.TryGetValue(manipulativeId.ToLowerInvariant(), out var name))
            return name;
        return manipulativeId.Replace('_', ' ');
    }

    private static Dictionary<string, string> Load()
    {
        var list = EmbeddedJsonResource.DeserializeList<ManipulativeDisplayEntry>(
            "manipulatives.json",
            "res/manipulatives.json");
        return list.ToDictionary(e => e.Id.ToLowerInvariant(), e => e.Name, StringComparer.Ordinal);
    }
}

internal sealed record ManipulativeDisplayEntry(string Id, string Name);
