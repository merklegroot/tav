namespace Tav;

/// <summary>Shape of embedded <c>initial_state.json</c> (starting inventory, etc.).</summary>
public record InitialStatePayload
{
    public List<string> Inventory { get; init; } = [];
}
