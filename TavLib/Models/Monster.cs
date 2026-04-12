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

    /// <summary>Author-tuned threat on a 1 (trivial) to 5 (deadly) scale.</summary>
    public required int DifficultyRating { get; init; }

    public string FormatThreatSummary()
    {
        int tier = Math.Clamp(DifficultyRating, 1, 5);
        string word = tier switch
        {
            1 => "Trivial",
            2 => "Easy",
            3 => "Moderate",
            4 => "Hard",
            _ => "Deadly",
        };
        return $"Threat {tier}/5 ({word})";
    }

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
