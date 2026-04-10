using Tav;

namespace Tav.Store;

/// <summary>Builds player-facing text for manipulatives (display names, edible and helmet effect copy).</summary>
public interface IManipulativeUtil
{
    string GetDisplayName(string manipulativeId);

    /// <summary>Prints what eating would do, from <see cref="ConsumeEffects"/>. Caller ensures <paramref name="definition"/> is edible with a positive heal cap.</summary>
    void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state);

    /// <summary>Explains armor and optional <c>attackBonus</c>. Caller ensures <paramref name="definition"/> is a helmet (including a crown).</summary>
    void WriteHelmetEffectDescription(ManipulativeDefinition definition);

    /// <summary>Same text as <see cref="WriteEdibleEffectDescription"/>; empty when not applicable.</summary>
    IEnumerable<string> EdibleEffectDescriptionLines(ManipulativeDefinition definition, GameState state);

    /// <summary>Same text as <see cref="WriteHelmetEffectDescription"/>; empty when not a helmet.</summary>
    IEnumerable<string> HelmetEffectDescriptionLines(ManipulativeDefinition definition);
}

public class ManipulativeUtil(IManipulativeStore manipulativeStore) : IManipulativeUtil
{
    public string GetDisplayName(string manipulativeId)
    {
        var def = manipulativeStore.Get(manipulativeId);
        if (def is not null)
            return def.Name;

        return manipulativeId.Replace('_', ' ');
    }

    public void WriteEdibleEffectDescription(ManipulativeDefinition definition, GameState state)
    {
        foreach (string line in EdibleEffectDescriptionLines(definition, state))
            Console.WriteLine(line);
    }

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
}
