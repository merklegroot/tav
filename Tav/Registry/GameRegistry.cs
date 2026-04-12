using Microsoft.Extensions.DependencyInjection;
using Tav;
using Tav.Models;
using Tav.Store;

namespace Tav.Registry;

public static class GameRegistry
{
    public static IServiceCollection RegisterGame(this IServiceCollection collection)
    {
        return collection
            .AddSingleton<ITerminal, Terminal>()
            .AddSingleton<IRoomStore, RoomStore>()
            .AddSingleton<IMonsterStore, MonsterStore>()
            .AddSingleton<IManipulativeStore, ManipulativeStore>()
            .AddSingleton<IManipulativeUtil, ManipulativeUtil>()
            .AddSingleton<IMonsterImageStore, MonsterImageStore>()
            .AddSingleton<IManipulativeImageStore, ManipulativeImageStore>()
            .AddSingleton<GameState>(sp =>
            {
                var rooms = sp.GetRequiredService<IRoomStore>().LoadAll();
                var initialRooms = rooms.Where(r => r.IsInitialRoom).ToList();
                if (initialRooms.Count != 1)
                {
                    throw new InvalidOperationException(
                        initialRooms.Count == 0
                            ? "Room data must set isInitialRoom: true on exactly one room."
                            : "Only one room may have isInitialRoom: true.");
                }

                var initial = EmbeddedJsonResource.DeserializeObject<InitialStatePayload>(
                    "initial_state.json",
                    "res/initial_state.json");
                var startingInventory = initial.Inventory ?? [];
                var state = new GameState(initialRooms[0], startingInventory);
                foreach (var room in rooms)
                {
                    if (room.GroundItems is not { Count: > 0 })
                        continue;
                    state.GroundItemsByRoomId[room.Id.ToLowerInvariant()] =
                        room.GroundItems
                            .Select(s => new GroundItemStack { Id = s.Id, Quantity = s.Quantity })
                            .ToList();
                }

                return state;
            })
            .AddScoped<IApp, App>();
    }
}