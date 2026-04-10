namespace Tav.Store;

public interface IManipulativeStore
{
    string GetDisplayName(string manipulativeId);
    ManipulativeDefinition? Get(string manipulativeId);
    void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state);
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
    public int? HelmetDamageReduction { get; init; }
}

public record ConsumeEffects
{
    public int? HealthRestored { get; init; }
}
