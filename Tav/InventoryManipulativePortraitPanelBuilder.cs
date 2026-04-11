using Tav.Store;

namespace Tav;

/// <summary>Inventory item detail: right column art with name above and a short effect line below, thin frame like <see cref="FightMonsterPortraitPanelBuilder"/>.</summary>
public static class InventoryManipulativePortraitPanelBuilder
{
    public static string[] Build(
        IManipulativeImageStore manipulativeImages,
        string displayName,
        string imageStem,
        string? effectSummaryPlain)
    {
        int outer = AdventureLayout.MapPanelOuterWidth;
        int inner = outer - 2;
        string footer = string.IsNullOrWhiteSpace(effectSummaryPlain)
            ? AdventureLayout.CenterVisual("", inner)
            : AdventureLayout.CenterVisual(
                Terminal.Muted(Terminal.TruncateVisible(effectSummaryPlain.Trim(), inner)),
                inner);
        var raw = new List<string>
        {
            AdventureLayout.CenterVisual(Terminal.Accent(displayName), inner),
            "",
        };
        raw.AddRange(manipulativeImages.Lines(imageStem).Select(Terminal.Border));
        raw.Add("");
        raw.Add(footer);

        string[] cells = AdventureLayout.BuildPortraitPanelCells(raw, inner);
        return AdventureLayout.WrapThinBoxAroundInnerRows(cells, outer);
    }
}
