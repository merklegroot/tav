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

        string[] panel = InventoryManipulativePortraitPanelBuilder.Build(store, "Apple", "apple");

        panel.ShouldNotBeEmpty();
        outputHelper.WriteLine(string.Join(Environment.NewLine, panel));

        panel[0].ShouldBe(@"┌──────────────┐");
        panel[^1].ShouldBe(@"└──────────────┘");
        panel[1].ShouldBe(@"│    Apple     │");
        panel[2].ShouldBe(@"│              │");
        panel[^2].ShouldBe(@"│    Apple     │");

        int artRows = store.Lines("apple").Count();
        panel.Length.ShouldBe(artRows + 4 + 2);
    }
}
