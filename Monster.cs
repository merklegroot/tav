namespace Tav;

public record Monster
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Blurb { get; init; }
    public required int HitPoints { get; init; }
    public required int MaxDamage { get; init; }
}
