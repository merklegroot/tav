using System.Text.Json.Serialization;

namespace Tav.Models;

public record Room
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, string>? Exits { get; init; }

    [JsonConverter(typeof(GroundItemsJsonConverter))]
    public List<GroundItemStack>? GroundItems { get; init; }

    public bool IsInitialRoom { get; init; }
}
