using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Tav.Tests;

public class BuildPortraitPanelCellsTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void BuildPortraitPanelCells_returns_non_empty_for_sample_rows()
    {
        int panelOuter = AdventureLayout.MapPanelOuterWidth;
        string[] borderedLines =
        [
            Terminal.Combat("5/6 HP"),
            "",
            Terminal.Border(" >< "),
            Terminal.Border("(xx)"),
            "",
            Terminal.Combat("Bone Gnawer"),
        ];

        string[] panel = AdventureLayout.BuildPortraitPanelCells(borderedLines, panelOuter);

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
