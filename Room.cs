namespace Tav;

internal sealed record Room(string Id, string Name, string Description, Dictionary<string, string>? Exits);
