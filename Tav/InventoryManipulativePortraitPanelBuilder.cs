using Tav.Store;

namespace Tav;

/// <summary>Inventory item detail: right column art with name above and short effect line(s) below, thin frame like <see cref="FightMonsterPortraitPanelBuilder"/>.</summary>
public static class InventoryManipulativePortraitPanelBuilder
{
    public static string[] Build(
        IManipulativeImageStore manipulativeImages,
        string displayName,
        string imageStem,
        string? effectSummaryPlain)
    {
        int outer = AdventureLayout.PortraitCardOuterWidth;
        int inner = AdventureLayout.PortraitCardInnerWidth;
        var raw = new List<string>
        {
            AdventureLayout.CenterVisual(Terminal.Accent(displayName), inner),
            "",
        };
        raw.AddRange(manipulativeImages.Lines(imageStem).Select(Terminal.PortraitArt));
        raw.Add("");
        if (string.IsNullOrWhiteSpace(effectSummaryPlain))
            raw.Add(AdventureLayout.CenterVisual("", inner));
        else
            raw.AddRange(BuildEffectFooterInnerLines(effectSummaryPlain.Trim(), inner));

        string[] fitted = AdventureLayout.FitPortraitCardInnerLines(raw, inner);
        string[] cells = AdventureLayout.BuildPortraitPanelCells(fitted, inner);
        return AdventureLayout.WrapThinBoxAroundInnerRows(cells, outer);
    }

    /// <summary>One or more inner-width rows (centered, muted). Splits on <c>", "</c> when each segment fits; otherwise word-wraps.</summary>
    private static IEnumerable<string> BuildEffectFooterInnerLines(string plain, int inner)
    {
        if (Terminal.VisibleLength(plain) <= inner)
        {
            yield return AdventureLayout.CenterVisual(Terminal.Muted(plain), inner);
            yield break;
        }

        if (plain.Contains(", ", StringComparison.Ordinal))
        {
            string[] parts = plain.Split(", ", StringSplitOptions.None);
            bool allFit = parts.Length >= 2;
            foreach (string p in parts)
            {
                if (Terminal.VisibleLength(p.Trim()) > inner)
                {
                    allFit = false;
                    break;
                }
            }

            if (allFit)
            {
                foreach (string p in parts)
                {
                    string t = p.Trim();
                    if (t.Length > 0)
                        yield return AdventureLayout.CenterVisual(Terminal.Muted(t), inner);
                }

                yield break;
            }
        }

        foreach (string w in AdventureLayout.WrapText(plain, inner))
            yield return AdventureLayout.CenterVisual(Terminal.Muted(w), inner);
    }
}
