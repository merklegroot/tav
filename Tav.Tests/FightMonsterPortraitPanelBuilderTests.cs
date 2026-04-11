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
        string joined = string.Join(Environment.NewLine, panel);
        joined.ShouldNotBeNullOrWhiteSpace();

        outputHelper.WriteLine(panel[6]);
        panel[0].ShouldBe(@"┌──────────────┐");
        panel[1].ShouldBe(@"│    5/6 HP    │");
        panel[2].ShouldBe(@"│              │");
        panel[3].ShouldBe(@"│    .-----.   │");
        panel[4].ShouldBe(@"│    o    o    │");
        panel[5].ShouldBe(@"│    \  ^  /   │");
        panel[6].ShouldBe(@"│     [===]    │");
        panel[7].ShouldBe(@"│  /       \s  │");
        panel[8].ShouldBe(@"│              │");
        panel[9].ShouldBe(@"│ Bone Gnawer  │");
        panel[10].ShouldBe(@"└──────────────┘");

        outputHelper.WriteLine(joined);
    }
}
