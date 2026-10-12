using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? outputRoot = null;
        var record = false;
        var opening = false;
        string? openingResume = null;
        string? lateInput = null;
        var lateResume = false;
        int[]? monsterCaptures = null;
        var monstersOnly = false;
        var snowOnly = false;
        var breedingOnly = false;
        var fireOnly = false;
        var earthOnly = false;
        var stoneOnly = false;
        var hellOnly = false;
        var maynardOnly = false;
        var outdoorsOnly = false;
        var cloudsOnly = false;
        var iceOnly = false;
        var waterOnly = false;
        var finalOnly = false;
        var menusOnly = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--help")
            {
                Console.WriteLine("Usage: Yipe.GraphicsChecks [--output DIRECTORY] [--record | --opening-route | --late-route CAMPAIGN_SAVES]");
                Console.WriteLine("Requires an active graphical desktop. --record additionally requires ffmpeg on PATH.");
                Console.WriteLine("--capture-monsters 7,9 selects monster clips; all registered monsters are still checked.");
                Console.WriteLine("--monsters-only runs focused monster and combat checks for local art iterations.");
                Console.WriteLine("--snow-only runs focused Zephyr snow-biome checks and captures.");
                Console.WriteLine("--outdoors-only runs focused surface-region, habitat and ambience checks.");
                Console.WriteLine("--final-only runs focused Final Cave checks and captures.");
                Console.WriteLine("--water-only runs focused Water Cave checks and captures.");
                Console.WriteLine("--ice-only runs focused Ice/Zephyr Cave checks and captures.");
                Console.WriteLine("--clouds-only runs focused cloud, sunlight, actor, weather and day/night checks.");
                Console.WriteLine("--maynard-only runs focused Maynard leaves and positional-audio checks.");
                Console.WriteLine("--hell-only runs focused Hell presentation and ambient-audio checks.");
                Console.WriteLine("--stone-only runs focused Stone Cave terrain, light and ambience checks.");
                Console.WriteLine("--earth-only runs focused Earth Cave material, lighting and ambience checks.");
                Console.WriteLine("--fire-only runs focused volcanic cave checks and captures.");
                Console.WriteLine("--menus-only runs focused pause/graphics/help/control checks and captures.");
                Console.WriteLine("--breeding-only runs focused breeding-ground terrain, light and ambience checks.");
                return 0;
            }
            if (args[i] == "--output" && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                outputRoot = args[++i];
            else if (args[i] == "--record") record = true;
            else if (args[i] == "--monsters-only") monstersOnly = true;
            else if (args[i] == "--snow-only") snowOnly = true;
            else if (args[i] == "--outdoors-only") outdoorsOnly = true;
            else if (args[i] == "--ice-only") iceOnly = true;
            else if (args[i] == "--final-only") finalOnly = true;
            else if (args[i] == "--water-only") waterOnly = true;
            else if (args[i] == "--clouds-only") cloudsOnly = true;
            else if (args[i] == "--maynard-only") maynardOnly = true;
            else if (args[i] == "--hell-only") hellOnly = true;
            else if (args[i] == "--stone-only") stoneOnly = true;
            else if (args[i] == "--earth-only") earthOnly = true;
            else if (args[i] == "--fire-only") fireOnly = true;
            else if (args[i] == "--menus-only") menusOnly = true;
            else if (args[i] == "--breeding-only") breedingOnly = true;
            else if (args[i] == "--capture-monsters" && i + 1 < args.Length)
            {
                var selected = new List<int>();
                foreach (var value in args[++i].Split(',', StringSplitOptions.TrimEntries))
                {
                    if (!int.TryParse(value, out var monster) || monster is < 1 or > 41)
                    { Console.Error.WriteLine("--capture-monsters requires comma-separated monster IDs from 1 to 41."); return 2; }
                    selected.Add(monster);
                }
                monsterCaptures = selected.Distinct().Order().ToArray();
            }
            else if (args[i] == "--opening-route") opening = true;
            else if (args[i] == "--opening-resume" && i + 1 < args.Length) { opening = true; openingResume = Path.GetFullPath(args[++i]); }
            else if (args[i] is "--late-route" or "--late-resume" && i + 1 < args.Length)
            { lateResume = args[i] == "--late-resume"; lateInput = Path.GetFullPath(args[++i]); }
            else
            {
                Console.Error.WriteLine("Usage: Yipe.GraphicsChecks [--output DIRECTORY] [--record | --opening-route | --late-route CAMPAIGN_SAVES]");
                return 2;
            }
        }
        if (finalOnly && (waterOnly || iceOnly || cloudsOnly || outdoorsOnly || maynardOnly || hellOnly || stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--final-only cannot be combined with other capture or route modes."); return 2; }
        if (waterOnly && (iceOnly || cloudsOnly || outdoorsOnly || maynardOnly || hellOnly || stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--water-only cannot be combined with other capture or route modes."); return 2; }
        if (iceOnly && (cloudsOnly || outdoorsOnly || maynardOnly || hellOnly || stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--ice-only cannot be combined with other capture or route modes."); return 2; }
        if (cloudsOnly && (outdoorsOnly || maynardOnly || hellOnly || stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--clouds-only cannot be combined with other capture or route modes."); return 2; }
        if (outdoorsOnly && (maynardOnly || hellOnly || stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--outdoors-only cannot be combined with other capture or route modes."); return 2; }
        if (opening && record) { Console.Error.WriteLine("Use --record for the visual tour or --opening-route for the earned replay."); return 2; }
        if (lateInput is not null && (opening || record)) { Console.Error.WriteLine("Run late-route checks separately from the opening or tour."); return 2; }
        if (monsterCaptures is not null && (opening || lateInput is not null))
        { Console.Error.WriteLine("--capture-monsters applies to the ordinary graphics checks."); return 2; }
        if (monstersOnly && (opening || lateInput is not null || record))
        { Console.Error.WriteLine("--monsters-only cannot be combined with route or tour modes."); return 2; }
        if (snowOnly && (monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--snow-only cannot be combined with other capture or route modes."); return 2; }
        if (breedingOnly && (snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--breeding-only cannot be combined with other capture or route modes."); return 2; }
        if (fireOnly && (breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--fire-only cannot be combined with other capture or route modes."); return 2; }
        if (menusOnly && (fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--menus-only cannot be combined with other capture or route modes."); return 2; }
        if (earthOnly && (menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--earth-only cannot be combined with other capture or route modes."); return 2; }
        if (stoneOnly && (earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--stone-only cannot be combined with other capture or route modes."); return 2; }
        if (hellOnly && (stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--hell-only cannot be combined with other capture or route modes."); return 2; }
        if (maynardOnly && (hellOnly || stoneOnly || earthOnly || menusOnly || fireOnly || breedingOnly || snowOnly || monstersOnly || opening || lateInput is not null || record || monsterCaptures is not null))
        { Console.Error.WriteLine("--maynard-only cannot be combined with other capture or route modes."); return 2; }
        try
        {
            var outputDirectory = Path.Combine(Path.GetFullPath(outputRoot ?? Path.GetTempPath()),
                "yipe-graphics-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(outputDirectory);
            Console.WriteLine("Graphics artifacts: " + outputDirectory);
            using var log = new StreamWriter(Path.Combine(outputDirectory, "checks.log")) { AutoFlush = true };
            log.WriteLine("UTC: " + DateTime.UtcNow.ToString("O"));
            log.WriteLine("Runtime: " + RuntimeInformation.FrameworkDescription);
            log.WriteLine("OS: " + RuntimeInformation.OSDescription);
            try
            {
                if (lateInput is not null)
                {
                    LateFrontendChecks.Run(lateInput, outputDirectory, log, lateResume);
                    if (!lateResume)
                    {
                        ContinueInFreshProcess("--late-resume", outputDirectory);
                        log.WriteLine("PASS fresh-process late frontend save/reload and earned finale.");
                    }
                }
                else if (opening)
                {
                    OpeningFrontendChecks.Run(outputDirectory, log, openingResume);
                    if (openingResume is null)
                    {
                        ContinueInFreshProcess("--opening-resume", outputDirectory);
                        log.WriteLine("PASS fresh-process frontend save/reload and earned completion.");
                    }
                }
                else RunChecks(outputDirectory, record, log, monsterCaptures, monstersOnly, snowOnly, breedingOnly, fireOnly, menusOnly, earthOnly, stoneOnly, hellOnly, maynardOnly, outdoorsOnly, cloudsOnly, iceOnly, waterOnly, finalOnly);
                return 0;
            }
            catch (Exception error)
            {
                log.WriteLine(error);
                Console.Error.WriteLine(error);
                return 1;
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void ContinueInFreshProcess(string mode, string outputDirectory)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate graphics runner.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(executable) == "dotnet") start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add(mode); start.ArgumentList.Add(Path.Combine(outputDirectory, "restart"));
        start.ArgumentList.Add("--output"); start.ArgumentList.Add(outputDirectory);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Cannot start frontend continuation.");
        child.WaitForExit();
        if (child.ExitCode != 0) throw new InvalidOperationException("Fresh-process frontend continuation failed.");
    }

    private static Process StartRecording(string outputDirectory)
    {
        var info = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-y", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "rgba", "-video_size", "1280x720", "-framerate", "60", "-i", "pipe:0", "-c:v", "libx264", "-preset", "fast", "-crf", "20", "-pix_fmt", "yuv420p", "-movflags", "+faststart", Path.Combine(outputDirectory, "tour.mp4") })
            info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start FFmpeg.");
    }

    private static void RunChecks(string outputDirectory, bool record, StreamWriter log, int[]? monsterCaptures, bool monstersOnly, bool snowOnly, bool breedingOnly, bool fireOnly, bool menusOnly, bool earthOnly, bool stoneOnly, bool hellOnly, bool maynardOnly, bool outdoorsOnly, bool cloudsOnly, bool iceOnly, bool waterOnly, bool finalOnly)
    {
        var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.TownGame", true)!;
        var saveDirectory = Path.Combine(outputDirectory, "test-saves");
        Directory.CreateDirectory(saveDirectory);
        var previewPath = Path.Combine(saveDirectory, "position.json");
        File.WriteAllText(previewPath, "{\"Version\":1,\"MapId\":17,\"X\":5,\"Y\":5}");
        var importedPath = Path.Combine(saveDirectory, "startup-import.y3m");
        using (var maps = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json")))
            new GameSaveStore(importedPath, MapCatalog.Load(maps)).ImportAsync(previewPath).GetAwaiter().GetResult();
        var startupTimer = Stopwatch.StartNew();
        using var game = (Game)Activator.CreateInstance(type, [false, null, importedPath])!;
        var startupGraphics = (GraphicsDeviceManager)type.GetField("graphics", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
        startupGraphics.SynchronizeWithVerticalRetrace = false;
        // The explicit selected-file path starts paused and loads only the isolated import.
        for (var i = 0; i < 5; i++) game.RunOneFrame();
        Log($"Content startup and five initial frames: {startupTimer.Elapsed.TotalSeconds:F2} s.");
        Log($"Graphics session active: {game.IsActive}");
        if (!game.IsActive) throw new Exception("Game must be active for the animation integration check.");
        var pendingStartup = type.GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game) as Task;
        if (pendingStartup is not null)
        {
            pendingStartup.GetAwaiter().GetResult();
            type.GetMethod("FinishStorage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);
        }
        var player = (PlayerMotion)Get("player");
        var map = (TownMap)Get("map");
        var session = (GameSession)Get("session");
        var randomState = session.Random.State;
        Set("saves", new GameSaveStore(Path.Combine(saveDirectory, "game.y3m"), (MapCatalog)Get("catalog")));
        Set("loading", null);
        var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var draw = type.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var restore = type.GetMethod("RestorePosition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var menu = type.GetMethod("ActivateMenu", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var finishStorage = type.GetMethod("FinishStorage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var checks = 0;
        Check((bool)Get("paused") && (string)Get("menuMessage") == "Saved game restored."
            && session.Location.Position == new MapPosition(5, 5) && session.Player.Gold == 0,
            "Explicit selected-file startup loads an imported preview into a paused, synchronized session");
        Set("paused", false);
        Set("pauseAmount", 0f);
        Check((int)Get("miniatureMode") == 2, "Miniature effect defaults to Strong");
        Check((bool)Get("scanlines"), "Scanlines default to on");
        Check((bool)Get("pixelLighting"), "Localized pixel lighting defaults to on");
        Check((int)Get("sunShaftStrength") == 1, "Sun shafts default to Subtle");
        Check((int)Get("cloudShadowStrength") == 1, "Cloud shadows default to Subtle");
        Check((int)Get("rainStrength") == 1, "Rain defaults to automatic Subtle showers");
        Check(!(bool)Get("screenShake"), "Screen shake defaults to Off");
        Check((int)Get("timeOfDayMode") == 0 && (double)Get("dayNightSeconds") is >= 300 and < 330,
            "Automatic day/night cycle is enabled by default, starting near midday as startup time elapses");
        if (finalOnly)
        {
            FinalCaveChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Final Cave run.");
            return;
        }
        if (waterOnly)
        {
            WaterCaveChecks.Run(game, outputDirectory, Check);
            RaftMotionChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Water Cave run.");
            return;
        }
        if (iceOnly)
        {
            IceCaveChecks.Run(game, outputDirectory, Check);
            IceMovementChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Ice Cave run.");
            return;
        }
        if (menusOnly)
        {
            HelpControlChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused menu/help/control run.");
            return;
        }
        if (snowOnly)
        {
            Set("timeOfDayMode", 1); SnowBiomeChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused snow-biome run.");
            return;
        }
        if (outdoorsOnly)
        {
            OutdoorPresentationChecks.Run(game, outputDirectory, Check);
            VillagePresentationChecks.Run(game.GraphicsDevice, Get("world"), Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused overworld run.");
            return;
        }
        if (maynardOnly)
        {
            MaynardAtmosphereChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Maynard run.");
            return;
        }
        if (hellOnly)
        {
            HellPresentationChecks.Run(game.GraphicsDevice, Get("world"), Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
            HellPresentationChecks.RunEnvironment(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Hell run.");
            return;
        }
        if (stoneOnly)
        {
            StoneCaveChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Stone Cave run.");
            return;
        }
        if (earthOnly)
        {
            EarthPresentationChecks.Run(game.GraphicsDevice, Get("world"), Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
            EarthEnvironmentChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Earth Cave run.");
            return;
        }
        if (fireOnly)
        {
            FireCaveChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused Fire Cave run.");
            return;
        }
        if (breedingOnly)
        {
            BreedingGroundChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused breeding-ground run.");
            return;
        }
        DayNightChecks.Run(game, outputDirectory, Check);
        // Auto is exercised above; hold Day for deterministic comparisons of other effects.
        Set("timeOfDayMode", 1);
        if (cloudsOnly)
        {
            RainChecks.Run(game, outputDirectory, Check);
            Set("rainStrength", 0);
            CloudShadowChecks.Run(game, outputDirectory, Check);
            Set("cloudShadowStrength", 0);
            SunShaftChecks.Run(game, outputDirectory, Check);
            SunActorChecks.Run(game, outputDirectory, Check);
            Log($"Passed {checks} visual integration checks. Focused cloud and sunlight run.");
            return;
        }
        FootstepChecks.Run(game, outputDirectory, Check);
        if (!monstersOnly) SnowBiomeChecks.Run(game, outputDirectory, Check);
        MonsterAnimationChecks.Run(game, outputDirectory, Check, monsterCaptures);
        CombatFeedbackChecks.Run(game, outputDirectory, Check);
        if (monstersOnly)
        {
            Log($"Passed {checks} visual integration checks. Focused monster and combat run.");
            return;
        }
        RainChecks.Run(game, outputDirectory, Check);
        Set("rainStrength", 0);
        CloudShadowChecks.Run(game, outputDirectory, Check);
        Set("cloudShadowStrength", 0);
        SunShaftChecks.Run(game, outputDirectory, Check);
        SunActorChecks.Run(game, outputDirectory, Check);
        Set("sunShaftStrength", 0);
        PixelLightingChecks.Run(game, outputDirectory, Check);
        Set("pixelLighting", false);
        Capture("yipe-defaults-play.png");
        Set("paused", true); Set("pauseAmount", 1f); Set("selected", 4); Set("selectionY", 40f);
        Capture("yipe-defaults-menu.png");
        Set("paused", false); Set("pauseAmount", 0f);
        Check(!(bool)Get("vignette"), "Vignette defaults to off");
        CheckVignette();
        HelpControlChecks.Run(game, outputDirectory, Check);
        // Isolate existing blur measurements from scanlines, then exercise the full mode cycle.
        Set("scanlines", false);
        Set("miniatureMode", 1);
        Set("graphicsMenuOpen", true); Set("selected", 2);
        menu.Invoke(game, null);
        Check((int)Get("miniatureMode") == 2, "Miniature menu advances to Strong");
        menu.Invoke(game, null);
        Check((int)Get("miniatureMode") == 0, "Miniature menu advances to Off");
        var off = DrawPixels();
        Capture("yipe-village-off.png");
        menu.Invoke(game, null);
        Check((int)Get("miniatureMode") == 1, "Miniature menu wraps to Subtle");
        var subtle = DrawPixels();
        Capture("yipe-village-subtle.png");
        Set("miniatureMode", 2);
        var strong = DrawPixels();
        Capture("yipe-village-strong.png");
        Check(!off.SequenceEqual(subtle) && !subtle.SequenceEqual(strong), "All three strengths render distinct images");
        Check(EdgeContrast(strong) < EdgeContrast(subtle) && EdgeContrast(subtle) < EdgeContrast(off),
            "Stronger settings progressively soften distant terrain detail");

        var effect = Get("miniature");
        var effectType = effect.GetType();
        var world = (RenderTarget2D)effectType.GetProperty("World")!.GetValue(effect)!;
        var result = (RenderTarget2D)effectType.GetProperty("Result")!.GetValue(effect)!;
        var apply = effectType.GetMethod("Apply")!;
        using var sharpReference = new RenderTarget2D(game.GraphicsDevice, 1280, 720);
        using var referenceBatch = new SpriteBatch(game.GraphicsDevice);
        foreach (var (x, y) in new[] { (5, 5), (4, 1), (10, 10) })
        {
            restore.Invoke(game, [new MapPosition(x, y)]);
            var actual = DrawPixels();
            var camera = (Vector2)Get("cameraPosition");
            var focus = (int)((player.VisualPosition.Y * 32 + 12 - camera.Y) * 4);
            game.GraphicsDevice.SetRenderTarget(sharpReference);
            referenceBatch.Begin(samplerState: SamplerState.PointClamp);
            referenceBatch.Draw(world, sharpReference.Bounds, Color.White);
            referenceBatch.End();
            game.GraphicsDevice.SetRenderTarget(null);
            var expected = new Color[1280 * 720];
            sharpReference.GetData(expected);
            var top = Math.Max(0, focus - 17 * 4);
            var bottom = Math.Min(719, focus + 17 * 4);
            Check(Enumerable.Range(top * 1280, (bottom - top + 1) * 1280).All(i => expected[i] == actual[i]),
                $"Focus band and hero stay pixel-sharp at map position {x},{y}");
            Capture($"yipe-village-focus-{x}-{y}.png");
        }
        // Uniform input catches dark borders, lost alpha, and non-normalized blur weights.
        game.GraphicsDevice.SetRenderTarget(world);
        game.GraphicsDevice.Clear(Color.White);
        apply.Invoke(effect, [referenceBatch, 90f, true, 1f]);
        game.GraphicsDevice.SetRenderTarget(null);
        var flat = new Color[1280 * 720];
        result.GetData(flat);
        Check(flat.All(c => c.A == 255 && Math.Abs(c.R - 255) <= 2 && Math.Abs(c.G - 255) <= 2 && Math.Abs(c.B - 255) <= 2),
            "Blur preserves brightness and fills every border pixel without transparent seams");
        using (var stripe = new Texture2D(game.GraphicsDevice, 320, 180))
        {
            var stripePixels = new Color[320 * 180];
            for (var i = 0; i < stripePixels.Length; i++) stripePixels[i] = i % 2 == 0 ? new Color(40, 40, 40) : new Color(200, 200, 200);
            stripe.SetData(stripePixels);
            game.GraphicsDevice.SetRenderTarget(world);
            referenceBatch.Begin(samplerState: SamplerState.PointClamp);
            referenceBatch.Draw(stripe, Vector2.Zero, Color.White);
            referenceBatch.End();
            apply.Invoke(effect, [referenceBatch, 90f, false, 1f]);
            game.GraphicsDevice.SetRenderTarget(null);
            var gradient = new Color[1280 * 720];
            result.GetData(gradient);
            var contrast = Enumerable.Range(0, 360).Select(y => (int)gradient[y * 1280 + 639].R - gradient[y * 1280 + 640].R).ToArray();
            Log($"Gradient contrast: edge={contrast[0]}, center={contrast[^1]}, levels={contrast.Distinct().Count()}, max row jump={contrast.Zip(contrast.Skip(1), (a, b) => Math.Abs(a - b)).Max()}");
            Check(contrast[0] < contrast[^1] && contrast.Distinct().Count() >= 12, "Falloff contains a gradual range of contrast levels");
            Check(contrast.Zip(contrast.Skip(1), (a, b) => Math.Abs(a - b)).Max() <= 3, "No abrupt contrast boundary between presentation rows");
            apply.Invoke(effect, [referenceBatch, 90.25f, false, 1f]);
            game.GraphicsDevice.SetRenderTarget(null);
            var moved = new Color[gradient.Length];
            result.GetData(moved);
            Check(gradient.Zip(moved, (a, b) => Math.Abs(a.R - b.R)).Max() <= 3, "Subpixel focus movement does not pop between blur bands");
        }
        restore.Invoke(game, [new MapPosition(5, 5)]);
        Set("paused", true);
        Set("pauseAmount", 1f);
        Set("selectionY", 20f);
        Set("miniatureMode", 0);
        var menuOff = DrawPixels();
        Set("miniatureMode", 2);
        var menuStrong = DrawPixels();
        Check((from y in Enumerable.Range(6 * 4, 168 * 4)
               where y < 64 * 4 || y >= 71 * 4
               from x in Enumerable.Range(34 * 4, 252 * 4)
               select y * 1280 + x).All(i => menuOff[i] == menuStrong[i]),
            "Opaque pause panel and typography remain untouched by the blur");
        Capture("yipe-village-menu.png");
        Set("graphicsMenuOpen", false); Set("selected", 0); Set("selectionY", 0f);
        Set("paused", false);
        Set("pauseAmount", 0f);
        Set("message", "Beyond Maynard\nThis area is not available in the town preview.");
        Set("messageRemaining", 4d);
        Set("miniatureMode", 0);
        var messageOff = DrawPixels();
        Set("miniatureMode", 2);
        var messageStrong = DrawPixels();
        Check((from y in Enumerable.Range(142 * 4, 28 * 4) from x in Enumerable.Range(12 * 4, 296 * 4) select y * 1280 + x)
            .All(i => messageOff[i] == messageStrong[i]), "Location message overlay stays sharp");
        Set("messageRemaining", 0d);
        Set("miniatureMode", 1);

        Capture("yipe-village-play.png");

        // Inspect the actual hero rendering against its opaque sprite mask on a transparent target.
        var townRenderer = Get("town");
        var townType = townRenderer.GetType();
        var drawHero = townType.GetMethod("DrawHero", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var heroAtlas = (Texture2D)townType.GetField("hero", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(townRenderer)!;
        var heroPixels = new Color[heroAtlas.Width * heroAtlas.Height];
        heroAtlas.GetData(heroPixels);
        using var heroTarget = new RenderTarget2D(game.GraphicsDevice, 416, 384);
        using var heroBatch = new SpriteBatch(game.GraphicsDevice);
        var exposurePlayer = new PlayerMotion(new(5, 5));
        foreach (var direction in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
        {
            exposurePlayer.SnapTo(new(5, 5));
            exposurePlayer.MoveTo(new(5 + direction.X, 5 + direction.Y));
            Check(HeroTrail().Count == 0, $"No pre-motion trail for {direction}");
            exposurePlayer.Update(PlayerMotion.StepDuration / 2);
            var trail = HeroTrail();
            Check(trail.Count > 0 && trail.Max(p => p.Color.A) <= 55, $"Faint trail appears for {direction}");
            var body = HeroMask();
            Check(trail.All(p => Enumerable.Range(1, 3).Any(offset =>
                body.Contains(new Point(p.X + direction.X * offset, p.Y + direction.Y * offset)))),
                $"Trail stays within three pixels behind {direction}");
            var peakAlpha = trail.Sum(p => (int)p.Color.A);
            exposurePlayer.Update(PlayerMotion.StepDuration * 5 / 12);
            Check(HeroTrail().Sum(p => (int)p.Color.A) < peakAlpha, $"Trail fades before stopping {direction}");
            exposurePlayer.Update(PlayerMotion.StepDuration / 6);
            Check(HeroTrail().Count == 0, $"Completed step clears trail for {direction}");
        }
        exposurePlayer.SnapTo(new(5, 5));
        exposurePlayer.Face(new(1, 0));
        Check(HeroTrail().Count == 0, "Facing a blocked direction without moving creates no trail");
        exposurePlayer.MoveTo(new(6, 5));
        exposurePlayer.Update(PlayerMotion.StepDuration / 2);
        var frozenTrail = HeroTrail();
        Check(frozenTrail.SequenceEqual(HeroTrail()), "Drawing repeatedly without updates leaves exposure unchanged");
        exposurePlayer.SnapTo(new(5, 4));
        Check(HeroTrail().Count == 0, "Loading/resetting mid-step clears the trail");
        restore.Invoke(game, [new MapPosition(5, 5)]);
        player.MoveTo(new(6, 5));
        Update(0.06);
        Capture("yipe-village-moving.png");
        restore.Invoke(game, [new MapPosition(5, 5)]);

        List<(int X, int Y, Color Color)> HeroTrail()
        {
            game.GraphicsDevice.SetRenderTarget(heroTarget);
            game.GraphicsDevice.Clear(Color.Transparent);
            heroBatch.Begin(samplerState: SamplerState.PointClamp);
            drawHero.Invoke(townRenderer, [heroBatch, exposurePlayer, 0d, false, true]);
            heroBatch.End();
            game.GraphicsDevice.SetRenderTarget(null);
            var data = new Color[416 * 384];
            heroTarget.GetData(data);
            var body = HeroMask();
            var feet = new Point((int)MathF.Round(exposurePlayer.VisualPosition.X * 32) + 16,
                (int)MathF.Round(exposurePlayer.VisualPosition.Y * 32) + 25);
            var shadow = new Rectangle(feet.X - 5, feet.Y - 1, 10, 2);
            var footsteps = FootstepPixels(exposurePlayer);
            var trail = new List<(int X, int Y, Color Color)>();
            for (var y = 0; y < 384; y++)
                for (var x = 0; x < 416; x++)
                {
                    var color = data[y * 416 + x];
                    if (color.A != 0 && !body.Contains(new Point(x, y)) && !shadow.Contains(x, y) && footsteps[y * 416 + x].A == 0)
                        trail.Add((x, y, color));
                }
            foreach (var point in body)
            {
                if (data[point.Y * 416 + point.X].A != 255) throw new Exception("Hero's opaque center became translucent.");
            }
            return trail;
        }
        Color[] FootstepPixels(PlayerMotion actor)
        {
            game.GraphicsDevice.SetRenderTarget(heroTarget);
            game.GraphicsDevice.Clear(Color.Transparent);
            heroBatch.Begin(samplerState: SamplerState.PointClamp);
            var feet = new Vector2(MathF.Round(actor.VisualPosition.X * 32) + 16,
                MathF.Round(actor.VisualPosition.Y * 32) + 25);
            townType.GetMethod("DrawFootsteps", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(townRenderer, [heroBatch, actor, feet]);
            heroBatch.End();
            game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416 * 384];
            heroTarget.GetData(pixels);
            return pixels;
        }
        HashSet<Point> HeroMask()
        {
            var frame = exposurePlayer.IsMoving ? exposurePlayer.AnimationFrame : 4;
            var origin = new Point((int)MathF.Round(exposurePlayer.VisualPosition.X * 32),
                (int)MathF.Round(exposurePlayer.VisualPosition.Y * 32) - 3);
            var mask = new HashSet<Point>();
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    if (heroPixels[(exposurePlayer.FacingRow * 32 + y) * heroAtlas.Width + frame * 32 + x].A != 0)
                        mask.Add(new Point(origin.X + x, origin.Y + y));
            return mask;
        }
        // Exercise the scenery through the actual graphics device and existing renderer boundaries.
        var sceneryPlayer = new PlayerMotion(new(5, 5));
        Check(FootstepPixels(sceneryPlayer).All(c => c.A == 0), "Standing creates no footstep particles");
        sceneryPlayer.MoveTo(new(6, 5));
        sceneryPlayer.Update(PlayerMotion.StepDuration / 2);
        var dustPixels = FootstepPixels(sceneryPlayer);
        Check(dustPixels.Any(c => c.A != 0) && dustPixels.Count(c => c.A != 0) <= 6
            && dustPixels.All(c => c.A <= 102), "Paved routes produce sparse low-opacity dust");
        sceneryPlayer.SnapTo(new(4, 5)); sceneryPlayer.MoveTo(new(5, 5));
        sceneryPlayer.Update(PlayerMotion.StepDuration / 2);
        var grassPixels = FootstepPixels(sceneryPlayer);
        Check(grassPixels.Any(c => c.A != 0) && grassPixels.Where(c => c.A != 0).All(c => c.G > c.R),
            "Grass produces green blade disturbance instead of road dust");
        Check(grassPixels.SequenceEqual(FootstepPixels(sceneryPlayer)), "Paused footsteps do not evolve between draws");
        sceneryPlayer.SnapTo(new(1, 6)); sceneryPlayer.MoveTo(new(2, 6));
        sceneryPlayer.Update(PlayerMotion.StepDuration / 2);
        Check(FootstepPixels(sceneryPlayer).All(c => c.A == 0), "Wooden bridges produce no grass or dust");
        sceneryPlayer.SnapTo(new(5, 5));
        Check(FootstepPixels(sceneryPlayer).All(c => c.A == 0), "Loading or resetting clears footstep effects");
        var waterMask = (bool[])townType.GetField("waterMask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(townRenderer)!;
        var visited = new HashSet<int>(); var queue = new Queue<int>();
        queue.Enqueue(Array.FindIndex(waterMask, v => v));
        while (queue.TryDequeue(out var i))
        {
            if (!visited.Add(i)) continue;
            foreach (var p in new[] { new Point(i % 416 - 1, i / 416), new Point(i % 416 + 1, i / 416), new Point(i % 416, i / 416 - 1), new Point(i % 416, i / 416 + 1) })
                if (p.X >= 0 && p.X < 416 && p.Y >= 0 && p.Y < 384 && waterMask[p.Y * 416 + p.X] && !visited.Contains(p.Y * 416 + p.X)) queue.Enqueue(p.Y * 416 + p.X);
        }
        Check(visited.Count == waterMask.Count(v => v), "River is one connected water surface through both bends and bridges");
        Check(waterMask[112] && waterMask[383 * 416 + 80] && waterMask[112 * 416 + 112] && waterMask[208 * 416 + 80],
            "River reaches both map edges and flows beneath both bridges");
        Check(Enumerable.Range(0, 96).Select(y => waterMask.Skip(y * 416).Take(416).Count(v => v)).Distinct().Count() > 3,
            "Natural river width varies along the banks");
        var riverA = Layer("DrawRiver", 0d); var riverB = Layer("DrawRiver", 1d);
        var riverChanges = Enumerable.Range(0, riverA.Length).Where(i => riverA[i] != riverB[i]).ToArray();
        Check(riverChanges.Length > 0 && riverChanges.Length < 400 && riverChanges.All(i => waterMask[i]),
            "Moving ripples and foam remain inside the river and leave banks untouched");
        Check(riverA.SequenceEqual(Layer("DrawRiver", 0d)), "River animation is deterministic and freezes with scene time");
        // Reflection is a rendered water effect, including approaches and both bridge decks.
        var reflectionActor = new PlayerMotion(new(3, 3));
        foreach (var bridge in new[] { new MapPosition(3, 3), new MapPosition(2, 6) })
        {
            foreach (var facing in new[] { new Point(0, 1), new Point(-1, 0), new Point(1, 0), new Point(0, -1) })
            {
                reflectionActor.SnapTo(bridge); reflectionActor.Face(facing);
                var reflected = Layer("DrawHeroReflection", reflectionActor, 1d);
                var visible = Enumerable.Range(0, reflected.Length).Where(i => reflected[i].A != 0).ToArray();
                Check(visible.Length > 25 && visible.All(i => waterMask[i]) && visible.All(i => reflected[i].A <= 72),
                    $"Bridge {bridge.X},{bridge.Y} reflection faces {facing} and stays faint and inside water");
            }
            reflectionActor.SnapTo(bridge); reflectionActor.Face(new(1, 0));
            var stillReflection = Layer("DrawHeroReflection", reflectionActor, 1d);
            Check(stillReflection.SequenceEqual(Layer("DrawHeroReflection", reflectionActor, 1d)),
                $"Bridge {bridge.X},{bridge.Y} reflection freezes at a fixed scene time");
            Check(!stillReflection.SequenceEqual(Layer("DrawHeroReflection", reflectionActor, 2d)),
                $"Bridge {bridge.X},{bridge.Y} reflection has gentle animated ripples");
            var withReflection = Layer("Draw", reflectionActor, 1d, false, 0);
            var originalMask = (bool[])waterMask.Clone();
            Color[] withoutReflection;
            try { Array.Fill(waterMask, false); withoutReflection = Layer("Draw", reflectionActor, 1d, false, 0); }
            finally { originalMask.CopyTo(waterMask, 0); }
            var townAtlas = (Texture2D)townType.GetField("town", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(townRenderer)!;
            var townPixels = new Color[townAtlas.Width * townAtlas.Height]; townAtlas.GetData(townPixels);
            var deckClear = true;
            for (var sy = 0; sy < 48; sy++) for (var sx = 0; sx < 48; sx++)
            {
                if (townPixels[(96 + sy) * townAtlas.Width + 48 + sx].A == 0) continue;
                var i = (bridge.Y * 32 - 8 + sy) * 416 + bridge.X * 32 - 8 + sx;
                deckClear &= withReflection[i] == withoutReflection[i];
            }
            Check(deckClear, "Bridge deck stays opaque over the reflection");
            Check(Enumerable.Range(0, stillReflection.Length).Count(i => stillReflection[i].A != 0 && withReflection[i] != withoutReflection[i]) > 25,
                "Part of the character reflection remains visible beyond the bridge deck");
            foreach (var direction in new[] { -1, 1 })
            {
                reflectionActor.SnapTo(new(bridge.X - direction, bridge.Y));
                reflectionActor.MoveTo(bridge);
                var accepted = Layer("DrawHeroReflection", reflectionActor, 1d);
                Check(accepted.All(c => c.A == 0), "Accepting a step onto a bridge does not pop its reflection onto the previous bank");
                var maximumAlpha = new List<int>();
                for (var sample = 0; sample < 4; sample++)
                {
                    reflectionActor.Update(PlayerMotion.StepDuration / 4);
                    var pixels = Layer("DrawHeroReflection", reflectionActor, 1d);
                    Check(Enumerable.Range(0, pixels.Length).All(i => pixels[i].A == 0 || waterMask[i]), "Crossing reflection never spills onto the bank");
                    maximumAlpha.Add(pixels.Max(c => (int)c.A));
                }
                Log($"Reflection alpha {bridge} dir={direction}: {string.Join(",", maximumAlpha)}");
                Check(maximumAlpha[0] < maximumAlpha[^1] && maximumAlpha[^1] > 0,
                    "Reflection becomes visible as the character reaches water from either bank");
                reflectionActor.MoveTo(new(bridge.X + direction, bridge.Y));
                reflectionActor.Update(PlayerMotion.StepDuration);
                Check(Layer("DrawHeroReflection", reflectionActor, 1d).All(c => c.A == 0), "Reflection clears after leaving the bridge");
            }
        }
        reflectionActor.SnapTo(new(5, 5));
        Check(Layer("DrawHeroReflection", reflectionActor, 1d).All(c => c.A == 0), "Load or reset inland clears the reflection immediately");
        sceneryPlayer.SnapTo(new(6, 7)); sceneryPlayer.Face(new(1, 0));
        var away = Layer("DrawGroundDetails", sceneryPlayer, 1d);
        sceneryPlayer.Face(new(0, -1));
        var toward = Layer("DrawGroundDetails", sceneryPlayer, 1d);
        var cue = new Rectangle(6 * 32 + 10, 6 * 32 + 28, 12, 3);
        var cueChanges = Enumerable.Range(0, away.Length).Where(i => away[i] != toward[i]).ToArray();
        Check(cueChanges.Length > 0 && cueChanges.All(i => cue.Contains(i % 416, i / 416)),
            "Facing a nearby shop changes only its small doorstep cue");
        sceneryPlayer.SnapTo(new(6, 8)); sceneryPlayer.MoveTo(new(6, 7));
        sceneryPlayer.Update(PlayerMotion.StepDuration / 2);
        Check(away.SequenceEqual(Layer("DrawGroundDetails", sceneryPlayer, 1d)), "Entrance cue stays hidden during movement");
        foreach (var sign in Enumerable.Range(8, 5))
        {
            var intact = true;
            foreach (var time in new[] { 0d, 1d, 3d })
            {
                var pixels = Layer("DrawProp", sign, new Vector2(120, 100), time);
                foreach (var x in new[] { 122, 125 })
                {
                    var bottom = pixels[99 * 416 + x];
                    intact &= bottom.A == 255 && bottom.R <= 60 && bottom.G <= 60 && bottom.B <= 60;
                }
            }
            Check(intact, $"Sign {sign} retains its dark bottom outline while swaying");
        }
        var propsA = Layer("DrawShopDetails", 6, 6, 0, 0d);
        var propsB = Layer("DrawShopDetails", 6, 6, 0, 3d);
        Check(!propsA.SequenceEqual(propsB), "Hanging shop signs sway with scene time");
        Check(propsA.SequenceEqual(Layer("DrawShopDetails", 6, 6, 0, 0d)), "Shop props freeze while paused");
        var paved = new Color[416 * 384];
        game.GraphicsDevice.SetRenderTarget(heroTarget); game.GraphicsDevice.Clear(Color.Transparent);
        heroBatch.Begin(samplerState: SamplerState.PointClamp);
        for (var y = 0; y < TownMap.Height; y++) for (var x = 0; x < TownMap.Width; x++)
        {
            var connections = map.Tile(new(x, y)) switch { 8 => 5, 22 => 10, 25 => 3, 26 => 12, 1 or 5 or 6 or 7 or 10 or 48 => 15, 57 => 4, _ => 0 };
            if (connections != 0) townType.GetMethod("DrawPath", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(townRenderer, [heroBatch, x, y, connections]);
        }
        heroBatch.End(); game.GraphicsDevice.SetRenderTarget(null); heroTarget.GetData(paved);
        // Attached boards can hang over the edge of a path; verify their facades and symbols below.
        var groundProps = ((int Sprite, int X, int Y)[])townType.GetField("GroundDetails", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var groundOverlap = false;
        foreach (var prop in groundProps)
        {
            var pixels = Layer("DrawProp", prop.Sprite, new Vector2(prop.X, prop.Y), 1d);
            groundOverlap |= Enumerable.Range(0, pixels.Length).Any(i => pixels[i].A != 0 && paved[i].A != 0);
        }
        Check(!groundOverlap, "Authored flowers and foliage leave paved walking routes clear");

        Color[] Layer(string name, params object[] arguments)
        {
            game.GraphicsDevice.SetRenderTarget(heroTarget); game.GraphicsDevice.Clear(Color.Transparent);
            heroBatch.Begin(samplerState: SamplerState.PointClamp);
            townType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(townRenderer, [heroBatch, .. arguments]);
            heroBatch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; heroTarget.GetData(pixels); return pixels;
        }

        // Compare facades against an otherwise identical frame with decorative textures hidden.
        var detailsAtlas = (Texture2D)townType.GetField("details", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(townRenderer)!;
        var originalDetails = new Color[detailsAtlas.Width * detailsAtlas.Height]; detailsAtlas.GetData(originalDetails);
        var buildingAtlas = (Texture2D)townType.GetField("town", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(townRenderer)!;
        var buildingPixels = new Color[buildingAtlas.Width * buildingAtlas.Height]; buildingAtlas.GetData(buildingPixels);
        var stillPlayer = new PlayerMotion(new(5, 5));
        Color[] bareFacades;
        try
        {
            detailsAtlas.SetData(new Color[originalDetails.Length]);
            bareFacades = Layer("Draw", stillPlayer, 1d, false, 0);
        }
        finally { detailsAtlas.SetData(originalDetails); }
        var decoratedFacades = Layer("Draw", stillPlayer, 1d, false, 0);
        for (var y = 0; y < TownMap.Height; y++) for (var x = 0; x < TownMap.Width; x++)
        {
            var sprite = map.Tile(new(x, y)) switch { 1 => 0, 5 => 1, 6 => 2, 7 => 3, 10 => 4, _ => -1 };
            if (sprite < 0) continue;
            var clear = true;
            for (var sy = 24; sy < 44; sy++) for (var sx = 0; sx < 48; sx++)
            {
                if (buildingPixels[(sprite / 3 * 48 + sy) * buildingAtlas.Width + sprite % 3 * 48 + sx].A != 255) continue;
                var i = (y * 32 - 16 + sy) * 416 + x * 32 - 8 + sx;
                clear &= bareFacades[i] == decoratedFacades[i];
            }
            Check(clear, $"Shop {sprite} windows, doors, and facade stay unobscured by its sign");
            var isolatedSign = Layer("DrawShopDetails", x, y, sprite, 1d);
            var opaque = Enumerable.Range(0, isolatedSign.Length).Where(i => isolatedSign[i].A == 255).ToArray();
            var left = opaque.Min(i => i % 416); var top = opaque.Min(i => i / 416);
            var board = opaque.Where(i => i % 416 >= left + 3 && i / 416 >= top + 6).ToArray();
            Check(board.Length > 50 && board.All(i => isolatedSign[i] == decoratedFacades[i]),
                $"Shop {sprite} keeps its hanging board and symbol visible beside the facade");

        }

        // Isolate the cave in the rendered map to check its threshold against the mountain tile.
        var withoutCavePixels = (Color[])buildingPixels.Clone();
        for (var y = 96; y < 144; y++) Array.Clear(withoutCavePixels, y * buildingAtlas.Width, 48);
        Color[] withoutCave;
        try
        {
            buildingAtlas.SetData(withoutCavePixels);
            withoutCave = Layer("Draw", stillPlayer, 1d, false, 0);
        }
        finally { buildingAtlas.SetData(buildingPixels); }
        var caveChanged = Enumerable.Range(0, withoutCave.Length).Where(i => withoutCave[i] != decoratedFacades[i]).ToArray();
        Check(caveChanged.Length > 100 && caveChanged.Max(i => i / 416) == 63,
            "Cave threshold reaches the last row of its mountain tile, with no gap below");

        // Render a short deterministic tour through the actual Update/Draw paths.
        Point[] route = [new(0, -1), new(0, -1), new(-1, 0), new(0, -1), new(1, 0), new(1, 0), new(0, 1), new(0, 1), new(1, 0), new(0, 1), new(0, 1), new(0, 1), new(-1, 0), new(-1, 0), new(0, -1), new(0, -1)];
        route = [.. route, .. route];
        var routeIndex = 0;
        using var encoder = record ? StartRecording(outputDirectory) : null;
        var encoderErrors = encoder?.StandardError.ReadToEndAsync();
        var videoPixels = record ? new Color[1280 * 720] : null;
        try
        {
            for (var frame = 0; frame < 360; frame++)
            {
                DismissOverlay();
                if (!player.IsMoving && routeIndex < route.Length)
                {
                    Set("pendingDirection", route[routeIndex++]);
                }
                Update(1.0 / 60);
                draw.Invoke(game, [new GameTime()]);
                if (encoder is not null)
                {
                    game.GraphicsDevice.GetBackBufferData(videoPixels!);
                    encoder.StandardInput.BaseStream.Write(MemoryMarshal.AsBytes(videoPixels.AsSpan()));
                }
                if (frame is 2 or 30 or 80 or 120) Capture($"yipe-village-frame-{frame}.png");
            }
        }
        finally
        {
            if (encoder is not null)
            {
                encoder.StandardInput.Close();
                if (!encoder.WaitForExit(10000))
                {
                    encoder.Kill(entireProcessTree: true);
                    throw new TimeoutException("FFmpeg did not finish writing the recording.");
                }
            }
        }
        if (encoder is not null && encoder.ExitCode != 0)
            throw new InvalidOperationException("FFmpeg failed: " + encoderErrors!.GetAwaiter().GetResult());
        DismissOverlay();
        Check(player.VisualPosition.X >= 0 && player.VisualPosition.X < TownMap.Width, "Tour stays inside the map");
        restore.Invoke(game, [new MapPosition(5, 5)]);
        Set("pendingDirection", new Point(0, -1));
        Update(0);
        Update(0.04);
        var halfway = player.VisualPosition;
        var animationTime = (double)Get("sceneTime");
        Set("paused", true);
        Set("pendingDirection", new Point(1, 0));
        Update(0.2);
        Check(player.VisualPosition == halfway && (double)Get("sceneTime") == animationTime, "Pause freezes motion and environmental time");
        Check((Point)Get("pendingDirection") == Point.Zero, "Pause clears buffered movement");
        Capture("yipe-village-pause.png");
        Set("paused", false);
        Update(PlayerMotion.StepDuration);
        Check(!player.IsMoving && player.VisualPosition == new Vector2(5, 4), "Resume finishes the pending visual step");

        // Save and load through the isolated store installed before any test input.
        restore.Invoke(game, [new MapPosition(5, 5)]);
        Set("pendingDirection", new Point(1, 0));
        Update(0);
        Update(0.04);
        Check(session.Location.Position == new MapPosition(6, 5) && player.IsMoving,
            "Session accepts the logical destination before the visual step finishes");
        session.Player.Gold = 321;
        session.Player.Health = 7;
        session.Player.Inventory[2] = 5;
        session.Player.QuestFlags[9] = true;
        session.Player.OpenedChests[16] = 3;
        session.DefeatedMonsterPlacements.Add(5);
        session.Random.Next();
        randomState = session.Random.State;
        Set("selected", 1);
        menu.Invoke(game, null);
        ((Task)Get("saving")).GetAwaiter().GetResult();
        finishStorage.Invoke(game, null);
        var savedSession = session;
        var savedRenderer = townRenderer;
        session.Player.Gold = 0; session.Player.Health = 1; session.Player.Inventory[2] = 0;
        session.Player.QuestFlags[9] = false; session.Player.OpenedChests[16] = 0;
        session.DefeatedMonsterPlacements.Clear(); session.Random.Next();
        restore.Invoke(game, [new MapPosition(5, 4)]);
        Set("selected", 2);
        menu.Invoke(game, null);
        ((Task)Get("loading")).GetAwaiter().GetResult();
        finishStorage.Invoke(game, null);
        session = (GameSession)Get("session");
        map = (TownMap)Get("map");
        townRenderer = Get("town");
        Check(player.Position == new MapPosition(6, 5) && player.VisualPosition == new Vector2(6, 5) && !player.IsMoving,
            "Saving during animation and loading restores the accepted destination without a stale tween");

        Check(session.Location.Position == player.Position,
            "Loading synchronizes the logical session and animated player");
        Check(session.Player.Gold == 321 && session.Player.Health == 7 && session.Player.Inventory[2] == 5
            && session.Player.QuestFlags[9] && session.Player.OpenedChests[16] == 3
            && session.DefeatedMonsterPlacements.SequenceEqual(new ushort[] { 5 }) && session.Random.State == randomState,
            "Pause-menu full load restores character, inventory, progression, chest/defeat flags and RNG");
        Check(!ReferenceEquals(savedSession, session) && !ReferenceEquals(savedRenderer, townRenderer)
            && heroAtlas.IsDisposed && ReferenceEquals(townType.GetField("map", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(townRenderer), map),
            "Successful load rebinds the renderer to the new session and disposes the old textures");

        var gameSavePath = Path.Combine(saveDirectory, "game.y3m");
        var goodSaveBytes = File.ReadAllBytes(gameSavePath);
        Set("pendingDirection", new Point(1, 0)); Update(0); Update(0.04);
        var beforeRejectedLoad = GameSaveCodec.Encode(session, (MapCatalog)Get("catalog"));
        var visualBeforeRejectedLoad = player.VisualPosition;
        var corruptSave = (byte[])goodSaveBytes.Clone(); corruptSave[^1] ^= 1;
        File.WriteAllBytes(gameSavePath, corruptSave);
        Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
        Check(ReferenceEquals(session, Get("session")) && ReferenceEquals(townRenderer, Get("town"))
            && player.VisualPosition == visualBeforeRejectedLoad && player.IsMoving
            && GameSaveCodec.Encode(session, (MapCatalog)Get("catalog")).SequenceEqual(beforeRejectedLoad),
            "A corrupt full save leaves the live session, renderer and in-flight animation untouched");
        Check(((string)Get("menuMessage")).StartsWith("Could not load"), "Load failure is reported in the pause overlay");

        var outsideTown = new GameSession((MapCatalog)Get("catalog"), location: new() { MapId = 57, MapLayer = 1 });
        File.WriteAllBytes(gameSavePath, GameSaveCodec.Encode(outsideTown, (MapCatalog)Get("catalog")));
        Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
        Check(((GameSession)Get("session")).CurrentMap.Id == 57 && player.Position == outsideTown.Location.Position,
            "A wider-world save opens in its cave with the animated player synchronized");
        Capture("yipe-earth-cave.png");
        var generatedSession = new GameSession((MapCatalog)Get("catalog"), location: new()
        { MapId = 118, RegionX = -3, RegionY = -1, AlternateMapSet = true, Position = new(1, 1) });
        var generatedBytes = GameSaveCodec.Encode(generatedSession, (MapCatalog)Get("catalog"));
        var expectedGenerated = GameSaveCodec.Decode(generatedBytes, (MapCatalog)Get("catalog"));
        File.WriteAllBytes(gameSavePath, generatedBytes);
        Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
        Check(((GameSession)Get("session")).CurrentMap.Id == 118 && ((GameSession)Get("session")).Random.State == expectedGenerated.Random.State,
            "Generated-map loading advances the restored RNG exactly once, independently of rendering");
        Capture("yipe-generated-map.png");
        RestoreSavedTown();
        File.WriteAllText(gameSavePath, "{\"Version\":1,\"MapId\":17,\"X\":5,\"Y\":5}");
        Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
        Check(ReferenceEquals(session, Get("session")) && session.Player.Gold == 321
            && ((string)Get("menuMessage")).StartsWith("Could not load"),
            "Loading raw preview JSON cannot silently replace earned character progress");
        var unsupportedPosition = new GameSession((MapCatalog)Get("catalog"), location: new() { Position = new(6, 6) });
        File.WriteAllBytes(gameSavePath, GameSaveCodec.Encode(unsupportedPosition, (MapCatalog)Get("catalog")));
        Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
        Check(((GameSession)Get("session")).Location.Position == new MapPosition(6, 6) && player.Position == new MapPosition(6, 6),
            "The full game can display saved positions beyond the former preview restriction");
        RestoreSavedTown();
        File.Delete(gameSavePath);
        Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
        Check(ReferenceEquals(session, Get("session")) && ((string)Get("menuMessage")) == "No saved game yet.",
            "Missing-save feedback preserves the live game");
        File.WriteAllBytes(gameSavePath, goodSaveBytes);
        session.Player.OpenedChests[0] = 16;
        Set("selected", 1); menu.Invoke(game, null); FinishPendingStorage("saving");
        session.Player.OpenedChests[0] = 0;
        Check(((string)Get("menuMessage")).StartsWith("Could not save") && File.ReadAllBytes(gameSavePath).SequenceEqual(goodSaveBytes),
            "Pause-menu save validation failure preserves the previous successful file");
        restore.Invoke(game, [new MapPosition(6, 5)]);

        void FinishPendingStorage(string field)
        {
            var task = (Task)Get(field);
            if (Task.WhenAny(task, Task.Delay(5000)).GetAwaiter().GetResult() != task)
                throw new TimeoutException("Storage did not finish.");
            finishStorage.Invoke(game, null);
        }
        // Exercise real game dispatch and rendering for each newly supported diagonal.
        Set("paused", false);
        foreach (var direction in new[] { new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1) })
        {
            var from = (from x in Enumerable.Range(1, TownMap.Width - 2)
                        from y in Enumerable.Range(1, TownMap.Height - 2)
                        let p = new MapPosition(x, y)
                        where map.CanResume(p) && map.CanResume(new(x + direction.X, y + direction.Y))
                            && map.Move(p, direction.X, direction.Y).OriginalCode == 1
                        select p).First();
            restore.Invoke(game, [from]);
            Set("pendingDirection", direction);
            Update(0);
            Update(PlayerMotion.StepDuration / 2);
            Check(player.IsMoving && player.VisualPosition == new Vector2(from.X + direction.X * 0.5f, from.Y + direction.Y * 0.5f),
                $"Game dispatch animates diagonal {direction} on both axes");
            if (direction == new Point(1, 1)) Capture("yipe-diagonal-step.png");
            Update(PlayerMotion.StepDuration / 2);
            Check(!player.IsMoving && player.Position == new MapPosition(from.X + direction.X, from.Y + direction.Y),
                $"Diagonal {direction} lands on its accepted destination after 250 ms");
        }

        Set("paused", false);
        Set("pauseAmount", 0f);
        restore.Invoke(game, [new MapPosition(1, 2)]);
        Set("pendingDirection", new Point(0, -1));
        Update(0);
        Check(session.CurrentMap.Id != TownMap.Id && player.Position == session.Location.Position && !player.IsMoving,
            "Cave entry changes the live map and snaps to the authored arrival");
        Capture("yipe-cave-entry.png");
        RestoreSavedTown(); Set("paused", false);
        restore.Invoke(game, [new MapPosition(11, 5)]);
        Set("pendingDirection", new Point(1, 0)); Update(0);
        Check(session.Location.MapId != TownMap.Id && player.Position == session.Location.Position,
            "Surface-edge dispatch enters the adjoining map");
        Capture("yipe-surface-edge.png");
        RestoreSavedTown(); Set("paused", false);
        restore.Invoke(game, [new MapPosition(6, 5)]);
        Set("pendingDirection", new Point(0, 1)); Update(0);
        Check(session.Location.Position == new MapPosition(6, 5) && player.Position == session.Location.Position
            && (string)Get("overlay").GetType().GetProperty("Title")!.GetValue(Get("overlay"))! == "General Store",
            "Entering the general store opens its live purchasing overlay");
        Capture("yipe-general-store.png");
        var afterShopRandom = new MsvcRandom(session.Random.State);
        afterShopRandom.Next();
        DismissOverlay();
        Check(session.Random.State == afterShopRandom.State, "Leaving a shop resumes the original town encounter roll exactly once");
        randomState = session.Random.State;
        var positions = (from y in Enumerable.Range(0, TownMap.Height)
                         from x in Enumerable.Range(0, TownMap.Width)
                         let p = new MapPosition(x, y)
                         where map.CanResume(p)
                         select p).ToArray();
        restore.Invoke(game, [positions.First()]);
        Capture("yipe-village-north.png");
        Check(((Vector2)Get("cameraPosition")).Y == 0, "Camera clamps at the north edge");
        restore.Invoke(game, [positions.Last()]);
        Capture("yipe-village-south.png");
        Check(((Vector2)Get("cameraPosition")).Y == 204, "Camera clamps at the south edge");
        restore.Invoke(game, [new MapPosition(1, 5)]);
        Check(((Vector2)Get("cameraPosition")).X == 0, "Camera clamps at the west edge");
        Capture("yipe-village-west.png");
        restore.Invoke(game, [new MapPosition(11, 5)]);
        Check(((Vector2)Get("cameraPosition")).X == 96, "Camera clamps at the east edge");
        Capture("yipe-village-east.png");
        Set("graphicsMenuOpen", true); Set("selected", 0);
        menu.Invoke(game, null);
        var graphics = (GraphicsDeviceManager)Get("graphics");
        Check(graphics.IsFullScreen, "Fullscreen activates");
        Capture("yipe-village-fullscreen.png");
        menu.Invoke(game, null);
        Check(!graphics.IsFullScreen && game.GraphicsDevice.PresentationParameters.BackBufferWidth == 1280
            && game.GraphicsDevice.PresentationParameters.BackBufferHeight == 720, "Windowed 16:9 resolution restores");
        Set("selected", 1);
        menu.Invoke(game, null);
        Check((bool)Get("scanlines"), "Scanlines toggle on through the menu");
        draw.Invoke(game, [new GameTime()]);
        var display = game.GraphicsDevice.PresentationParameters;
        var withLines = new Color[display.BackBufferWidth * display.BackBufferHeight];
        game.GraphicsDevice.GetBackBufferData(withLines);
        menu.Invoke(game, null);
        draw.Invoke(game, [new GameTime()]);
        var withoutLines = new Color[withLines.Length];
        game.GraphicsDevice.GetBackBufferData(withoutLines);
        Check(!(bool)Get("scanlines") && !withLines.SequenceEqual(withoutLines), "Scanlines alter the presentation and toggle off again");
        using (var capture = new Texture2D(game.GraphicsDevice, display.BackBufferWidth, display.BackBufferHeight))
        {
            capture.SetData(withLines);
            using var file = File.Create(Path.Combine(outputDirectory, "yipe-village-scanlines.png"));
            capture.SaveAsPng(file, capture.Width, capture.Height);
        }
        Set("graphicsMenuOpen", false); Set("selected", 0); Set("selectionY", 0f);
        var renderer = Get("town");
        var rendererType = renderer.GetType();
        var palette = (Color[])rendererType.GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        foreach (var (name, w, h) in new[] { ("terrain", 64, 64), ("town", 144, 144), ("hero", 192, 128), ("meadow", 64, 64), ("details", 128, 128) })
        {
            var atlas = (Texture2D)rendererType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
            var pixels = new Color[atlas.Width * atlas.Height];
            atlas.GetData(pixels);
            Check(atlas.Width == w && atlas.Height == h, name + " uses its native pixel grid");
            Check(pixels.All(p => p.A == 0 || p.A == 255 && palette.Contains(p)), name + " has only palette colors and binary alpha");
            using var file = File.Create(Path.Combine(outputDirectory, "yipe-village-native-" + name + ".png"));
            atlas.SaveAsPng(file, atlas.Width, atlas.Height);
        }
        var worldRenderer = Get("world");
        var monsterAtlases = (Texture2D[])worldRenderer.GetType().GetField("monsters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(worldRenderer)!;
        var worldAtlases = monsterAtlases.Concat(new[] { "props", "flora", "quests" }.Select(name =>
            (Texture2D)worldRenderer.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(worldRenderer)!)).ToArray();
        for (var atlasIndex = 0; atlasIndex < worldAtlases.Length; atlasIndex++)
        {
            var atlas = worldAtlases[atlasIndex];
            var pixels = new Color[atlas.Width * atlas.Height]; atlas.GetData(pixels);
            using (var file = File.Create(Path.Combine(outputDirectory, $"yipe-world-monsters-{atlasIndex + 1}.png"))) atlas.SaveAsPng(file, atlas.Width, atlas.Height);
            Check(pixels.All(p => p.A == 0 || p.A == 255 && palette.Contains(p)), $"World atlas {atlasIndex + 1} uses the shared palette and binary alpha");
            for (var cell = 0; cell < atlas.Width / 48 * (atlas.Height / 48); cell++)
            {
                var columns = atlas.Width / 48;
                var left = cell % columns * 48; var top = cell / columns * 48;
                var occupied = (from y in Enumerable.Range(0, 48) from x in Enumerable.Range(0, 48)
                    where pixels[(top + y) * atlas.Width + left + x].A != 0 select new Point(x, y)).ToArray();
                Check(occupied.Length > 20 && occupied.All(p => p.X > 0 && p.X < 47 && p.Y > 0 && p.Y < 47),
                    atlasIndex < 3 ? $"Monster/NPC sprite {atlasIndex * 16 + cell + 1} is nonempty with a clear atlas border" : $"Map atlas {atlasIndex + 1} prop {cell + 1} has a clear atlas border");
            }
        }
        OutdoorPresentationChecks.Run(game, outputDirectory, Check);
        MaynardAtmosphereChecks.Run(game, outputDirectory, Check);
        WorldPresentationChecks.Run(game.GraphicsDevice, worldRenderer, Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
        VillagePresentationChecks.Run(game.GraphicsDevice, worldRenderer, Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
        MountainPresentationChecks.Run(game.GraphicsDevice, worldRenderer, Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
        HellPresentationChecks.Run(game.GraphicsDevice, worldRenderer, Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
        HellPresentationChecks.RunEnvironment(game, outputDirectory, Check);
        EarthPresentationChecks.Run(game.GraphicsDevice, worldRenderer, Get("town"), (MapCatalog)Get("catalog"), outputDirectory, Check);
        EarthEnvironmentChecks.Run(game, outputDirectory, Check);
        FinalCaveChecks.Run(game, outputDirectory, Check);
        StoneCaveChecks.Run(game, outputDirectory, Check);
        IceCaveChecks.Run(game, outputDirectory, Check);
        IceMovementChecks.Run(game, outputDirectory, Check);
        WaterCaveChecks.Run(game, outputDirectory, Check);
        RaftMotionChecks.Run(game, outputDirectory, Check);
        BreedingGroundChecks.Run(game, outputDirectory, Check);
        FireCaveChecks.Run(game, outputDirectory, Check);
        HeatPresentationChecks.Run(game, outputDirectory, Check);
        restore.Invoke(game, [new MapPosition(5, 5)]);
        Set("paused", true);
        Set("pauseAmount", 1f);
        Set("selected", 4); Set("selectionY", 40f);
        Set("menuMessage", "Your place in town is saved.");
        Capture("yipe-village-pause.png");
        Set("paused", false);
        Set("pauseAmount", 0f);
        Set("message", "Beyond Maynard\nThis area is not available in the town preview.");
        Set("messageRemaining", 4d);
        Capture("yipe-village-message.png");
        Set("messageRemaining", 0d);
        using (var fullMap = new RenderTarget2D(game.GraphicsDevice, 416, 384))
        {
            game.GraphicsDevice.SetRenderTarget(fullMap);
            game.GraphicsDevice.Clear(Color.Black);
            referenceBatch.Begin(samplerState: SamplerState.PointClamp);
            townType.GetMethod("Draw")!.Invoke(townRenderer, [referenceBatch, player, 1.2d, false, 0]);
            referenceBatch.End();
            game.GraphicsDevice.SetRenderTarget(null);
            using var file = File.Create(Path.Combine(outputDirectory, "yipe-village-entire-town.png"));
            fullMap.SaveAsPng(file, 416, 384);
        }
        Set("miniatureMode", 2); Set("scanlines", true);
        foreach (var p in new[] { new MapPosition(7, 6), new MapPosition(7, 7), new MapPosition(6, 7), new MapPosition(10, 10) })
        {
            restore.Invoke(game, [p]);
            Capture($"yipe-sign-spacing-{p.X}-{p.Y}.png");
        }
        restore.Invoke(game, [new MapPosition(11, 2)]);
        player.Face(new(0, -1));
        Capture("yipe-castle-simplified.png");
        foreach (var bridge in new[] { new MapPosition(3, 3), new MapPosition(2, 6) })
        {
            restore.Invoke(game, [bridge]); player.Face(new(1, 0));
            Capture($"yipe-reflection-bridge-{bridge.X}-{bridge.Y}.png");
            Set("miniatureMode", 0); Set("scanlines", false);
            Capture($"yipe-reflection-sharp-{bridge.X}-{bridge.Y}.png");
            Set("miniatureMode", 2); Set("scanlines", true);
        }
        Check(session.Random.State == randomState, "Rendering and display settings do not draw gameplay RNG");
        CheckFrontend();
        Log($"Passed {checks} visual integration checks. Rendered 360 tour frames. Recording: {(record ? "enabled" : "disabled")}.");
        game.Exit();

        void CheckFrontend()
        {
            void Choose(string label)
            {
                var current = Get("overlay") ?? throw new InvalidOperationException("No active overlay.");
                var overlayType = current.GetType();
                var control = type.GetMethod("UpdateOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!;
                while ((int)overlayType.GetProperty("Page")!.GetValue(current)! < (int)overlayType.GetProperty("PageCount")!.GetValue(current)! - 1)
                    control.Invoke(game, [new InputFrame(new Point(1, 0), false, false, false)]);
                var options = (Array)overlayType.GetProperty("Options")!.GetValue(current)!;
                var labels = options.Cast<object>().Select(o => (string)o.GetType().GetProperty("Label")!.GetValue(o)!).ToArray();
                var target = Array.FindIndex(labels, text => text == label);
                if (target < 0) throw new InvalidOperationException($"Choice '{label}' missing in {string.Join(", ", labels)}");
                for (var count = 0; (int)overlayType.GetProperty("Selected")!.GetValue(current)! != target && count < labels.Length; count++)
                    control.Invoke(game, [new InputFrame(new Point(0, 1), false, false, false)]);
                control.Invoke(game, [new InputFrame(Point.Zero, true, false, false)]);
            }
            void Invoke(string method, params object[] arguments) => type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, arguments);
            RestoreSavedTown(); Set("paused", false); Set("pauseAmount", 0f);
            var audio = Get("audio");
            Check((string)audio.GetType().GetProperty("Error")!.GetValue(audio)! == "", "All original effects and packaged monster WAV files load on the real audio device");
            audio.GetType().GetProperty("Enabled")!.SetValue(audio, true);
            var clipField = audio.GetType().GetField("current", BindingFlags.Instance | BindingFlags.NonPublic)!;
            audio.GetType().GetMethod("Play")!.Invoke(audio, [131]);
            var clip = (Microsoft.Xna.Framework.Audio.SoundEffectInstance)clipField.GetValue(audio)!;
            Check(clip.State == Microsoft.Xna.Framework.Audio.SoundState.Playing, "An original effect starts playback on the real audio device");
            audio.GetType().GetMethod("MonsterDeath")!.Invoke(audio, [35]);
            Check(!ReferenceEquals(clip, clipField.GetValue(audio)) && clip.IsDisposed,
                "A monster sound replaces and disposes the previous effect");
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            Check(clipField.GetValue(audio) is null, "Losing focus stops and releases the playing sound");
            audio.GetType().GetProperty("Enabled")!.SetValue(audio, false);
            Invoke("BeginCharacterCreation"); Choose("Combat"); Choose("Use these attributes");
            var nameOverlay = Get("overlay"); var nameReceiver = Get("textReceiver");
            Invoke("ShowNewGameConfirmation");
            var inputSuspended = Get("textReceiver") is null;
            Choose("Keep playing");
            Check(inputSuspended && ReferenceEquals(nameOverlay, Get("overlay")) && ReferenceEquals(nameReceiver, Get("textReceiver")),
                "Cancelling New adventure restores name entry after suspending its text input");
            Choose("Done"); Choose("Medium"); Choose("Classic"); Choose("Knight");
            Capture("yipe-creation-intro.png"); Choose("Continue");
            session = (GameSession)Get("session"); map = (TownMap)Get("map"); townRenderer = Get("town");
            Check(session.Player.Name == "Yipie" && session.Player.Strength == 16 && session.Player.Difficulty == 2
                && session.Player.Inventory[28] == 1 && session.Random.State == 1 && Get("overlay") is null,
                "Controller-style creation choices start an unmodified Combat/Medium character");
            var beforeScores = session.Random.State;
            Invoke("ShowActions");
            var actionOverlay = Get("overlay");
            Invoke("ShowNewGameConfirmation"); Invoke("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            Check(ReferenceEquals(actionOverlay, Get("overlay")) && session.Random.State == beforeScores,
                "Backing out of New adventure restores the action menu without changing gameplay RNG");
            Choose("Character and progress"); Choose("High scores");
            Choose("Next difficulty");
            Check((string)Get("overlay").GetType().GetProperty("Title")!.GetValue(Get("overlay"))! == "High Scores - Hard",
                "High-score overlay navigates difficulty groups with controller-style choices");
            Choose("Score code");
            var scoreLines = (string[])Get("overlay").GetType().GetProperty("Lines")!.GetValue(Get("overlay"))!;
            Check(string.Join("", scoreLines).Contains(HighScores.Code(session.Player)) && session.Random.State == beforeScores,
                "Score-code overlay exposes the original code without drawing RNG");
            Capture("yipe-score-code.png");
            Choose("Continue"); Choose("Back"); Choose("Continue exploring");
            session.Player.EncounteredMonsterTypes[2] = true;
            Invoke("ShowActions"); Choose("Character and progress"); Choose("Monster history");
            string OverlayText() => string.Join("\n", (string[])Get("overlay").GetType().GetProperty("Lines")!.GetValue(Get("overlay"))!);
            Check(!OverlayText().Contains(CombatCatalog.Monster(2).Name) && OverlayText().Split("?????").Length == 42,
                "Monster history lists all 41 unknown types and does not reveal a name from an encounter alone");
            Choose("Continue");
            session.Player.MonsterDefeats[1] = 3; session.Player.MonsterDefeatHistoryComplete = false;
            Choose("Monster history");
            Check(OverlayText().Contains("Kills: 3") && OverlayText().Contains(CombatCatalog.Monster(2).Name + ": 3")
                && OverlayText().Contains("Older save:"), "Monster history exposes recorded defeats, totals and the imported-history limitation");
            Capture("yipe-monster-history.png"); Choose("Continue");
            Check(OverlayText().Contains("Carried items 1/13") && OverlayText().Contains("Current ability: None"),
                "Character statistics include carried capacity and the current special ability");
            session.Player.EncounteredMonsterTypes[2] = false; session.Player.MonsterDefeats[1] = 0;
            session.Player.MonsterDefeatHistoryComplete = true;
            Choose("Use silly names"); Choose("Equipment");
            Check(session.Player.SillyMode && OverlayText().Contains("Thumbtack") && OverlayText().Contains("T-Shirt with Pocket")
                && session.Player.Inventory[28] == 1 && session.Random.State == beforeScores,
                "Silly Mode can be toggled during play while preserving appearance, equipment ranks and RNG");
            Choose("Continue"); Choose("Use classic names"); Choose("Equipment");
            Check(!session.Player.SillyMode && OverlayText().Contains("Small Dagger") && OverlayText().Contains("Robe"),
                "Switching back restores the original equipment names in the statistics overlay");
            Choose("Continue");
            Choose("Back"); Choose("Continue exploring");
            Invoke("ShowActions"); Choose("Inventory"); Choose("French Fries"); Choose("Back"); Choose("Back"); Choose("Continue exploring");
            Check(session.Player.Inventory[0] == 1 && Get("overlay") is null, "Inventory cancel returns through its parent overlays without consuming an item");
            Invoke("ShowActions"); Choose("Spells"); Choose("Return to Surface"); Choose("Low power - 5 SP");
            Check(session.Player.SpellPoints == 10, "Unavailable surface-return spell leaves SP untouched through the frontend");
            Choose("Continue");
            restore.Invoke(game, [new MapPosition(5, 5)]);
            var liveAdventure = (Adventure)Get("adventure");
            var timer = typeof(Adventure).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(liveAdventure)!;
            var tickField = typeof(ExplorationTimer).GetField("tick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var beforeTick = (int)tickField.GetValue(timer)!;
            Set("tickRemaining", .5d);
            for (var step = 0; step < 12; step++)
            {
                Set("pendingDirection", new Point(step % 2 == 0 ? 1 : -1, 0));
                Update(.125); Update(.125);
            }
            Check((int)tickField.GetValue(timer)! >= beforeTick + 4, "Continuous walking does not starve exploration timers");
            Update(PlayerMotion.StepDuration);
            Invoke("ShowActions");
            var pausedTick = (int)tickField.GetValue(timer)!;
            Update(30);
            Check((int)tickField.GetValue(timer)! == pausedTick, "Action overlays pause exploration without advancing timer phases");
            Choose("Continue exploring"); Update(0);
            Check((int)tickField.GetValue(timer)! == pausedTick, "Closing an overlay does not replay elapsed paused time");
            // Isolated service diagnostics; the separate opening/campaign runners earn their funds.
            session.Player.Gold = 500;
            var casinoRandom = new MsvcRandom(session.Random.State);
            var slot = new SlotMachineState { Gold = 500 };
            Invoke("ShowCasino"); Choose("Slots"); Choose("Spin"); Choose("Continue");
            Check(session.Random.State == casinoRandom.State, "An unfunded slot spin consumes no gameplay RNG through the overlay");
            Choose("Insert 10 gold"); Casino.InsertCredits(slot);
            Choose("Spin"); Casino.Spin(slot, casinoRandom); Capture("yipe-casino-slots.png"); Choose("Continue");
            Choose("Collect credits"); Casino.CollectCredits(slot); Choose("Leave");
            Check(session.Player.Gold == slot.Gold && session.Random.State == casinoRandom.State,
                "Slot insert, spin, collection and leave preserve the verified payout and RNG rules");
            var race = new CasinoWager { Gold = session.Player.Gold };
            Choose("Dot Racing"); Choose("Add bet"); Choose("Add 25 gold"); Casino.PlaceWager(race, 25);
            Choose("Race");
            var raceLines = (string[])Get("overlay").GetType().GetProperty("Lines")!.GetValue(Get("overlay"))!;
            Check(string.Join(" ", raceLines).Contains("Choose a racer first.", StringComparison.OrdinalIgnoreCase)
                && session.Random.State == casinoRandom.State, "A funded dot race asks for a racer before spending the wager or RNG");
            Choose("Continue"); Choose("Change racer"); Choose("Race"); Casino.Race(race, 1, casinoRandom);
            Capture("yipe-casino-racing.png"); Choose("Continue");
            Choose("Add bet"); Choose("Add 10 gold"); Casino.PlaceWager(race, 10); Choose("Leave"); Casino.ClearWager(race);
            Check(session.Player.Gold == race.Gold && session.Random.State == casinoRandom.State,
                "Dot racing resolves the selected racer and refunds an unplayed wager on leave");
            var darts = new CasinoWager { Gold = session.Player.Gold };
            Choose("Darts"); Choose("Add bet"); Choose("Add 25 gold"); Casino.PlaceWager(darts, 25);
            Choose("Aim down"); Choose("Aim down");
            var dartScores = new int[3];
            for (var i = 0; i < 3; i++)
            {
                Choose("Throw"); dartScores[i] = Casino.ThrowDart(i == 0 ? 46 : Casino.InitialDartAim, i, casinoRandom).Score;
                if (i == 2) Capture("yipe-casino-darts.png");
                Choose("Continue");
                if (i == 0) { Choose("Add bet"); Choose("Continue"); }
            }
            darts.Gold += Casino.DartPayout(dartScores, darts.Bet); darts.Bet = 0;
            Choose("Leave");
            Check(session.Player.Gold == darts.Gold && session.Random.State == casinoRandom.State
                && (int)Get("dartsHighScore") == dartScores.Sum(), "Three controller-driven dart throws preserve aiming, payout, round lock and session high score");
            Choose("Darts"); Choose("Add bet"); Choose("Add 5 gold"); Choose("Leave");
            Check(session.Player.Gold == darts.Gold - 5, "Leaving a dart round retains the original uncollected-wager behavior");
            Choose("Leave");
            CheckTimedRoutes();
            CheckMaps();
            string[] portraits = ["Knight", "Golden Knight", "Ranger", "Rose Mage", "Sorceress", "Wizard", "Violet Mage", "Bear", "Rooster", "Fairy", "Smiley", "Little Imp"];
            for (var portrait = 1; portrait <= portraits.Length; portrait++)
            {
                Invoke("BeginCharacterCreation"); Choose("Combat"); Choose("Use these attributes"); Choose("Done"); Choose("Medium");
                Choose(portrait >= 9 ? "Silly Mode" : "Classic");
                if (portrait is 1 or 9) Capture($"yipe-portrait-selection-{portrait}.png");
                Choose(portraits[portrait - 1]); Choose("Continue");
                var created = (GameSession)Get("session");
                Check(created.Player.Inventory[28] == portrait && created.Player.SillyMode == (portrait >= 9)
                    && created.Player.Strength == 16 && created.Random.State == 1
                    && ((MapJournal)Get("journal")).Regions.Count == 1 && ((MapJournal)Get("journal")).Find(17) is not null,
                    $"Appearance {portrait} starts fresh map notes without changing attributes or RNG");
                Capture($"yipe-character-{portrait}.png");
            }
            RestoreSavedTown(); Set("paused", false);

            void CheckMaps()
            {
                RestoreSavedTown(); Set("paused", false); Set("pauseAmount", 0f);
                Set("journal", new MapJournal()); restore.Invoke(game, [new MapPosition(5, 5)]);
                var mapCatalog = (MapCatalog)Get("catalog");
                Invoke("ShowActions"); var parent = Get("overlay"); Choose("Map");
                Check((bool)Get("mapOpen") && !(bool)Get("worldMapOpen"), "Actions opens the complete current-area map first");
                var savedState = GameSaveCodec.Encode(session, mapCatalog);
                var scene = (double)Get("sceneTime");
                Update(30);
                Check(GameSaveCodec.Encode(session, mapCatalog).SequenceEqual(savedState) && (double)Get("sceneTime") == scene,
                    "An open map freezes gameplay, scene animation and RNG");
                Capture("yipe-map-area-village.png");
                Set("miniatureMode", 0); var sharpMap = DrawPixels();
                Set("miniatureMode", 2); var strongMap = DrawPixels();
                Check(sharpMap.SequenceEqual(strongMap), "Area map and all labels stay sharp at every miniature setting");
                Invoke("UpdateMap", new InputFrame(Point.Zero, true, false, false));
                Check((bool)Get("worldMapOpen") && (int)Get("mapLayer") == 0, "Confirm switches from area to world overview");
                Capture("yipe-map-world-new-adventure.png");
                Set("controllerConnected", true); Capture("yipe-map-controller.png"); Set("controllerConnected", false);
                Invoke("UpdateMap", new InputFrame(Point.Zero, false, true, false));
                Check(!(bool)Get("mapOpen") && ReferenceEquals(parent, Get("overlay")), "Back closes the map and restores its action-menu parent");
                Choose("Continue exploring");
                Set("pendingDirection", new Point(1, 0)); Update(0); Update(.05);
                Invoke("OpenMap"); var visual = player.VisualPosition; var logical = session.Location.Position;
                Check((bool)Get("mapOpen") && player.IsMoving, "Map can open during an ordinary walking step");
                Update(20);
                Check(player.VisualPosition == visual && session.Location.Position == logical && (Point)Get("pendingDirection") == Point.Zero,
                    "Opening the map freezes the walking tween and clears buffered movement");
                Invoke("UpdateMap", new InputFrame(Point.Zero, false, false, false, Map: true)); Update(0);
                Check(player.VisualPosition == visual && (double)Get("tickRemaining") == .512,
                    "Closing the map does not replay paused time or buffered input");
                Update(PlayerMotion.StepDuration); Invoke("OpenMap");
                Invoke("UpdateMap", new InputFrame(Point.Zero, false, false, true));
                Set("selected", 3); menu.Invoke(game, null);
                Check(!(bool)Get("mapOpen") && !(bool)Get("paused") && Get("overlay") is not null,
                    "New adventure from the map's pause menu exposes its confirmation correctly");
                Choose("Keep playing");

                // Diagnostic exploration history; each region is observed through the same visibility boundary as play.
                var notes = (MapJournal)Get("journal");
                foreach (var id in new[] { 8, 9, 10, 16, 18, 21, 27, 41, 57, 81, 117, 133 })
                {
                    var layer = id >= 117 ? 0 : (id - 1) / 40;
                    var offset = id - layer * 40 - (id >= 117 ? 98 : 0) - 1;
                    var location = new WorldLocation { MapId = id, MapLayer = layer, AlternateMapSet = id >= 117,
                        RegionX = offset % 6 - 4, RegionY = 2 - offset / 6, Position = new(1, 2) };
                    notes.Observe(new Adventure(new GameSession(mapCatalog, location: location)));
                }
                notes.Links.AddRange([new(8, 9), new(9, 10), new(10, 16), new(16, 17), new(17, 18), new(17, 57), new(57, 81)]);
                Invoke("OpenMap"); Invoke("UpdateMap", new InputFrame(Point.Zero, true, false, false));
                Capture("yipe-map-world-explored.png");
                Invoke("UpdateMap", new InputFrame(new Point(1, 0), false, false, false));
                Check((int)Get("mapLayer") == 1, "World overview cycles through discovered cave layers");
                Capture("yipe-map-world-caves.png");
                Invoke("UpdateMap", new InputFrame(new Point(1, 0), false, false, false));
                Invoke("UpdateMap", new InputFrame(new Point(1, 0), false, false, false));
                Capture("yipe-map-world-hell.png");
                Check((int)Get("mapLayer") == 3, "Alternate regions occupy their original coordinates on a separate world layer");
                Invoke("UpdateMap", new InputFrame(new Point(1, 0), false, false, false));
                Set("paused", true); Set("selected", 1); menu.Invoke(game, null); FinishPendingStorage("saving");
                var mapNotes = File.ReadAllBytes(Path.ChangeExtension(gameSavePath, ".map.json"));
                Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
                session = (GameSession)Get("session"); Set("paused", false); Set("pauseAmount", 0f);
                Check(!(bool)Get("mapOpen") && ((MapJournal)Get("journal")).Regions.Count == 13
                    && ((MapJournal)Get("journal")).Encode(File.ReadAllBytes(gameSavePath)).SequenceEqual(mapNotes),
                    "Production save/load restores the map journal and closes the old map screen");
                var caveSession = new GameSession(mapCatalog, location: new() { MapId = 41, MapLayer = 1,
                    RegionX = -4, RegionY = 2, Position = new(1, 2) });
                Set("session", caveSession); Set("adventure", new Adventure(caveSession)); Set("journal", new MapJournal());
                Invoke("SynchronizeWorld", false); Invoke("OpenMap");
                Capture("yipe-map-area-cave.png");
                var caveNotes = ((MapJournal)Get("journal")).Find(41)!;
                var mapTexture = (RenderTarget2D)Get("mapView"); var terrainPixels = new Color[416 * 384]; mapTexture.GetData(terrainPixels);
                var hiddenPixelsClear = true;
                for (var x = 0; x < Map.Width; x++)
                    for (var y = 0; y < Map.Height; y++)
                        if (!caveNotes.Seen[x * Map.Height + y])
                            for (var px = x * 32; px < x * 32 + 32; px++)
                                for (var py = y * 32; py < y * 32 + 32; py++)
                                    hiddenPixelsClear &= terrainPixels[py * 416 + px] == new Color(20, 25, 36);
                Check(caveNotes.Seen.Any(s => !s) && hiddenPixelsClear, "Unexplored map cells hide terrain and overlapping tall sprites completely");
                Invoke("UpdateMap", new InputFrame(Point.Zero, false, true, false));
                var generatedSession = new GameSession(mapCatalog, location: new() { MapId = 118, AlternateMapSet = true,
                    RegionX = -3, RegionY = -1, Position = new(1, 5) });
                Set("session", generatedSession); Set("adventure", new Adventure(generatedSession));
                var generatedRng = generatedSession.Random.State;
                Invoke("SynchronizeWorld", false); Invoke("OpenMap"); Capture("yipe-map-area-generated.png");
                Check(generatedSession.Random.State == generatedRng && ((MapJournal)Get("journal")).Find(118)!.Seen.Any(s => s),
                    "A generated area map renders its current discoveries without consuming extra RNG");
                Invoke("UpdateMap", new InputFrame(Point.Zero, false, true, false));
                RestoreSavedTown(); Set("paused", false); Set("pauseAmount", 0f);
                Invoke("BeginCharacterCreation"); Choose("Combat"); Choose("Use these attributes");
                Invoke("OpenMap");
                Check(!(bool)Get("mapOpen"), "Map refuses to interrupt character name entry");
            }

            void CheckTimedRoutes()
            {
                // Diagnostic prerequisites isolate timing; earned acquisition is checked by the campaign.
                var routeCatalog = (MapCatalog)Get("catalog");
                void Load(WorldLocation location)
                {
                    var hero = new Player { Health = 100, MaximumHealth = 100, HasRainbowKey = true, RandomEncounterThreshold = 32767 };
                    var scenario = new GameSession(routeCatalog, hero, location);
                    File.WriteAllBytes(gameSavePath, GameSaveCodec.Encode(scenario, routeCatalog));
                    Set("paused", true); Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
                    session = (GameSession)Get("session"); Set("paused", false); Set("pauseAmount", 0f);
                }
                void Frame()
                {
                    Update(.025);
                    if (Get("overlay") is not { } current) return;
                    var title = (string)current.GetType().GetProperty("Title")!.GetValue(current)!;
                    if (title == "Rescued Princess") Choose("Kiss");
                    else if (title == "Combat") throw new InvalidOperationException("Unexpected combat in isolated timed route.");
                    else Choose("Continue");
                }
                void Walk(string directions)
                {
                    foreach (var key in directions)
                    {
                        var direction = key switch { '1' => new Point(-1, 1), '2' => new(0, 1), '4' => new(-1, 0),
                            '6' => new(1, 0), '8' => new(0, -1), '9' => new(1, -1), _ => throw new InvalidOperationException("Unexpected timed direction") };
                        Set("pendingDirection", direction); Update(0);
                        for (var frame = 0; frame < 10; frame++) Frame();
                        while (player.IsMoving || Get("overlay") is not null) Frame();
                    }
                }
                void Until(Func<bool> condition)
                {
                    for (var frame = 0; frame < 40000; frame++)
                    {
                        if (condition() && !player.IsMoving && Get("overlay") is null) return;
                        Frame();
                    }
                    throw new InvalidOperationException($"Timed route did not reach its landing: {session.Location.Position}");
                }
                Load(new WorldLocation { MapId = 68, RegionX = -1, RegionY = -2, MapLayer = 1, Position = new(9, 10) });
                Walk("21111222");
                Until(() => session.CurrentMap.Events.Overlay.Position == new MapPosition(4, 9)); Walk("1");
                Check(session.CurrentMap.Id == 74 && session.Location.Position == new MapPosition(4, 9),
                    "At the approved walking pace, the character enters the water cave and boards its moving platform");
                Set("paused", true); Set("selected", 1); menu.Invoke(game, null); FinishPendingStorage("saving");
                Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
                session = (GameSession)Get("session"); Set("paused", false); Set("pauseAmount", 0f);
                Check(session.Location.Position == new MapPosition(4, 9) && session.CurrentMap.Events.Overlay.Position == session.Location.Position,
                    "Production pause-menu save/load restores platform support under its rider");
                Capture("yipe-platform-reopened.png");
                Until(() => session.Location.Position == new MapPosition(3, 3)); Walk("44886");
                Check(session.Player.InventoryValues[11] == 1, "The moving-platform route reaches princess 11 through the key door using normal frontend movement");
                Walk("4226"); Until(() => session.CurrentMap.Events.Overlay.Position == new MapPosition(3, 3)); Walk("6");
                Until(() => session.Location.Position == new MapPosition(4, 5)); Walk("6");
                Check(session.Location.Position == new MapPosition(5, 5) && session.Player.Health == 100,
                    "The platform rescue returns aboard and disembarks safely with exploration timers running");
                Capture("yipe-platform-rescued.png");
                Load(new WorldLocation { MapId = 133, RegionX = 0, RegionY = -3, AlternateMapSet = true, Position = new(6, 8) });
                Until(() => session.CurrentMap.Tile(new(5, 8)) == 0x27); Walk("44441");
                Check(session.Player.InventoryValues[20] == 1, "The timed fire opening is long enough to reach princess 20 at 250 ms per step");
                Walk("966");
                // Wait for a fresh opening, rather than entering at the end of its safe interval.
                Until(() => session.CurrentMap.Tile(new(5, 8)) == 0x24);
                Until(() => session.CurrentMap.Tile(new(5, 8)) == 0x27); Walk("66");
                Check(session.Location.Position == new MapPosition(6, 8) && session.Player.Health == 100,
                    "The fire-wall rescue returns safely; dialogue pauses do not replay hazard ticks");
                Capture("yipe-fire-route-rescued.png");

                Load(new WorldLocation { MapId = 57, MapLayer = 1, Position = new(1, 2) });
                session.Player.SurfaceReturnMapId = 17;
                session.Player.Inventory[0] = 5;
                var potionRandom = session.Random.State;
                audio.GetType().GetProperty("Enabled")!.SetValue(audio, true);
                Invoke("ShowActions"); Choose("Inventory"); Choose(ItemNames.Consumable(5)); Choose("Use");
                var potionClip = (Microsoft.Xna.Framework.Audio.SoundEffectInstance)clipField.GetValue(audio)!;
                Check(session.CurrentMap.Id == 17 && session.Player.Inventory[0] == 0 && session.Random.State == potionRandom,
                    "The surface potion relocates through its inventory overlay without drawing RNG");
                Choose("Continue");
                Check(potionClip.IsDisposed && !ReferenceEquals(potionClip, clipField.GetValue(audio)) && clipField.GetValue(audio) is not null,
                    "Acknowledging a surface potion plays the return cue after its item cue");
                audio.GetType().GetProperty("Enabled")!.SetValue(audio, false);
                Choose("Back"); Choose("Continue exploring");
            }
        }

        void DismissOverlay()
        {
            for (var i = 0; i < 10 && Get("overlay") is { } current; i++)
            {
                var back = (Action?)current.GetType().GetProperty("Back")!.GetValue(current);
                if (back is null) throw new InvalidOperationException("Unexpected non-dismissible interaction in visual tour.");
                back();
            }
        }
        void RestoreSavedTown()
        {
            File.WriteAllBytes(gameSavePath, goodSaveBytes);
            Set("selected", 2); menu.Invoke(game, null); FinishPendingStorage("loading");
            session = (GameSession)Get("session"); map = (TownMap)Get("map"); townRenderer = Get("town");
            randomState = session.Random.State;
        }
        object Get(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
        void Set(string name, object? value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
        void Update(double seconds) => update.Invoke(game, [new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds))]);
        void Log(string message)
        {
            Console.WriteLine(message);
            log.WriteLine(message);
        }

        void CheckVignette()
        {
            var snapshot = GameSaveCodec.Encode(session, (MapCatalog)Get("catalog"));
            Set("graphicsMenuOpen", true); Set("selected", 3); menu.Invoke(game, null);
            Check((bool)Get("vignette"), "Vignette toggles on through the pause menu");
            var profileType = Get("profile").GetType();
            var reopened = Activator.CreateInstance(profileType, [Path.ChangeExtension(importedPath, ".profile.json")])!;
            var preferences = profileType.GetProperty("Data")!.GetValue(reopened)!;
            Check((bool)preferences.GetType().GetProperty("Vignette")!.GetValue(preferences)!,
                "The pause-menu vignette preference survives reopening its profile");
            foreach (var mode in new[] { 0, 1, 2 })
                foreach (var lines in new[] { false, true })
                {
                    Set("miniatureMode", mode); Set("scanlines", lines); Set("vignette", false);
                    var before = DrawPixels(); Set("vignette", true); var after = DrawPixels();
                    Check((from y in Enumerable.Range(300, 120) from x in Enumerable.Range(560, 160) select y * 1280 + x)
                        .All(i => before[i] == after[i]) && before.Zip(after).All(p =>
                            p.Second.R <= p.First.R && p.Second.G <= p.First.G && p.Second.B <= p.First.B && p.Second.A == 255)
                        && (from y in Enumerable.Range(0, 80) from x in Enumerable.Range(0, 80) select y * 1280 + x)
                            .Any(i => before[i].R > after[i].R + 10),
                        $"Vignette darkens edges, preserves the center and never blurs or brightens with miniature {mode}, scanlines {lines}");
                }
            Capture("yipe-vignette-on.png");
            Set("vignette", false); Capture("yipe-vignette-off.png");
            Set("miniatureMode", 0); Set("paused", true); Set("pauseAmount", 1f); Set("selectionY", 30f);
            Set("menuMessage", "Preferences could not be saved.\nChanges apply for this session.");
            var pauseOff = DrawPixels(); Set("vignette", true); var pauseOn = DrawPixels();
            Check((from y in Enumerable.Range(6 * 4, 168 * 4) where y < 74 * 4 || y >= 81 * 4
                from x in Enumerable.Range(34 * 4, 252 * 4) select y * 1280 + x).All(i => pauseOff[i] == pauseOn[i]),
                "Vignette leaves pause text and panels unchanged apart from its own On/Off label");
            Capture("yipe-vignette-menu.png");
            Set("paused", false); Set("pauseAmount", 0f); Set("menuMessage", "");
            type.GetMethod("ShowActions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);
            var actionsOn = DrawPixels(); Set("vignette", false); var actionsOff = DrawPixels();
            Check((from y in Enumerable.Range(7 * 4, 166 * 4) from x in Enumerable.Range(14 * 4, 292 * 4)
                select y * 1280 + x).All(i => actionsOn[i] == actionsOff[i]),
                "Action and dialogue panels retain their original colors with vignette enabled");
            Set("overlay", null);
            type.GetMethod("OpenMap", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);
            var mapOff = DrawPixels(); Set("vignette", true);
            Check(mapOff.SequenceEqual(DrawPixels()), "Vignette does not darken the full-screen map or its labels");
            Set("mapOpen", false); Set("miniatureMode", 2); Set("scanlines", true); Set("selected", 3);
            menu.Invoke(game, null);
            Check(!(bool)Get("vignette") && GameSaveCodec.Encode(session, (MapCatalog)Get("catalog")).SequenceEqual(snapshot),
                "Vignette toggles off again without changing gameplay or RNG");
            Set("graphicsMenuOpen", false); Set("selected", 0); Set("selectionY", 0f); Set("menuMessage", "");
        }

        void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("FAIL: " + label);
            checks++;
            Log("PASS: " + label);
        }
        void Capture(string path)
        {
            draw.Invoke(game, [new GameTime()]);
            var parameters = game.GraphicsDevice.PresentationParameters;
            var data = new Color[parameters.BackBufferWidth * parameters.BackBufferHeight];
            game.GraphicsDevice.GetBackBufferData(data);
            using var texture = new Texture2D(game.GraphicsDevice, parameters.BackBufferWidth, parameters.BackBufferHeight);
            texture.SetData(data);
            using var output = File.Create(Path.Combine(outputDirectory, path));
            texture.SaveAsPng(output, texture.Width, texture.Height);
        }

        Color[] DrawPixels()
        {
            draw.Invoke(game, [new GameTime()]);
            var data = new Color[1280 * 720];
            game.GraphicsDevice.GetBackBufferData(data);
            return data;
        }
        double EdgeContrast(Color[] data)
        {
            double total = 0;
            for (var y = 0; y < 96; y++)
                for (var x = 1; x < 1280; x++)
                {
                    var a = data[y * 1280 + x - 1]; var b = data[y * 1280 + x];
                    total += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
                }
            return total;
        }
    }
}
