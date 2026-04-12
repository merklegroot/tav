using Tav.Store;

namespace Tav;

/// <summary>Inventory item detail: right column art from <see cref="IManipulativeImageStore"/> (<c>res/items/*.ans</c>), name above (blank row before art) and short effect line(s) below, thin frame like <see cref="FightMonsterPortraitPanelBuilder"/>.</summary>
public static class InventoryManipulativePortraitPanelBuilder
{
    public static string[] Build(
        ITerminal terminal,
        IManipulativeImageStore manipulativeImages,
        string displayName,
        string imageStem,
        string? effectSummaryPlain)
    {
        int outer = AdventureLayout.PortraitCardOuterWidth;
        int inner = AdventureLayout.PortraitCardInnerWidth;
        var raw = new List<string>
        {
            AdventureLayout.CenterVisual(terminal, terminal.Accent(displayName), inner),
            "",
        };
        raw.AddRange(
            manipulativeImages.Lines(imageStem).Select(line =>
                terminal.PortraitArt(terminal.UseAnsi ? line : terminal.StripAnsi(line))));
        raw.Add("");
        if (string.IsNullOrWhiteSpace(effectSummaryPlain))
            raw.Add(AdventureLayout.CenterVisual(terminal, "", inner));
        else
            raw.AddRange(BuildEffectFooterInnerLines(terminal, effectSummaryPlain.Trim(), inner));

        string[] fitted = AdventureLayout.FitPortraitCardInnerLines(terminal, raw, inner);
        string[] cells = AdventureLayout.BuildPortraitPanelCells(terminal, fitted, inner);
        return AdventureLayout.WrapThinBoxAroundInnerRows(terminal, cells, outer);
    }

    /// <summary>One or more inner-width rows (centered, muted). Splits on <c>", "</c> when each segment fits; otherwise word-wraps.</summary>
    private static IEnumerable<string> BuildEffectFooterInnerLines(ITerminal terminal, string plain, int inner)
    {
        if (terminal.VisibleLength(plain) <= inner)
        {
            yield return AdventureLayout.CenterVisual(terminal, terminal.Muted(plain), inner);
            yield break;
        }

        if (plain.Contains(", ", StringComparison.Ordinal))
        {
            string[] parts = plain.Split(", ", StringSplitOptions.None);
            bool allFit = parts.Length >= 2;
            foreach (string p in parts)
            {
                if (terminal.VisibleLength(p.Trim()) > inner)
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
                        yield return AdventureLayout.CenterVisual(terminal, terminal.Muted(t), inner);
                }

                yield break;
            }
        }

        foreach (string w in AdventureLayout.WrapText(plain, inner))
            yield return AdventureLayout.CenterVisual(terminal, terminal.Muted(w), inner);
    }
}
