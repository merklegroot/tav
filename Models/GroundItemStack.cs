namespace Tav.Models;

public record GroundItemStack
{
    public required string Id { get; init; }
    public int Quantity { get; init; } = 1;
}
