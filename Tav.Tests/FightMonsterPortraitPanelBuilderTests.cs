using Shouldly;
using Tav;
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

        panel.Length.ShouldBe(AdventureLayout.PortraitCardInnerLineCount + 2);
        outputHelper.WriteLine(joined);

        panel[0].ShouldBe(@"┌──────────────┐");
        panel[1].ShouldBe(@"│              │");
        panel[2].ShouldBe(@"│    5/6 HP    │");
        panel[3].ShouldBe(@"│              │");
        panel[4].ShouldBe(@"│   .-----.    │");
        panel[5].ShouldBe(@"│   o    o     │");
        panel[6].ShouldBe(@"│   \  ^  /    │");
        panel[7].ShouldBe(@"│    [===]     │");
        panel[8].ShouldBe(@"│  /       \s  │");
        panel[9].ShouldBe(@"│              │");
        panel[10].ShouldBe(@"│ Bone Gnawer  │");
        panel[11].ShouldBe(@"│              │");
        panel[12].ShouldBe(@"└──────────────┘");
    }
}
