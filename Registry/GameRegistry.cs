using Microsoft.Extensions.DependencyInjection;
using Tav;
using Tav.Store;

namespace Tav.Registry;

public static class GameRegistry
{
    public static IServiceCollection RegisterGame(this IServiceCollection collection)
    {
        return collection
            .AddSingleton<IRoomStore, RoomStore>()
            .AddSingleton<IMonsterStore, MonsterStore>()
            .AddSingleton<IManipulativeStore, ManipulativeStore>()
            .AddSingleton<IMonsterImageStore, MonsterImageStore>()
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

                var state = new GameState(initialRooms[0]);
                foreach (var room in rooms)
                {
                    if (room.GroundItems is not { Count: > 0 })
                        continue;
                    state.GroundItemsByRoomId[room.Id.ToLowerInvariant()] =
                        new List<string>(room.GroundItems);
                }

                return state;
            })
            .AddScoped<IApp, App>();
    }
}