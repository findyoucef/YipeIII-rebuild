using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

/// <summary>Real Game.Run startup: no smoke mode, manual frame driver or graphics overrides.</summary>
internal sealed class StartupChecks : DrawableGameComponent
{
    private readonly string output;
    private readonly Type type;
    private readonly Stopwatch clock = new();
    private int frames;
    private Color[] titlePixels = [];
    private double titleTime;
    private bool completed;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private StartupChecks(Game game, string output) : base(game)
    { this.output = output; type = game.GetType(); }

    internal static int Run(string directory)
    {
        var output = Path.GetFullPath(directory);
        Directory.CreateDirectory(output);
        using var deadline = new CancellationTokenSource();
        // A display-link hang blocks the main thread, including Game.Exit().
        _ = Task.Delay(TimeSpan.FromSeconds(45), deadline.Token).ContinueWith(_ =>
        {
            Console.Error.WriteLine("FAIL: normal startup/display changes did not finish within 45 seconds.");
            Environment.Exit(1);
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        try
        {
            using var maps = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
            var catalog = MapCatalog.Load(maps);
            var save = Path.Combine(output, "fixture.y3m");
            var bytes = GameSaveCodec.Encode(new GameSession(catalog), catalog);
            File.WriteAllBytes(save, bytes);
            File.WriteAllText(Path.ChangeExtension(save, ".profile.json"), "{\"Version\":1,\"Sound\":false}");
            var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.TownGame", true)!;
            using var game = (Game)Activator.CreateInstance(type, [false, null, save])!;
            var observer = new StartupChecks(game, output);
            game.Components.Add(observer);
            game.Run();
            Require(observer.completed, "The normal game loop completed all startup phases");
            Require(File.ReadAllBytes(save).SequenceEqual(bytes), "Normal startup and display changes preserve the selected save");
            Console.WriteLine("Normal startup checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { deadline.Cancel(); }
    }

    public override void Draw(GameTime gameTime)
    {
        if (Get("loading") is not null) return;
        frames++;
        var graphics = (GraphicsDeviceManager)Get("graphics")!;
        switch (frames)
        {
            case 1:
                Require((bool)Get("paused")! && (string)Get("menuMessage")! == "Saved game restored.",
                    "Normal startup loads the selected adventure and renders the pause menu");
                Capture("loaded.png");
                // Exercise the actual ident-to-title path while retaining isolated storage.
                Set("paused", false); Set("studioSplashRemaining", 3.2d);
                break;
            case 35:
                Capture("ident.png");
                break;
            case 250:
                Require((bool)Get("titleScreen")!, "The studio ident naturally reaches the title screen");
                titlePixels = Capture("title.png"); titleTime = (double)Get("titleTime")!;
                clock.Start();
                break;
            case 370:
                clock.Stop();
                var fps = 120 / clock.Elapsed.TotalSeconds;
                Console.WriteLine($"Normal-loop presentation: {fps:F1} FPS across {clock.Elapsed.TotalSeconds:F2} seconds.");
                Require(fps is > 15 and < 75, "Presentation advances at a bounded rate with the normal game timer");
                Require((double)Get("titleTime")! > titleTime + .5 && !titlePixels.SequenceEqual(Capture("title-later.png")),
                    "Title animation advances across presented frames");
                graphics.PreferredBackBufferWidth = 960; graphics.PreferredBackBufferHeight = 540;
                graphics.ApplyChanges();
                break;
            case 415:
                Capture("resized.png");
                Require(GraphicsDevice.PresentationParameters.BackBufferWidth == 960, "Window resize keeps rendering");
                graphics.IsFullScreen = true; graphics.ApplyChanges();
                break;
            case 460:
                Capture("fullscreen.png");
                Require(graphics.IsFullScreen, "Fullscreen entry keeps presenting frames");
                graphics.IsFullScreen = false; graphics.ApplyChanges();
                break;
            case 505:
                Capture("windowed-again.png");
                Require(!graphics.IsFullScreen, "Returning to a window keeps presenting frames");
                completed = true; Game.Exit();
                break;
        }
    }

    private object? Get(string name) => type.GetField(name, Flags)!.GetValue(Game);
    private void Set(string name, object value) => type.GetField(name, Flags)!.SetValue(Game, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    private Color[] Capture(string name)
    {
        var pp = GraphicsDevice.PresentationParameters;
        using var image = new Texture2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
        var pixels = new Color[image.Width * image.Height];
        GraphicsDevice.GetBackBufferData(pixels); image.SetData(pixels);
        using var file = File.Create(Path.Combine(output, name)); image.SaveAsPng(file, image.Width, image.Height);
        return pixels;
    }
}
