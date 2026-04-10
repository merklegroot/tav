namespace Tav.Store;

public interface IManipulativeStore
{
    ManipulativeDefinition? Get(string manipulativeId);
}

public class ManipulativeStore : IManipulativeStore
{
    private readonly Lazy<Dictionary<string, ManipulativeDefinition>> _byIdLower = new(Load);

    public ManipulativeDefinition? Get(string manipulativeId)
    {
        if (string.IsNullOrEmpty(manipulativeId))
            return null;
        return _byIdLower.Value.TryGetValue(manipulativeId.ToLowerInvariant(), out var def)
            ? def
            : null;
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
