namespace Tav.Models;

public record MenuItem
{
    public required string Text { get; init; }
    public required char Key { get; init; }
    public required Action Action { get; init; }

    /// <summary>When true, the main view inserts a blank menu line immediately after this row (room-specific options).</summary>
    public bool BlankLineAfter { get; init; }
}
