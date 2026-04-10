namespace Tav.Store;

public interface IManipulativeStore
{
    string GetDisplayName(string manipulativeId);
    ManipulativeDefinition? Get(string manipulativeId);
    void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state);
    void WriteHelmetEffectDescription(ManipulativeDefinition definition);

    /// <summary>Same text as <see cref="WriteEdibleEffectDescription"/>; empty when not applicable.</summary>
    IEnumerable<string> EdibleEffectDescriptionLines(ManipulativeDefinition definition, GameState state);

    /// <summary>Same text as <see cref="WriteHelmetEffectDescription"/>; empty when not a helmet.</summary>
    IEnumerable<string> HelmetEffectDescriptionLines(ManipulativeDefinition definition);
}

public class ManipulativeStore : IManipulativeStore
{
    private readonly Lazy<Dictionary<string, ManipulativeDefinition>> _byIdLower = new(Load);

    public string GetDisplayName(string manipulativeId)
    {
        var def = Get(manipulativeId);
        if (def is not null)
            return def.Name;

        return manipulativeId.Replace('_', ' ');
    }

    public ManipulativeDefinition? Get(string manipulativeId)
    {
        if (string.IsNullOrEmpty(manipulativeId))
            return null;
        return _byIdLower.Value.TryGetValue(manipulativeId.ToLowerInvariant(), out var def)
            ? def
            : null;
    }

    /// <summary>Prints what eating would do, from <see cref="ConsumeEffects"/>. Caller ensures <paramref name="definition"/> is edible with a positive heal cap.</summary>
    public void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state)
    {
        foreach (string line in EdibleEffectDescriptionLines(definition, state))
            Console.WriteLine(line);
    }

    /// <summary>Explains armor and optional <c>attackBonus</c>. Caller ensures <paramref name="definition"/> is a helmet (including a crown).</summary>
    public void WriteHelmetEffectDescription(ManipulativeDefinition definition)
    {
        foreach (string line in HelmetEffectDescriptionLines(definition))
            Console.WriteLine(line);
    }

    public IEnumerable<string> EdibleEffectDescriptionLines(ManipulativeDefinition definition, GameState state)
    {
        int cap = definition.ConsumeEffects?.HealthRestored ?? 0;
        if (cap <= 0)
            yield break;

        yield return Terminal.Muted($"Restores {cap} HP.");
        if (state.HitPoints >= state.MaxHitPoints)
        {
            yield return Terminal.Muted(
                "You're at full health — eating won't restore HP, but you can still eat it.");
            yield break;
        }

        int wouldHeal = Math.Min(cap, state.MaxHitPoints - state.HitPoints);
        yield return Terminal.Muted($"If you eat it now, you would restore {wouldHeal} HP.");
    }

    public IEnumerable<string> HelmetEffectDescriptionLines(ManipulativeDefinition definition)
    {
        if (!definition.IsEquippableHelmet)
            yield break;

        int a = definition.Armor ?? 0;
        if (a > 0)
        {
            yield return Terminal.Muted(
                $"Armor {a}: each enemy hit loses up to {a} damage before HP (never below 1 damage per hit).");
        }
        else
        {
            yield return Terminal.Muted("Armor 0 — this helmet does not reduce damage from hits.");
        }

        int atk = definition.AttackBonus ?? 0;
        if (atk > 0)
        {
            yield return Terminal.Muted(
                $"Attack +{atk}: adds {atk} to strike damage when you land a hit (stacks with your equipped weapon).");
        }
        else if (atk < 0)
        {
            yield return Terminal.Muted(
                $"Attack {atk}: subtracts {-atk} from strike damage when you land a hit.");
        }
    }

    private static Dictionary<string, ManipulativeDefinition> Load()
    {
        var list = EmbeddedJsonResource.DeserializeList<ManipulativeDefinition>(
            "manipulatives.json",
            "res/manipulatives.json");
        return list.ToDictionary(d => d.Id.ToLowerInvariant(), StringComparer.Ordinal);
    }
}

public record ManipulativeDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsEdible { get; init; }
    public ConsumeEffects? ConsumeEffects { get; init; }
    public bool IsEquippableWeapon { get; init; }
    public bool IsEquippableHelmet { get; init; }
    /// <summary>Damage stripped from each incoming hit (README: Armor). Used by equipped helmet (including crown).</summary>
    public int? Armor { get; init; }
    /// <summary>Flat bonus to strike damage from this item (weapon and helmet slots both use this field; bonuses stack).</summary>
    public int? AttackBonus { get; init; }

    /// <summary>If set, embedded art is loaded from <c>res/{image}.img.txt</c> (same convention as monster portraits).</summary>
    public string? Image { get; init; }
}

public record ConsumeEffects
{
    public int? HealthRestored { get; init; }
}
