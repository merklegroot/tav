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

    public IReadOnlyList<GroundItemStack> GroundStacksInCurrentRoom
    {
        get
        {
            var id = CurrentRoom.Id.ToLowerInvariant();
            return GroundItemsByRoomId.TryGetValue(id, out var list)
                ? list
                : Array.Empty<GroundItemStack>();
        }
    }

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

    /// <summary>Removes the item from inventory and places it in the current room. Returns the item name.</summary>
    public string DropItemAt(int index)
    {
        string name = Inventory[index];
        Inventory.RemoveAt(index);
        if (EquippedWeaponId is not null
            && string.Equals(EquippedWeaponId, name, StringComparison.OrdinalIgnoreCase))
        {
            EquippedWeaponId = null;
        }

        var roomId = CurrentRoom.Id.ToLowerInvariant();
        if (!GroundItemsByRoomId.TryGetValue(roomId, out var ground))
        {
            ground = [];
            GroundItemsByRoomId[roomId] = ground;
        }

        for (int i = 0; i < ground.Count; i++)
        {
            if (!string.Equals(ground[i].Id, name, StringComparison.OrdinalIgnoreCase))
                continue;

            GroundItemStack s = ground[i];
            ground[i] = s with { Quantity = s.Quantity + 1 };
            return name;
        }

        ground.Add(new GroundItemStack { Id = name, Quantity = 1 });
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

        GroundItemStack stack = ground[index];
        string id = stack.Id;
        if (stack.Quantity <= 1)
        {
            ground.RemoveAt(index);
            if (ground.Count == 0)
                GroundItemsByRoomId.Remove(roomId);
        }
        else
        {
            ground[index] = stack with { Quantity = stack.Quantity - 1 };
        }

        Inventory.Add(id);
        return id;
    }
}
