namespace Tav;

public record Room(
    string Id,
    string Name,
    string Description,
    Dictionary<string, string>? Exits,
    List<string>? GroundItems = null);
