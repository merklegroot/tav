using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raylib_cs;
using TavRay;
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
            var rayConsole = scope.ServiceProvider.GetRequiredService<RayConsoleWrapper>();

            Task appTask = Task.Run(() => app.Run());

            while (!appTask.IsCompleted)
            {
                if (Raylib.WindowShouldClose())
                {
                    Raylib.CloseWindow();
                    Environment.Exit(0);
                }

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.BLACK);
                int w = Raylib.GetScreenWidth();
                int h = Raylib.GetScreenHeight();
                var content = new Rectangle(16, 16, Math.Max(1, w - 32), Math.Max(1, h - 32));
                rayConsole.PumpFrame(content);
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
