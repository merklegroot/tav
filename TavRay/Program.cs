using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raylib_cs;
using TavRay.Registry;

namespace Tav;

public static class Program
{
    public static void Main(string[] args)
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        Raylib.InitWindow(screenWidth, screenHeight, "TavRay");
        Raylib.SetTargetFPS(60);
        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.Services.RegisterRay();

            using IHost host = builder.Build();
            using IServiceScope scope = host.Services.CreateScope();
            IApp app = scope.ServiceProvider.GetRequiredService<IApp>();

            Task appTask = Task.Run(() => app.Run());

            while (!appTask.IsCompleted)
            {
                if (Raylib.WindowShouldClose())
                {
                    Raylib.CloseWindow();
                    Environment.Exit(0);
                }

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.RAYWHITE);
                Raylib.EndDrawing();
            }

            appTask.GetAwaiter().GetResult();
        }
        finally
        {
            Raylib.CloseWindow();
        }
    }
}
