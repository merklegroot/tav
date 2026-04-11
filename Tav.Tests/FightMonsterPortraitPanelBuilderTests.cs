using Shouldly;
using Tav.Models;
using Tav.Store;
using Xunit;
using Xunit.Abstractions;

namespace Tav.Tests;

public class FightMonsterPortraitPanelBuilderTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void Build_returns_non_empty_panel()
    {
        var store = new MonsterImageStore();
        var monster = new Monster
        {
            Id = "bone_gnawer",
            Name = "Bone Gnawer",
            Blurb = "Too many joints click when it moves.",
            HitPoints = 6,
            Strength = 10,
            Dexterity = 13,
            AttackBonus = 1,
        };

        string[] panel = FightMonsterPortraitPanelBuilder.Build(store, 5, monster);

        panel.ShouldNotBeEmpty();
        string joined = string.Join('\n', panel);
        joined.ShouldNotBeNullOrWhiteSpace();

        outputHelper.WriteLine(joined);
    }
}
