using Shouldly;
using Tav;
using Tav.Store;
using Xunit;
using Xunit.Abstractions;

namespace Tav.Tests;

public class InventoryManipulativePortraitPanelBuilderTests(ITestOutputHelper outputHelper)
{
    private static string Plain(string s) => Terminal.StripAnsi(s);

    [Fact]
    public void Build_wraps_art_in_thin_frame_with_name_like_fight_portrait()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Apple", "apple", "Heal 6 HP");

        panel.ShouldNotBeEmpty();
        outputHelper.WriteLine(string.Join(Environment.NewLine, panel));

        panel.Length.ShouldBe(AdventureLayout.PortraitCardInnerLineCount + 2);
        panel[0].ShouldBe(@"┌────────────────┐");
        panel[^1].ShouldBe(@"└────────────────┘");

        panel.Count(p => Plain(p).Contains("Apple", StringComparison.Ordinal)).ShouldBe(1);
        panel.Count(p => Plain(p).Contains("Heal 6 HP", StringComparison.Ordinal)).ShouldBe(1);
        panel.Count(p => Plain(p).Contains("_/_", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public void Build_with_null_effect_summary_has_blank_footer_row()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Torch", "torch", null);

        panel[^2].ShouldBe(@"│                │");
    }

    [Fact]
    public void Build_portrait_effect_shows_atk_without_plus_when_it_fits_one_line()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Crown", "crown", "Armor 1, Atk 1");

        panel.Count(p => Plain(p).Contains("Armor 1, Atk 1", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public void Build_splits_armor_atk_on_comma_when_summary_wider_than_inner_panel()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Crown", "crown", "Armor 10, Atk 12");

        panel.Count(p => Plain(p).Contains("Atk 12", StringComparison.Ordinal)).ShouldBe(1);
        panel.Count(p => Plain(p).Contains("Armor 10", StringComparison.Ordinal)).ShouldBe(1);
    }
}
