using Tav.Models;

namespace Tav;

public static class GameStateGroundOps
{
    public static IReadOnlyList<GroundItemStack> GetStacksInCurrentRoom(GameState state)
    {
        var id = state.CurrentRoom.Id.ToLowerInvariant();
        return state.GroundItemsByRoomId.TryGetValue(id, out var list)
            ? list
            : Array.Empty<GroundItemStack>();
    }

    public static string DropInventoryItemAt(GameState state, int index)
    {
        string name = state.Inventory[index];
        state.Inventory.RemoveAt(index);
        if (state.EquippedWeaponId is not null
            && string.Equals(state.EquippedWeaponId, name, StringComparison.OrdinalIgnoreCase))
        {
            state.EquippedWeaponId = null;
        }

        if (state.EquippedHelmetId is not null
            && string.Equals(state.EquippedHelmetId, name, StringComparison.OrdinalIgnoreCase))
        {
            state.EquippedHelmetId = null;
        }

        var roomId = state.CurrentRoom.Id.ToLowerInvariant();
        if (!state.GroundItemsByRoomId.TryGetValue(roomId, out var ground))
        {
            ground = [];
            state.GroundItemsByRoomId[roomId] = ground;
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

    public static string? PickUpGroundItemAt(GameState state, int index)
    {
        var roomId = state.CurrentRoom.Id.ToLowerInvariant();
        if (!state.GroundItemsByRoomId.TryGetValue(roomId, out var ground))
            return null;
        if (index < 0 || index >= ground.Count)
            return null;

        GroundItemStack stack = ground[index];
        string id = stack.Id;
        if (stack.Quantity <= 1)
        {
            ground.RemoveAt(index);
            if (ground.Count == 0)
                state.GroundItemsByRoomId.Remove(roomId);
        }
        else
        {
            ground[index] = stack with { Quantity = stack.Quantity - 1 };
        }

        state.Inventory.Add(id);
        return id;
    }
}
