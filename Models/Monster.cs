namespace Tav.Models;

public record Monster
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Blurb { get; init; }
    public required int HitPoints { get; init; }
    public required int Strength { get; init; }
    public required int Dexterity { get; init; }
    /// <summary>Natural weapon bonus (claws, bite); same role as an equipped weapon’s bonus.</summary>
    public int WeaponDamageBonus { get; init; }
}
