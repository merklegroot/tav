using Tav.Models;

namespace Tav;

public record GameState
{
    public Room CurrentRoom { get; set; }
    public bool ShouldExit { get; set; }
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Gold { get; set; }
    public List<string> Inventory { get; } = [];

    /// <summary>Canonical manipulative id for the wielded weapon, or null.</summary>
    public string? EquippedWeaponId { get; set; }

    /// <summary>Item stacks on the floor, keyed by lowercase room id.</summary>
    public Dictionary<string, List<GroundItemStack>> GroundItemsByRoomId { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public GameState(Room start, IReadOnlyList<string> startingInventory)
    {
        CurrentRoom = start;
        MaxHitPoints = 20;
        HitPoints = MaxHitPoints;
        Strength = 12;
        Dexterity = 14;
        foreach (string id in startingInventory)
            Inventory.Add(id);
    }
}
