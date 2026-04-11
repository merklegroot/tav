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

    /// <summary>
    /// Experience granted when the player wins the fight. Scales with HP, strength, dexterity, and natural attack —
    /// the same knobs that make the encounter harder in combat.
    /// </summary>
    public int GetExperienceReward()
    {
        int danger = HitPoints + Strength + Dexterity + AttackBonus;
        return Math.Max(3, danger);
    }
}
