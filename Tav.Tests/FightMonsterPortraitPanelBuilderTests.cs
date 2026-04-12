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
            DifficultyRating = 3,
        };

        string[] panel = FightMonsterPortraitPanelBuilder.Build(store, 5, monster);

        panel.ShouldNotBeEmpty();
        string joined = string.Join(Environment.NewLine, panel);
        joined.ShouldNotBeNullOrWhiteSpace();

        panel.Length.ShouldBe(AdventureLayout.PortraitCardInnerLineCount + 2);
        outputHelper.WriteLine(joined);

        panel[0].ShouldBe(@"┌────────────────┐");
        panel[1].ShouldBe(@"│                │");
        panel[2].ShouldBe(@"│     5/6 HP     │");
        panel[3].ShouldBe(@"│                │");
        // Art rows carry embedded SGR from .ans when ANSI is on; compare visible glyphs only.
        Terminal.StripAnsi(panel[4]).ShouldBe(@"│    .-----.     │");
        Terminal.StripAnsi(panel[5]).ShouldBe(@"│    o    o      │");
        Terminal.StripAnsi(panel[6]).ShouldBe(@"│    \  ^  /     │");
        Terminal.StripAnsi(panel[7]).ShouldBe(@"│     [===]      │");
        Terminal.StripAnsi(panel[8]).ShouldBe(@"│   /       \s   │");
        panel[9].ShouldBe(@"│                │");
        panel[10].ShouldContain("Bone Gnawer");
        panel[11].ShouldContain("Threat 3/5");
        panel[12].ShouldBe(@"│                │");
        panel[13].ShouldBe(@"└────────────────┘");
    }
}
