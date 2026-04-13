using Microsoft.Extensions.DependencyInjection;
using Tav;
using Tav.Models;
using Tav.Registry;
using Tav.Store;

namespace TavRay.Registry;

public static class RayRegistry
{
    public static IServiceCollection RegisterRay(this IServiceCollection collection)
    {
        return collection
            .AddSingleton<RayConsoleWrapper>()
            .AddSingleton<IConsoleWrapper>(sp => sp.GetRequiredService<RayConsoleWrapper>())
            .RegisterGame();
    }
}