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
                return new GameState(roomsById["castle_entrance"]);
            })
            .AddScoped<IApp, App>();
    }
}