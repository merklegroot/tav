namespace Tav.Models;

public record MenuItem
{
    public required string Text { get; init; }
    public required char Key { get; init; }
    public required Action Action { get; init; }
}
