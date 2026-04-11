using Tav;

namespace Tav.Store;

/// <summary>Builds player-facing text for manipulatives (display names, edible and equip effect copy).</summary>
public interface IManipulativeUtil
{
    string GetDisplayName(string manipulativeId);

    /// <summary>Lines describing edible heal effects from <see cref="ConsumeEffects"/>; empty when not applicable.</summary>
    IEnumerable<string> GetEdibleEffectDescriptionLines(ManipulativeDefinition definition, GameState state);

    /// <summary>Lines describing helmet armor and optional attack bonus; empty when not a helmet.</summary>
    IEnumerable<string> GetHelmetEffectDescriptionLines(ManipulativeDefinition definition);

    /// <summary>Lines describing body armor; empty when not body armor.</summary>
    IEnumerable<string> GetBodyArmorEffectDescriptionLines(ManipulativeDefinition definition);

    /// <summary>Lines describing weapon attack bonus; empty when not a weapon.</summary>
    IEnumerable<string> GetWeaponEffectDescriptionLines(ManipulativeDefinition definition);

    /// <summary>One short plain line for the inventory portrait footer, or null when there is nothing to show.</summary>
    string? GetInventoryPortraitEffectSummaryLine(ManipulativeDefinition definition, GameState state);
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

    public IEnumerable<string> GetEdibleEffectDescriptionLines(ManipulativeDefinition definition, GameState state)
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

    public IEnumerable<string> GetHelmetEffectDescriptionLines(ManipulativeDefinition definition)
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

    public IEnumerable<string> GetBodyArmorEffectDescriptionLines(ManipulativeDefinition definition)
    {
        if (!definition.IsEquippableBodyArmor)
            yield break;

        int a = definition.Armor ?? 0;
        if (a > 0)
        {
            yield return Terminal.Muted(
                $"Armor {a}: stacks with a helmet. Each enemy hit loses up to your total Armor before HP (never below 1 damage per hit).");
        }
        else
        {
            yield return Terminal.Muted("Armor 0 — this piece does not reduce damage from hits.");
        }
    }

    public IEnumerable<string> GetWeaponEffectDescriptionLines(ManipulativeDefinition definition)
    {
        if (!definition.IsEquippableWeapon)
            yield break;

        int atk = definition.AttackBonus ?? 0;
        if (atk > 0)
        {
            yield return Terminal.Muted(
                $"Attack +{atk}: adds {atk} to strike damage when you land a hit (stacks with attack bonus from your equipped helmet, if any).");
        }
        else if (atk < 0)
        {
            yield return Terminal.Muted(
                $"Attack {atk}: subtracts {-atk} from strike damage when you land a hit.");
        }
        else
        {
            yield return Terminal.Muted("No combat bonuses from this weapon.");
        }
    }

    public string? GetInventoryPortraitEffectSummaryLine(ManipulativeDefinition definition, GameState state)
    {
        int heal = definition.ConsumeEffects?.HealthRestored ?? 0;
        if (definition.IsEdible && heal > 0)
            return $"Heal {heal} HP";

        if (definition.IsEquippableWeapon)
        {
            int atk = definition.AttackBonus ?? 0;
            if (atk != 0)
                return $"Attack {atk}";
            return null;
        }

        if (definition.IsEquippableHelmet)
        {
            int a = definition.Armor ?? 0;
            int atk = definition.AttackBonus ?? 0;
            if (atk != 0)
                return $"Armor {a}, Atk {atk}";

            if (a != 0)
                return $"Armor {a}";
            return null;
        }

        if (definition.IsEquippableBodyArmor)
        {
            int a = definition.Armor ?? 0;
            if (a != 0)
                return $"Armor {a}";
            return null;
        }

        return null;
    }
}
