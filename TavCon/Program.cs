using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tav.Registry;

namespace Tav;

public static class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.RegisterConsole();

        using IHost host = builder.Build();
        using IServiceScope scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IApp>().Run();
    }
}
