using Microsoft.Extensions.DependencyInjection;
using Tav.Registry;

namespace Tav;

internal static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.RegisterGame();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IApp>()!.Run();
    }
}
