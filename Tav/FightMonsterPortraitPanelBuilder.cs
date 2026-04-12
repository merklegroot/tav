using System.Text;
using Tav.Models;
using Tav.Store;

namespace Tav;

/// <summary>Builds the fight UI right column: HP, white portrait art from <see cref="IMonsterImageStore"/>, name, gray thin frame.</summary>
public static class FightMonsterPortraitPanelBuilder
{
    public static string[] Build(
        IMonsterImageStore monsterImages,
        int currentHp,
        Monster monster,
        bool silhouetteArt = false,
        int deathCrossPortraitArtRows = 0)
    {
        int outer = AdventureLayout.PortraitCardOuterWidth;
        int inner = AdventureLayout.PortraitCardInnerWidth;
        var raw = BuildRawLines(monsterImages, currentHp, monster, silhouetteArt, inner);
        string[] fitted = AdventureLayout.FitPortraitCardInnerLines(raw, inner);
        string[] cells = AdventureLayout.BuildPortraitPanelCells(fitted, inner);
        int artLineCount = monsterImages.Lines(monster.Id).Count();
        int contentLines = 5 + artLineCount;
        int topPad = Math.Max(0, (AdventureLayout.PortraitCardInnerLineCount - contentLines) / 2);
        int artStartInCells = topPad + 2;
        if (deathCrossPortraitArtRows > 0 && cells.Length > artStartInCells)
            ApplyDeathCrossOverlay(cells, artStartInCells, deathCrossPortraitArtRows, inner);
        return AdventureLayout.WrapThinBoxAroundInnerRows(cells, outer);
    }

    private static List<string> BuildRawLines(
        IMonsterImageStore monsterImages,
        int currentHp,
        Monster monster,
        bool silhouetteArt,
        int innerWidth)
    {
        int shown = Math.Max(0, currentHp);
        string hpLine = AdventureLayout.CenterVisual(Terminal.Combat($"{shown}/{monster.HitPoints} HP"), innerWidth);
        var lines = new List<string> { hpLine, "" };
        lines.AddRange(
            silhouetteArt
                ? monsterImages.Lines(monster.Id).Select(Terminal.Silhouette)
                : monsterImages.Lines(monster.Id).Select(Terminal.PortraitArt));
        lines.Add("");
        lines.Add(AdventureLayout.CenterVisual(Terminal.Combat(monster.Name), innerWidth));
        int tier = Math.Clamp(monster.DifficultyRating, 1, 5);
        lines.Add(AdventureLayout.CenterVisual(Terminal.Muted($"Threat {tier}/5"), innerWidth));
        return lines;
    }

    /// <summary>Very dim dark red X on both diagonals across the art block; non-X glyphs muted so the body stays visible underneath.</summary>
    private static void ApplyDeathCrossOverlay(string[] panel, int artStartIndex, int artRowCount, int panelOuter)
    {
        if (artRowCount <= 0)
            return;

        for (int r = 0; r < artRowCount; r++)
        {
            int idx = artStartIndex + r;
            if (idx < panel.Length)
                panel[idx] = OverlayDeathCrossOnPanelLine(panel[idx], r, artRowCount, panelOuter);
        }
    }

    private static bool VisibleCellOnDeathCross(int row, int col, int artRows, int cols)
    {
        if (cols <= 1)
            return col == 0;
        if (artRows <= 1)
            return col >= cols / 5 && col < cols - cols / 5;

        int backslashCol = (int)Math.Round((double)row * (cols - 1) / (artRows - 1));
        int forwardCol = (int)Math.Round((cols - 1) - (double)row * (cols - 1) / (artRows - 1));
        const int thickness = 2;
        return Math.Abs(col - backslashCol) <= thickness
               || Math.Abs(col - forwardCol) <= thickness;
    }

    private static string OverlayDeathCrossOnPanelLine(string panelLine, int rowInArt, int artRows, int outer)
    {
        string plain = Terminal.StripAnsi(panelLine);
        if (plain.Length > outer)
            plain = plain[..outer];
        plain = plain.PadRight(outer);

        var sb = new StringBuilder();
        for (int c = 0; c < outer; c++)
        {
            if (VisibleCellOnDeathCross(rowInArt, c, artRows, outer))
            {
                sb.Append(Terminal.CombatDark("X"));
                continue;
            }

            char ch = plain[c];
            if (ch == ' ')
                sb.Append(' ');
            else
                sb.Append(Terminal.Muted(ch.ToString()));
        }

        return sb.ToString();
    }
}
