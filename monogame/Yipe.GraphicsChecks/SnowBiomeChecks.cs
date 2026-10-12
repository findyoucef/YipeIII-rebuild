using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class SnowBiomeChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "session", "adventure", "renderedMap", "cameraPosition", "paused", "pauseAmount", "overlay", "mapOpen",
            "worldMapOpen", "journal", "sceneTime", "miniatureMode", "scanlines", "vignette", "pixelLighting", "sunShaftStrength",
            "cloudShadowStrength", "rainStrength", "outdoorWeatherTime", "tickRemaining", "messageRemaining", "timeOfDayMode",
            "dayNightSeconds", "dayNightUpdatedAt", "saving", "loading", "studioSplashRemaining", "walkingStep", "pendingDirection" }
            .ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var position = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!; var world = Get("world")!; var wt = world.GetType();
        object Field(string name) => wt.GetField(name, flags)!.GetValue(world)!;
        void Invoke(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var snowAt = (Func<int, int, bool>)wt.GetMethod("SnowAt", flags)!.CreateDelegate(typeof(Func<int, int, bool>), world);
        var coldAt = (Func<int, int, bool>)wt.GetMethod("SnowColdAt", flags)!.CreateDelegate(typeof(Func<int, int, bool>), world);
        int Prints() => ((System.Collections.ICollection)Field("snowPrints")).Count;
        var directory = Path.Combine(output, "snow-biome"); Directory.CreateDirectory(directory);
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 416, 384);
        using var presented = new Texture2D(game.GraphicsDevice, 1280, 720);
        var presentedPixels = new Color[1280 * 720];
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("timeOfDayMode", 1); Set("messageRemaining", 0d); Set("pendingDirection", Point.Zero);
            var atlas = (Texture2D)Field("snowProps"); var pixels = new Color[96 * 96]; atlas.GetData(pixels);
            var art = type.Assembly.GetType("Yipe.Desktop.RetroArt")!;
            var palette = (Color[])art.GetField("Palette", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var snowPalette = (Color[])art.GetField("SnowPalette", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var mountains = Field("mountains"); var mt = mountains.GetType();
            var stonePalette = (Color[])mt.GetField("StonePalette", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            check(atlas.Width == 96 && atlas.Height == 96 && pixels.All(p => p.A is 0 or 255 && (p.A == 0 || palette.Contains(p))),
                "Winter art compiles to a native four-frame 48px atlas with binary alpha and the shared palette");
            check(Enumerable.Range(0, 4).All(frame => Enumerable.Range(0, 48 * 48)
                .Any(i => pixels[(frame / 2 * 48 + i / 48) * 96 + frame % 2 * 48 + i % 48].A == 255)),
                "Both snowy pines, the Zephyr arch and the rock/branch detail have nonempty native frames");
            using (var file = File.Create(Path.Combine(directory, "snow-props-native.png"))) atlas.SaveAsPng(file, 96, 96);

            var snow = Scene(13); var snapshot = GameSaveCodec.Encode(snow.Session, catalog);
            var artSession = snow.Session;
            check(snow.Map.Tile(new(5, 4)) == 0x39 && snow.Map.Events.Message.Position == new MapPosition(8, 2)
                && snow.Map.Events.Message.Value == 12, "Surface A3 resolves to the authored Zephyr cave at 5,4 and sign at 8,2");
            Invoke("PrepareSnow", snow.Map);
            var ground = ((Color[])Field("snowPixels")).ToArray();
            check(ground.Count(p => snowPalette.Contains(p)) > 10000
                && ground.All(p => p.A == 0 || p.A == 255 && (snowPalette.Contains(p) || stonePalette.Contains(p))),
                "The cold basin retains opaque native snow and slate ground without warm desert colors");
            check(Enumerable.Range(176, 32).All(y => snowAt(223, y) && snowAt(224, y)),
                "Adjacent full snow cells join without a grid seam or exposed grass crack");
            var melting = (from y in Enumerable.Range(64, 32) from x in Enumerable.Range(224, 32) select snowAt(x, y)).ToArray();
            check(melting.Any(value => value) && melting.Any(value => !value), "The authored melting edge mixes snow and exposed ground at its actual footprint");
            Save(World(snow, 2), "a3-clear.png");
            var secret = World(snow, 2); snow.Map.RestoreTile(new(5, 7), 0x1f);
            check(secret.SequenceEqual(World(snow, 2)) && ground.SequenceEqual((Color[])Field("snowPixels")),
                "Searching or replacing hidden-gold snow with ordinary snow cannot reveal its position through artwork or mask changes");
            snow.Map.RestoreTile(new(5, 7), 0x69);
            var surface = wt.GetMethod("FootstepAt", flags)!;
            var secretSurface = surface.Invoke(world, [snow.Map, new MapPosition(5, 7)])!.ToString();
            snow.Map.RestoreTile(new(5, 7), 0x1f);
            check(surface.Invoke(world, [snow.Map, new MapPosition(5, 5)])!.ToString() == "Snow"
                && secretSurface == surface.Invoke(world, [snow.Map, new MapPosition(5, 7)])!.ToString(),
                "Snow uses its recorded crunch, and secret terrain has the same surface sound as ordinary ground");
            snow.Map.RestoreTile(new(5, 7), 0x69);

            var snowPeaks = (Color[])mt.GetField("snowPeaks", flags)!.GetValue(mountains)!;
            var snowPeakBounds = (Rectangle[])mt.GetField("snowPeakBounds", flags)!.GetValue(mountains)!;
            check(snowPeakBounds.Length == 16 && snowPeaks.All(p => p.A == 0
                || p.A == 255 && (snowPalette.Contains(p) || stonePalette.Contains(p))),
                "Dedicated winter peaks compile to sixteen native sprites with binary alpha and snow/stone colors");
            var snowCounts = snowPeakBounds.Select(bounds =>
                (from y in Enumerable.Range(bounds.Y, bounds.Height)
                 from x in Enumerable.Range(bounds.X, bounds.Width)
                 select snowPeaks[y * 256 + x]).Count(p => snowPalette.Contains(p))).ToArray();
            check(Enumerable.Range(0, 8).All(frame => snowCounts[frame] > 25
                && snowCounts[frame + 8] > snowCounts[frame] * 1.5),
                "Each mountain has authored light-dusting and deep-snow variants with distinct snow coverage");
            using (var nativePeaks = new Texture2D(game.GraphicsDevice, 256, 256))
            {
                nativePeaks.SetData(snowPeaks);
                using var file = File.Create(Path.Combine(directory, "snow-peaks-native.png"));
                nativePeaks.SaveAsPng(file, 256, 256);
            }
            Color[] Mountain(Map map)
            {
                mt.GetMethod("Prepare", flags)!.Invoke(mountains, [map.Id, (Func<MapPosition, ushort>)map.Tile]);
                var tex = (Texture2D)mt.GetProperty("Texture", flags)!.GetValue(mountains)!;
                var data = new Color[416 * 384]; tex.GetData(data); return data;
            }
            var ridge = Mountain(snow.Map); var north = Mountain(catalog.CreateMap(7)!);
            // The west overlap has an authored skirt/sand difference (map7 4,11 vs map13 4,1).
            // Compare the matching solid ridge spine, not different authored boundaries.
            check((from y in Enumerable.Range(0, 64) from x in Enumerable.Range(192, 32)
                select ridge[y * 416 + x] == north[(y + 320) * 416 + x]).All(value => value),
                "The matching snow-capped A3/A2 ridge spine retains shared contour and color continuity");
            check(ridge.Any(p => snowPalette.Contains(p)) && ridge.Any(p => p == palette[2]),
                "The ridge combines snow-covered tops with visible dark slate cliff faces");
            check((from y in Enumerable.Range(32, 32) from x in Enumerable.Range(192, 32)
                select ridge[y * 416 + x]).Any(p => snowPalette.Contains(p)),
                "Light snow reaches the northern ridge beyond the basin's ground tiles instead of stopping at the tile boundary");
            var contact = new Map(13, 0, Enumerable.Repeat((ushort)0x1f, Map.TileCount).ToArray());
            for (var y = 3; y <= 7; y++)
                for (var x = 3; x <= 6; x++) contact.RestoreTile(new(x, y), 3);
            contact.RestoreTile(new(7, 5), 0x21);
            check(Mountain(contact).All(p => p != palette[10] && p != palette[11]),
                "Rock beside snowy pines and snow ground never grows a meadow-green fringe");
            Invoke("PrepareSnow", contact);
            var contactGround = (Color[])Field("snowPixels");
            check(Enumerable.Range(0, contactGround.Length).Where(i => contact.Tile(new(i % 416 / 32, i / 416 / 32)) == 0x1f)
                .All(i => contactGround[i].A == 255),
                "The snowy basin has continuous snow or scree coverage without accidental meadow holes");
            var contactRock = Mountain(contact);
            check(Enumerable.Range(144, 32).All(y =>
                Enumerable.Range(208, 28).Any(x => contactRock[y * 416 + x].A == 0
                    && stonePalette.Contains(contactGround[y * 416 + x]) && !snowAt(x, y) && coldAt(x, y))
                && snowAt(236, y)),
                "A narrow cold stone margin separates snow from mountain feet, with snow returning within twelve pixels");
            check(surface.Invoke(world, [contact, new MapPosition(4, 2)])!.ToString() == "Snow",
                "Walking close to the mountains crunches where snow now covers the foot plant");
            var warm = Scene(7); Invoke("PrepareSnow", warm.Map);
            check(!Enumerable.Range(0, ground.Length).Any(i => snowAt(i % 416, i / 416))
                && Effect("DrawSnowfall", warm, 2, 2f).All(p => p.A == 0), "A2's warm ground and weather do not retain A3 snow masks");
            var cave = Scene(53); cave.Map.RestoreTile(new(5, 5), 0x1f); Invoke("PrepareSnow", cave.Map);
            check(Effect("DrawSnowfall", cave, 2, 2f).All(p => p.A == 0), "Snow weather stays outside the Zephyr cave interior");

            snow = Scene(13); Invoke("PrepareSnow", snow.Map);
            var flakes = Effect("DrawSnowfall", snow, 2, 1f);
            check(flakes.Any(p => p.A > 0) && flakes.SequenceEqual(Effect("DrawSnowfall", snow, 2, 1f))
                && !flakes.SequenceEqual(Effect("DrawSnowfall", snow, 2.5, 1f)), "Snowfall repeats at fixed scene time and drifts when that clock advances");
            check(Effect("DrawSnowfall", snow, 2, 0f).Any(p => p.A > 0)
                && Effect("DrawSnowfall", snow, 2, 2f).Count(p => p.A > 0) > flakes.Count(p => p.A > 0),
                "The basin has gentle snow in clear weather and heavier flurries during storms");
            check(Enumerable.Range(0, flakes.Length).All(i => flakes[i].A == 0 || coldAt(i % 416, i / 416)),
                "Every snowflake remains inside the compiled cold basin");
            snow.Visibility.Initialize(snow.Map, new(7, 8));
            var hidden = Effect("DrawSnowfall", snow, 2, 2f);
            check(Enumerable.Range(0, hidden.Length).All(i => hidden[i].A == 0 || snow.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                "Snowfall cannot expose an unexplored snowy cell or cliff");
            Reveal(snow);
            var contactSession = snow.Session;
            var contactSnapshot = GameSaveCodec.Encode(contactSession, catalog);
            var rain = type.Assembly.GetType("Yipe.Desktop.Rain")!;
            Color[] Rain(bool masked) => Render(() => rain.GetMethod("DrawStreaks")!.Invoke(null,
                [batch, Get("pixel"), 2d, 2f, masked ? coldAt : null]));
            var ordinary = Rain(false); var clipped = Rain(true);
            check(ordinary.Where((p, i) => coldAt(i % 416, i / 416)).Any(p => p.A > 0)
                && Enumerable.Range(0, ordinary.Length).All(i => coldAt(i % 416, i / 416) ? clipped[i].A == 0 : ordinary[i] == clipped[i]),
                "Cold pixels receive no rain streaks while every warm rain pixel keeps its original rendering");
            var impacts = Render(() => rain.GetMethod("DrawSplashes")!.Invoke(null,
                [batch, Get("pixel"), 2d, 2f, (Func<MapPosition, ushort>)(_ => 2), (Func<int, int, bool>)((_, _) => false), coldAt]));
            check(impacts.Any(p => p.A > 0) && Enumerable.Range(0, impacts.Length)
                .All(i => impacts[i].A == 0 || !coldAt(i % 416, i / 416)),
                "Warm ground impacts cannot splash a neighboring snow pixel at the cold boundary");

            Invoke("ClearSnowTracks"); player.SnapTo(new(5, 5)); player.MoveTo(new(6, 5)); Set("walkingStep", true);
            Update(.1); check(Prints() == 0, "A walking step produces no snow track before its foot plant");
            Update(.15); check(Prints() == 1, "An accepted planted walking step produces one snow boot/powder contact");
            var born = (double)Get("sceneTime")!; var prints = Effect("DrawSnowTracks", snow, born + .1);
            check(prints.Any(p => p.A > 0) && Enumerable.Range(0, prints.Length).All(i => prints[i].A == 0 || snowAt(i % 416, i / 416)),
                "Snow boot impressions and contact powder are clipped to actual snow pixels");
            check(prints.SequenceEqual(Effect("DrawSnowTracks", snow, born + .1))
                && !prints.SequenceEqual(Effect("DrawSnowTracks", snow, born + .2))
                && Effect("DrawSnowTracks", snow, born + 4.1).All(p => p.A == 0),
                "Boot trails repeat at fixed time, powder lifts briefly, and both fade without permanent marks");
            Set("paused", true); var clock = Get("sceneTime"); Update(.5);
            check(Equals(clock, Get("sceneTime")) && Prints() == 1, "Pause freezes snow contacts and their fading scene clock"); Set("paused", false);
            Invoke("ClearSnowTracks"); player.SnapTo(new(5, 5)); player.MoveTo(new(6, 5)); Set("walkingStep", false); Update(.25);
            check(Prints() == 0, "Passive movement and idle updates cannot create snow footprints");
            Invoke("SnowStep", snow.Map, player, 3d); Call("RestorePosition", new MapPosition(6, 5));
            check(Prints() == 0, "Restoring or snapping the player clears old snow contacts");
            Invoke("SnowStep", snow.Map, player, 3d); Scene(7);
            check(Prints() == 0, "A real map synchronization clears the prior basin's snow trail");
            snow = Scene(13); Invoke("PrepareSnow", snow.Map);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) if (wt.GetMethod("SnowTile", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [snow.Map.Tile(new(x, y))]) is true)
                snow.Map.RestoreTile(new(x, y), 2);
            Invoke("PrepareSnow", snow.Map);
            check(Enumerable.Range(0, ground.Length).All(i => !snowAt(i % 416, i / 416) && !coldAt(i % 416, i / 416)),
                "Removing snow on the same map instance invalidates ground and cold-weather caches");

            snow = Scene(13); Invoke("PrepareSnow", snow.Map);
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(artSession, catalog))
                && contactSnapshot.SequenceEqual(GameSaveCodec.Encode(contactSession, catalog)),
                "Snow rendering, contacts, weather and art inspection preserve the same-session gameplay/RNG snapshots");
            Save(World(snow, 4, 1), "a3-snowing.png");
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("pixelLighting", true);
            Set("cameraPosition", new Vector2(100, 75)); Set("rainStrength", 1);
            var weather = rain.GetMethod("WeatherAmount")!;
            Set("outdoorWeatherTime", (double)Enumerable.Range(300, 600).First(t => (float)weather.Invoke(null, [(double)t])! >= .9f));
            foreach (var mode in new[] { 1, 3 }) { Set("timeOfDayMode", mode); Call("Draw", new GameTime()); using var file = File.Create(Path.Combine(directory, mode == 1 ? "day.png" : "night.png")); game.GraphicsDevice.GetBackBufferData(presentedPixels); presented.SetData(presentedPixels); presented.SaveAsPng(file, 1280, 720); }
            Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", true); Call("Draw", new GameTime());
            game.GraphicsDevice.GetBackBufferData(presentedPixels);
            check(presentedPixels.All(p => p.A == 255), "Snow and moonlight compose with miniature, scanlines, vignette and local lighting");
            using (var file = File.Create(Path.Combine(directory, "night-combined.png"))) { presented.SetData(presentedPixels); presented.SaveAsPng(file, 1280, 720); }
            Set("timeOfDayMode", 1); Invoke("ClearSnowTracks");
            for (var frame = 0; frame < 48; frame++)
            {
                var time = frame / 12d;
                if (frame % 6 == 0) player.MoveTo(new(frame / 6 % 2 == 0 ? 5 : 6, 5));
                var moving = player.IsMoving; player.Update(1d / 12);
                if (moving && !player.IsMoving) Invoke("SnowStep", snow.Map, player, time);
                Save(World(snow, time, 1), $"frame-{frame:D3}.png");
            }
        }
        finally
        {
            Invoke("ClearSnowTracks"); foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        void Update(double elapsed) { Set("tickRemaining", 100d); Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(elapsed))); }
        Adventure Scene(int id)
        {
            var cell = (id - 1) % 40;
            var adventure = new Adventure(new GameSession(catalog, location: new() { MapId = id, MapLayer = (id - 1) / 40,
                RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = new(6, 5) }));
            Set("session", adventure.Session); Set("adventure", adventure); Reveal(adventure); Call("SynchronizeWorld", false); return adventure;
        }
        void Reveal(Adventure adventure) { for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) adventure.Visibility.RevealFrom(adventure.Map, new(x, y)); }
        Color[] Render(Action draw)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            draw(); batch.End(); game.GraphicsDevice.SetRenderTarget(null); var data = new Color[416 * 384]; target.GetData(data); return data;
        }
        Color[] World(Adventure scene, double time, float strength = 0) => Render(() =>
        { Invoke("Draw", batch, scene, player, Get("town")!, time, false, strength); Invoke("DrawSnowfall", batch, scene, time, strength); });
        Color[] Effect(string method, Adventure scene, double time, float? strength = null) => Render(() =>
        { if (strength is { } value) Invoke(method, batch, scene, time, value); else Invoke(method, batch, scene, time); });
        void Save(Color[] data, string name) { target.SetData(data); using var file = File.Create(Path.Combine(directory, name)); target.SaveAsPng(file, 416, 384); }
    }
}
