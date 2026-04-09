namespace Tav;

public static class AdventureLayout
{
    public const int Gap = 2;
    public const int LeftColumnWidth = 46;
    // Room panel spec (see README): 16 chars wide, 5 chars tall.
    public const int MapPanelOuterWidth = 16;
    public const int ScreenWidth = LeftColumnWidth + Gap + MapPanelOuterWidth;
}
