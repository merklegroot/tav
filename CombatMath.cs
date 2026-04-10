using Tav.Models;

namespace Tav;

/// <summary>Combat rules from README: d20 hit vs defender DEX, potential × placement damage.</summary>
public static class CombatMath
{
    private const int HitThreshold = 11;

    public static int RollD20(Random random) => random.Next(1, 21);

    public static bool HitLands(int hitTotal) => hitTotal >= HitThreshold;

    public static int PotentialDamage(int attackBonus, int attackerStrength) =>
        Math.Max(1, attackBonus + attackerStrength - 10);

    public static double ComputePlacement(int hitTotal, int attackerDexterity)
    {
        int margin = hitTotal - HitThreshold;
        double placement = 0.25 + margin / 20.0 + 0.03 * (attackerDexterity - 10);
        return Math.Clamp(placement, 0.25, 1.0);
    }

    public static int DamageOnHit(
        int attackBonus,
        int attackerStrength,
        int hitTotal,
        int attackerDexterity)
    {
        int potential = PotentialDamage(attackBonus, attackerStrength);
        double placement = ComputePlacement(hitTotal, attackerDexterity);
        return Math.Max(1, (int)Math.Floor(potential * placement));
    }

    public static AttackResolution ResolveAttack(
        Random random,
        int attackerStrength,
        int attackerDexterity,
        int attackBonus,
        int defenderDexterity)
    {
        int d20 = RollD20(random);
        int hitTotal = d20 + attackerDexterity - defenderDexterity;
        if (!HitLands(hitTotal))
        {
            return new AttackResolution
            {
                Hit = false,
                D20 = d20,
                HitTotal = hitTotal,
                Damage = 0,
            };
        }

        int damage = DamageOnHit(attackBonus, attackerStrength, hitTotal, attackerDexterity);
        return new AttackResolution
        {
            Hit = true,
            D20 = d20,
            HitTotal = hitTotal,
            Damage = damage,
        };
    }

    /// <summary>
    /// Armor (see README Stats): subtracts from rolled damage when the defender takes a hit. Each hit still removes at least 1 HP.
    /// </summary>
    public static int ApplyArmorToIncomingDamage(int rolledDamage, int defenderArmor)
    {
        if (defenderArmor <= 0)
            return rolledDamage;
        return Math.Max(1, rolledDamage - defenderArmor);
    }
}
