using Microsoft.Extensions.DependencyInjection;

namespace Tav.Registry;

public static class GameRegistry
{
    public static IServiceCollection RegisterGame(this IServiceCollection collection)
    {
        return collection
            .AddSingleton<GameState>(_ =>
            {
                var rooms = RoomStore.LoadAll();
                var roomsById = rooms.ToDictionary(r => r.Id.ToLowerInvariant());
                var state = new GameState(roomsById["castle_entrance"]);
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