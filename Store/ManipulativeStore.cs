using System.Diagnostics.CodeAnalysis;

namespace Tav.Store;

public static class ManipulativeStore
{
    private static readonly Lazy<Dictionary<string, ManipulativeDefinition>> ByIdLower = new(Load);

    public static string DisplayName(string manipulativeId)
    {
        if (TryGet(manipulativeId, out var def))
            return def.Name;

        return manipulativeId.Replace('_', ' ');
    }

    public static bool TryGet(
        string manipulativeId,
        [NotNullWhen(true)] out ManipulativeDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrEmpty(manipulativeId))
            return false;
        return ByIdLower.Value.TryGetValue(manipulativeId.ToLowerInvariant(), out definition);
    }

    /// <summary>Prints what eating would do, from <see cref="ConsumeEffects"/>. Caller ensures <paramref name="definition"/> is edible with a positive heal cap.</summary>
    public static void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state)
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
}

public record ConsumeEffects
{
    public int? HealthRestored { get; init; }
}
