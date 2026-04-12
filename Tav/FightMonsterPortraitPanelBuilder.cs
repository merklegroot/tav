using System.Text;
using Tav.Models;
using Tav.Store;

namespace Tav;

/// <summary>Builds the fight UI right column: HP, portrait art from <see cref="IMonsterImageStore"/> (embedded ANSI in <c>res/monsters/*.ans</c>), name, gray thin frame.</summary>
public static class FightMonsterPortraitPanelBuilder
{
    public static string[] Build(
        ITerminal terminal,
        IMonsterImageStore monsterImages,
        int currentHp,
        Monster monster,
        bool silhouetteArt = false,
        int deathCrossPortraitArtRows = 0)
    {
        int outer = AdventureLayout.PortraitCardOuterWidth;
        int inner = AdventureLayout.PortraitCardInnerWidth;
        var raw = BuildRawLines(terminal, monsterImages, currentHp, monster, silhouetteArt, inner);
        string[] fitted = AdventureLayout.FitPortraitCardInnerLines(terminal, raw, inner);
        string[] cells = AdventureLayout.BuildPortraitPanelCells(terminal, fitted, inner);
        int artLineCount = monsterImages.Lines(monster.Id).Count();
        int contentLines = 5 + artLineCount;
        int topPad = Math.Max(0, (AdventureLayout.PortraitCardInnerLineCount - contentLines) / 2);
        int artStartInCells = topPad + 2;
        if (deathCrossPortraitArtRows > 0 && cells.Length > artStartInCells)
            ApplyDeathCrossOverlay(terminal, cells, artStartInCells, deathCrossPortraitArtRows, inner);
        return AdventureLayout.WrapThinBoxAroundInnerRows(terminal, cells, outer);
    }

    private static List<string> BuildRawLines(
        ITerminal terminal,
        IMonsterImageStore monsterImages,
        int currentHp,
        Monster monster,
        bool silhouetteArt,
        int innerWidth)
    {
        int shown = Math.Max(0, currentHp);
        string hpLine = AdventureLayout.CenterVisual(terminal, terminal.Combat($"{shown}/{monster.HitPoints} HP"), innerWidth);
        var lines = new List<string> { hpLine, "" };
        lines.AddRange(monsterImages.Lines(monster.Id).Select(line => StyleMonsterPortraitLine(terminal, line, silhouetteArt)));
        lines.Add("");
        lines.Add(AdventureLayout.CenterVisual(terminal, terminal.Combat(monster.Name), innerWidth));
        int tier = Math.Clamp(monster.DifficultyRating, 1, 5);
        lines.Add(AdventureLayout.CenterVisual(terminal, terminal.Muted($"Threat {tier}/5"), innerWidth));
        return lines;
    }

    /// <summary>Same convention as <see cref="InventoryManipulativePortraitPanelBuilder"/>: embedded SGR in <c>.ans</c> lines when ANSI is on; strip for plain output.</summary>
    private static string StyleMonsterPortraitLine(ITerminal terminal, string line, bool silhouetteArt)
    {
        string body = terminal.UseAnsi ? line : terminal.StripAnsi(line);
        return silhouetteArt
            ? terminal.Silhouette(body)
            : terminal.PortraitArt(body);
    }

    /// <summary>Very dim dark red X on both diagonals across the art block; non-X glyphs muted so the body stays visible underneath.</summary>
    private static void ApplyDeathCrossOverlay(
        ITerminal terminal,
        string[] panel,
        int artStartIndex,
        int artRowCount,
        int panelOuter)
    {
        if (artRowCount <= 0)
            return;

        for (int r = 0; r < artRowCount; r++)
        {
            int idx = artStartIndex + r;
            if (idx < panel.Length)
                panel[idx] = OverlayDeathCrossOnPanelLine(terminal, panel[idx], r, artRowCount, panelOuter);
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

    private static string OverlayDeathCrossOnPanelLine(
        ITerminal terminal,
        string panelLine,
        int rowInArt,
        int artRows,
        int outer)
    {
        string plain = terminal.StripAnsi(panelLine);
        if (plain.Length > outer)
            plain = plain[..outer];
        plain = plain.PadRight(outer);

        var sb = new StringBuilder();
        for (int c = 0; c < outer; c++)
        {
            if (VisibleCellOnDeathCross(rowInArt, c, artRows, outer))
            {
                sb.Append(terminal.CombatDark("X"));
                continue;
            }

            char ch = plain[c];
            if (ch == ' ')
                sb.Append(' ');
            else
                sb.Append(terminal.Muted(ch.ToString()));
        }

        return sb.ToString();
    }
}
