namespace Tav.Store;

public interface IManipulativeStore
{
    string GetDisplayName(string manipulativeId);
    ManipulativeDefinition? Get(string manipulativeId);
    void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state);
    void WriteArmorEffectDescription(ManipulativeDefinition definition);
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
        int cap = definition.ConsumeEffects?.HealthRestored ?? 0;
        if (cap <= 0)
            return;

        Console.WriteLine(Terminal.Muted($"If you eat it, you can restore up to {cap} hit points."));
        if (state.HitPoints >= state.MaxHitPoints)
        {
            Console.WriteLine(
                Terminal.Muted(
                    "You're at full health — eating won't restore HP, but you can still eat it."));
            return;
        }

        int wouldHeal = Math.Min(cap, state.MaxHitPoints - state.HitPoints);
        Console.WriteLine(
            Terminal.Muted($"If you eat it now, you would restore {wouldHeal} HP."));
    }

    /// <summary>Explains armor rating for helmet-style gear. Caller ensures <paramref name="definition"/> is a helmet.</summary>
    public void WriteArmorEffectDescription(ManipulativeDefinition definition)
    {
        if (!definition.IsEquippableHelmet)
            return;

        int a = definition.Armor ?? 0;
        if (a > 0)
        {
            Console.WriteLine(
                Terminal.Muted(
                    $"Armor {a}: each enemy hit loses up to {a} damage before HP (never below 1 damage per hit)."));
            return;
        }

        Console.WriteLine(
            Terminal.Muted("Armor 0 — this headgear does not reduce damage from hits."));
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
    public int? WeaponDamageBonus { get; init; }
    public bool IsEquippableHelmet { get; init; }
    /// <summary>Damage stripped from each incoming hit (README: Armor). Used by equipped head protection.</summary>
    public int? Armor { get; init; }
}

public record ConsumeEffects
{
    public int? HealthRestored { get; init; }
}
