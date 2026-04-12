using Shouldly;
using Tav;
using Xunit;
using Xunit.Abstractions;

namespace Tav.Tests;

public class BuildPortraitPanelCellsTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void BuildPortraitPanelCells_returns_non_empty_for_sample_rows()
    {
        ITerminal terminal = new Terminal(new ConsoleWrapper());
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        string[] borderedLines =
        [
            terminal.Combat("5/6 HP"),
            "",
            terminal.PortraitArt(" >< "),
            terminal.PortraitArt("(xx)"),
            "",
            terminal.Combat("Bone Gnawer"),
        ];

        string[] panel = AdventureLayout.BuildPortraitPanelCells(terminal, borderedLines, panelOuter);

        panel.ShouldNotBeEmpty();
        panel.Length.ShouldBe(borderedLines.Length);
        string joined = string.Join(Environment.NewLine, panel);
        joined.ShouldNotBeNullOrWhiteSpace();

        outputHelper.WriteLine($"panelOuter={panelOuter}, rowCount={panel.Length}");
        outputHelper.WriteLine("---");
        foreach (string row in panel)
            outputHelper.WriteLine(row);
        outputHelper.WriteLine("---");
        outputHelper.WriteLine("(raw joined)");
        outputHelper.WriteLine(joined);
    }
}
