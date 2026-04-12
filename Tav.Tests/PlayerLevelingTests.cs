using Tav;
using Tav.Models;
using Xunit;

namespace Tav.Tests;

public class PlayerLevelingTests
{
    private static readonly ITerminal TestTerminal = new Terminal(new ConsoleWrapper());

    private static GameState NewState()
    {
        var room = new Room
        {
            Id = "test_room",
            Name = "Test",
            Description = "Test room.",
            IsInitialRoom = true,
        };
        return new GameState(room, []);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 50)]
    [InlineData(3, 200)]
    [InlineData(4, 450)]
    public void MinTotalXpForLevel_matches_quadratic_curve(int level, int expected)
    {
        Assert.Equal(expected, PlayerLeveling.MinTotalXpForLevel(level));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(199, 2)]
    [InlineData(200, 3)]
    public void GetLevelFromTotalExperience_respects_thresholds(int totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, PlayerLeveling.GetLevelFromTotalExperience(totalXp));
    }

    [Fact]
    public void GainExperience_applies_one_level_up_crossing_first_threshold()
    {
        var state = NewState();
        state.Experience = 49;
        int str0 = state.Strength;
        int dex0 = state.Dexterity;
        int max0 = state.MaxHitPoints;
        int hp0 = state.HitPoints;

        var lines = PlayerLeveling.GainExperience(state, 1, TestTerminal);

        Assert.Equal(50, state.Experience);
        Assert.Equal(2, PlayerLeveling.GetLevelFromTotalExperience(state.Experience));
        Assert.Equal(str0 + 1, state.Strength);
        Assert.Equal(dex0 + 1, state.Dexterity);
        Assert.Equal(max0 + 2, state.MaxHitPoints);
        Assert.Equal(Math.Min(hp0 + 2, state.MaxHitPoints), state.HitPoints);
        Assert.Single(lines);
        Assert.Contains("level 2", lines[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GainExperience_can_cross_multiple_levels_in_one_gain()
    {
        var state = NewState();
        state.Experience = 40;

        var lines = PlayerLeveling.GainExperience(state, 200, TestTerminal);

        Assert.Equal(240, state.Experience);
        Assert.Equal(3, PlayerLeveling.GetLevelFromTotalExperience(state.Experience));
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void GainExperience_odd_level_does_not_add_Dexterity()
    {
        var state = NewState();
        state.Experience = 199;
        int dexBefore = state.Dexterity;

        PlayerLeveling.GainExperience(state, 1, TestTerminal);

        Assert.Equal(200, state.Experience);
        Assert.Equal(3, PlayerLeveling.GetLevelFromTotalExperience(state.Experience));
        Assert.Equal(dexBefore, state.Dexterity);
    }
}
