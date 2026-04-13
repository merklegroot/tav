namespace Tav.Store;

public interface IManipulativeStore
{
    ManipulativeDefinition? Get(string manipulativeId);

    /// <summary>All definitions in JSON order (for shop stock and similar).</summary>
    IReadOnlyList<ManipulativeDefinition> ListAll();
}

public class ManipulativeStore : IManipulativeStore
{
    private static readonly Lazy<(Dictionary<string, ManipulativeDefinition> ById, IReadOnlyList<ManipulativeDefinition> All)> Data =
        new(LoadAll);

    public ManipulativeDefinition? Get(string manipulativeId)
    {
        if (string.IsNullOrEmpty(manipulativeId))
            return null;
        return Data.Value.ById.TryGetValue(manipulativeId.ToLowerInvariant(), out var def)
            ? def
            : null;
    }

    public IReadOnlyList<ManipulativeDefinition> ListAll() => Data.Value.All;

    private static (Dictionary<string, ManipulativeDefinition>, IReadOnlyList<ManipulativeDefinition>) LoadAll()
    {
        var list = EmbeddedJsonResource.DeserializeList<ManipulativeDefinition>(
            "manipulatives.json",
            "res/manipulatives.json");
        var dict = list.ToDictionary(d => d.Id.ToLowerInvariant(), StringComparer.Ordinal);
        return (dict, list);
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
    public bool IsEquippableBodyArmor { get; init; }

    /// <summary>Damage stripped from each incoming hit (README: Armor). Sums from equipped helmet and body armor.</summary>
    public int? Armor { get; init; }
    /// <summary>Flat bonus to strike damage from this item (weapon and helmet slots both use this field; bonuses stack).</summary>
    public int? AttackBonus { get; init; }

    /// <summary>If set, embedded art is loaded from <c>res/items/{image}.ans</c> (monster portraits use <c>res/monsters/</c>).</summary>
    public string? Image { get; init; }

    /// <summary>Gold to buy one from a shop that stocks this item; unset means not sold.</summary>
    public int? ShopBuyGold { get; init; }

    /// <summary>Gold the shop pays per item when you sell; unset means not bought back (unless derived from buy price later).</summary>
    public int? ShopSellGold { get; init; }
}

public record ConsumeEffects
{
    public int? HealthRestored { get; init; }
}
