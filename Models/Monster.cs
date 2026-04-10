namespace Tav.Models;

public record Monster
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Blurb { get; init; }
    public required int HitPoints { get; init; }
    public required int Strength { get; init; }
    public required int Dexterity { get; init; }
    /// <summary>Natural attack bonus (claws, bite); same role as an equipped weapon’s <c>attackBonus</c>.</summary>
    public int AttackBonus { get; init; }
}
