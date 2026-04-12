namespace Tav;

/// <summary>
/// Total <see cref="GameState.Experience"/> is lifetime XP. Level is derived from thresholds; each level-up improves stats.
/// </summary>
public static class PlayerLeveling
{
    public const int MaxLevel = 60;

    /// <summary>Minimum total XP required to <em>be</em> at least this level (level 1 is always 0).</summary>
    public static int MinTotalXpForLevel(int level)
    {
        if (level <= 1)
            return 0;

        int n = level - 1;
        return 50 * n * n;
    }

    public static int GetLevelFromTotalExperience(int totalXp)
    {
        int level = 1;
        while (level < MaxLevel && MinTotalXpForLevel(level + 1) <= totalXp)
            level++;

        return level;
    }

    /// <summary>XP accumulated within the current level (0 until you reach the next threshold).</summary>
    public static int ExperienceIntoCurrentLevel(int totalXp)
    {
        int level = GetLevelFromTotalExperience(totalXp);
        return totalXp - MinTotalXpForLevel(level);
    }

    /// <summary>How much XP you need in the current level tier before the next level-up.</summary>
    public static int ExperienceSpanForCurrentLevel(int totalXp)
    {
        int level = GetLevelFromTotalExperience(totalXp);
        return MinTotalXpForLevel(level + 1) - MinTotalXpForLevel(level);
    }

    /// <summary>Adds XP, applies every crossed level-up, returns lines to show (empty if no level-up).</summary>
    public static List<string> GainExperience(GameState state, int amount, ITerminal terminal)
    {
        if (amount <= 0)
            return [];

        int oldLevel = GetLevelFromTotalExperience(state.Experience);
        state.Experience += amount;
        int newLevel = GetLevelFromTotalExperience(state.Experience);

        var lines = new List<string>();
        for (int reachedLevel = oldLevel + 1; reachedLevel <= newLevel; reachedLevel++)
            ApplyLevelUp(state, lines, reachedLevel, terminal);

        return lines;
    }

    private static void ApplyLevelUp(GameState state, List<string> lines, int newLevel, ITerminal terminal)
    {
        state.Strength += 1;
        if (newLevel % 2 == 0)
            state.Dexterity += 1;

        state.MaxHitPoints += 2;
        state.HitPoints = Math.Min(state.HitPoints + 2, state.MaxHitPoints);

        string dexPart = newLevel % 2 == 0 ? ", +1 Dexterity" : "";
        lines.Add(
            terminal.Ok(
                $"You advance to level {newLevel}! +1 Strength{dexPart}, +2 maximum HP (current HP up to +2)."));
    }

    public static string BuildXpTitleFragment(ITerminal terminal, GameState state)
    {
        int level = GetLevelFromTotalExperience(state.Experience);
        if (level >= MaxLevel)
        {
            return terminal.Muted("  Lv ")
                   + terminal.Accent(level.ToString())
                   + terminal.Muted("  XP ")
                   + terminal.Accent("MAX");
        }

        int into = ExperienceIntoCurrentLevel(state.Experience);
        int span = ExperienceSpanForCurrentLevel(state.Experience);
        return terminal.Muted("  Lv ")
               + terminal.Accent(level.ToString())
               + terminal.Muted("  XP ")
               + terminal.Accent($"{into}/{span}");
    }
}
