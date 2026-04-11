using Tav.Models;
using Xunit;

namespace Tav.Tests;

public class MonsterExperienceRewardTests
{
    [Fact]
    public void GetExperienceReward_scales_with_combat_stats_and_orders_sample_monsters()
    {
        var boneGnawer = new Monster
        {
            Id = "bone_gnawer",
            Name = "Bone Gnawer",
            Blurb = "x",
            HitPoints = 6,
            Strength = 10,
            Dexterity = 13,
            AttackBonus = 1,
        };
        var rotHound = new Monster
        {
            Id = "rot_hound",
            Name = "Rot Hound",
            Blurb = "x",
            HitPoints = 10,
            Strength = 12,
            Dexterity = 11,
            AttackBonus = 2,
        };

        Assert.Equal(30, boneGnawer.GetExperienceReward());
        Assert.Equal(35, rotHound.GetExperienceReward());
        Assert.True(rotHound.GetExperienceReward() > boneGnawer.GetExperienceReward());
    }

    [Fact]
    public void GetExperienceReward_has_a_small_floor_for_trivial_stats()
    {
        var tiny = new Monster
        {
            Id = "tiny",
            Name = "Tiny",
            Blurb = "x",
            HitPoints = 1,
            Strength = 1,
            Dexterity = 1,
            AttackBonus = 0,
        };

        Assert.Equal(3, tiny.GetExperienceReward());
    }
}
