using Shouldly;
using Tav.Store;
using Xunit;
using Xunit.Abstractions;

namespace Tav.Tests;

public class InventoryManipulativePortraitPanelBuilderTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void Build_wraps_art_in_thin_frame_with_name_like_fight_portrait()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Apple", "apple", "Heal 6 HP");

        panel.ShouldNotBeEmpty();
        outputHelper.WriteLine(string.Join(Environment.NewLine, panel));

        panel[0].ShouldBe(@"┌──────────────┐");
        panel[^1].ShouldBe(@"└──────────────┘");
        panel[1].ShouldBe(@"│    Apple     │");
        panel[2].ShouldBe(@"│              │");
        panel[^2].ShouldBe(@"│  Heal 6 HP   │");

        int artRows = store.Lines("apple").Count();
        panel.Length.ShouldBe(artRows + 4 + 2);
    }

    [Fact]
    public void Build_with_null_effect_summary_has_blank_footer_row()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Torch", "torch", null);

        panel[^2].ShouldBe(@"│              │");
    }

    [Fact]
    public void Build_portrait_effect_shows_atk_without_plus_when_it_fits_one_line()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Crown", "crown", "Armor 1, Atk 1");

        panel[^2].ShouldBe(@"│Armor 1, Atk 1│");
    }

    [Fact]
    public void Build_splits_armor_atk_on_comma_when_summary_wider_than_inner_panel()
    {
        var store = new ManipulativeImageStore();

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Crown", "crown", "Armor 10, Atk 12");

        panel[^2].ShouldBe(@"│    Atk 12    │");
        panel[^3].ShouldBe(@"│   Armor 10   │");
    }
}
