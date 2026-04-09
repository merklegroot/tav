using Microsoft.Extensions.DependencyInjection;

namespace Tav.Registry;

public static class GameRegistry
{
    public static IServiceCollection RegisterGame(this IServiceCollection collection)
    {
        return collection.AddScoped<IApp, App>();
    }
}