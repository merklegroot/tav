using Tav.Store;

namespace Tav;

/// <summary>Inventory item detail: right column art with name above and caption below, thin frame like <see cref="FightMonsterPortraitPanelBuilder"/>.</summary>
public static class InventoryManipulativePortraitPanelBuilder
{
    public static string[] Build(
        IManipulativeImageStore manipulativeImages,
        string displayName,
        string imageStem)
    {
        int outer = AdventureLayout.MapPanelOuterWidth;
        int inner = outer - 2;
        var raw = new List<string>
        {
            AdventureLayout.CenterVisual(Terminal.Accent(displayName), inner),
            "",
        };
        raw.AddRange(manipulativeImages.Lines(imageStem).Select(Terminal.Border));
        raw.Add("");
        raw.Add(AdventureLayout.CenterVisual(Terminal.Muted(displayName), inner));

        string[] cells = AdventureLayout.BuildPortraitPanelCells(raw, inner);
        return AdventureLayout.WrapThinBoxAroundInnerRows(cells, outer);
    }
}
