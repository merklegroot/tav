using Microsoft.Extensions.DependencyInjection;
using Tav;
using Tav.Models;
using Tav.Store;

namespace Tav.Registry;

public static class GameRegistry
{
    public static IServiceCollection RegisterConsole(this IServiceCollection collection)
    {
        return collection
            .AddSingleton<IConsoleWrapper, ConsoleWrapper>()
            .RegisterGame();
    }
}