namespace Tav;

public record Room
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, string>? Exits { get; init; }
    public List<string>? GroundItems { get; init; }
    public bool IsInitialRoom { get; init; }
}
