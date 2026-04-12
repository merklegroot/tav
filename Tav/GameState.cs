using Tav.Models;

namespace Tav;

public record GameState
{
    /// <summary>Spawn room from room data (<c>isInitialRoom: true</c>); used after combat defeat to move the player back.</summary>
    public Room InitialRoom { get; init; }

    public Room CurrentRoom { get; set; }
    public bool ShouldExit { get; set; }

    /// <summary>Set when the player equips the crown — the winning condition.</summary>
    public bool GameWon { get; set; }
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Gold { get; set; }

    /// <summary>Total lifetime experience; level is derived via <see cref="PlayerLeveling"/>.</summary>
    public int Experience { get; set; }

    public List<string> Inventory { get; } = [];

    /// <summary>Canonical manipulative id for the wielded weapon, or null.</summary>
    public string? EquippedWeaponId { get; set; }

    /// <summary>Canonical manipulative id for the worn helmet (helmet, crown, …), or null.</summary>
    public string? EquippedHelmetId { get; set; }

    /// <summary>Canonical manipulative id for worn body armor (chain mail, …), or null.</summary>
    public string? EquippedBodyArmorId { get; set; }

    /// <summary>Item stacks on the floor, keyed by lowercase room id.</summary>
    public Dictionary<string, List<GroundItemStack>> GroundItemsByRoomId { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public GameState(Room start, IReadOnlyList<string> startingInventory)
    {
        InitialRoom = start;
        CurrentRoom = start;
        MaxHitPoints = 20;
        HitPoints = MaxHitPoints;
        Strength = 12;
        Dexterity = 14;
        foreach (string id in startingInventory)
            Inventory.Add(id);
    }
}
