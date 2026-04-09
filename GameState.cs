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
    public List<string> Inventory { get; } = ["Torch", "Apple"];

    /// <summary>Items on the floor, keyed by lowercase room id.</summary>
    public Dictionary<string, List<string>> GroundItemsByRoomId { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GroundItemsInCurrentRoom
    {
        get
        {
            var id = CurrentRoom.Id.ToLowerInvariant();
            return GroundItemsByRoomId.TryGetValue(id, out var list)
                ? list
                : Array.Empty<string>();
        }
    }

    public GameState(Room start)
    {
        CurrentRoom = start;
        MaxHitPoints = 20;
        HitPoints = MaxHitPoints;
        Strength = 12;
        Dexterity = 14;
    }

    /// <summary>Removes the item from inventory and places it in the current room. Returns the item name.</summary>
    public string DropItemAt(int index)
    {
        string name = Inventory[index];
        Inventory.RemoveAt(index);
        var roomId = CurrentRoom.Id.ToLowerInvariant();
        if (!GroundItemsByRoomId.TryGetValue(roomId, out var ground))
        {
            ground = [];
            GroundItemsByRoomId[roomId] = ground;
        }
        ground.Add(name);
        return name;
    }

    /// <summary>Removes the item from the current room’s ground and adds it to inventory. Returns null if the slot is invalid.</summary>
    public string? PickUpGroundItemAt(int index)
    {
        var roomId = CurrentRoom.Id.ToLowerInvariant();
        if (!GroundItemsByRoomId.TryGetValue(roomId, out var ground))
            return null;
        if (index < 0 || index >= ground.Count)
            return null;

        string name = ground[index];
        ground.RemoveAt(index);
        if (ground.Count == 0)
            GroundItemsByRoomId.Remove(roomId);
        Inventory.Add(name);
        return name;
    }
}
