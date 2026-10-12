using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class OutdoorPresentationChecks
{
    internal static void Run(Game host, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = host.GetType();
        object Get(string name) => type.GetField(name, flags)!.GetValue(host)!;
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(host, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(host, args);
        var world = Get("world"); var wt = world.GetType(); var town = Get("town");
        object? World(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var catalog = (MapCatalog)Get("catalog");
        var directory = Path.Combine(output, "outdoors"); Directory.CreateDirectory(directory);
        var saved = new[] { "session", "adventure", "renderedMap", "cameraPosition", "sceneTime", "paused", "pauseAmount", "overlay", "mapOpen",
            "worldMapOpen", "saving", "loading", "studioSplashRemaining", "walkingStep", "pendingDirection", "messageRemaining", "timeOfDayMode", "outdoorWeatherTime" }.ToDictionary(n => n, Get);
        var player = (PlayerMotion)Get("player"); var position = player.Position;
        var audio = Get("audio"); var at = audio.GetType();
        void Audio(string name, params object[] args) => at.GetMethod(name)!.Invoke(audio, args);
        SoundEffectInstance? Voice(string name) => (SoundEffectInstance?)at.GetField(name, flags)!.GetValue(audio);
        var enabled = at.GetProperty("Enabled")!.GetValue(audio); var volume = at.GetProperty("Volume")!.GetValue(audio);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var buffer = new Color[1280 * 720];
        var counts = new List<string>();
        try
        {
            for (var id = 1; id <= 36; id++)
            {
                var scene = Scene(id); var map = scene.Map;
                var before = GameSaveCodec.Encode(scene.Session, catalog);
                var seen = Enumerable.Repeat(true, Map.TileCount).ToArray();
                var pixels = Render(() => World("DrawMap", batch, map, seen, 2d, town)); Save($"map-{id:D3}.png");
                check(pixels.SequenceEqual(Render(() => World("DrawMap", batch, map, seen, 2d, town))), $"Surface {id}: scenery freezes deterministically at the same time");
                if (id != 17)
                {
                    var plants = ((System.Collections.ICollection)wt.GetField("outdoorPlants", flags)!.GetValue(world)!).Count;
                    counts.Add($"{(char)('A' + (id - 1) % 6)}{(id - 1) / 6 + 1}, {id}, {plants}");
                    check(plants is > 0 and < 100, $"Surface {id}: bounded habitat planting leaves room for routes and encounters ({plants} clumps)");
                }
                Array.Clear(seen); seen[6 * Map.Height + 6] = true;
                var fog = Render(() => World("DrawMap", batch, map, seen, 2d, town));
                check(Enumerable.Range(0, fog.Length).Where(i => !seen[i % 416 / 32 * Map.Height + i / 416 / 32])
                    .Select(i => fog[i]).Distinct().Count() == 1, $"Surface {id}: planting and shadows cannot reveal unexplored cells");
                check(before.SequenceEqual(GameSaveCodec.Encode(scene.Session, catalog)), $"Surface {id}: complete gameplay, map state and RNG stay unchanged");
                if (id == 17) continue;
                // Review the actual game presentation, with lighting and weather, as well as the map view.
                foreach (var mode in new[] { 1, 3 })
                {
                    Set("timeOfDayMode", mode); Set("sceneTime", 2d); Set("outdoorWeatherTime", 0d);
                    Capture($"play-{id:D3}-{(mode == 1 ? "day" : "night")}.png");
                }
            }
            File.WriteAllLines(Path.Combine(directory, "region-coverage.csv"), new[] { "region,map,plant_clumps", "E3,17,existing Maynard treatment" }.Concat(counts));
            var meadow = catalog.CreateMap(16)!;
            var all = Enumerable.Repeat(true, Map.TileCount).ToArray();
            meadow.RestoreTile(new(2, 2), 2);
            var ordinary = Render(() => World("DrawMap", batch, meadow, all, 2d, town));
            meadow.RestoreTile(new(2, 2), 0x46);
            check(ordinary.SequenceEqual(Render(() => World("DrawMap", batch, meadow, all, 2d, town))), "Secret grass has identical soil and vegetation to ordinary grass");
            foreach (var pair in new[] { (Normal: (ushort)0x31, Secret: (ushort)0x56), (Normal: (ushort)0x2e, Secret: (ushort)0x57) })
            {
                meadow.RestoreTile(new(2, 2), pair.Normal);
                ordinary = Render(() => World("DrawMap", batch, meadow, all, 2d, town));
                meadow.RestoreTile(new(2, 2), pair.Secret);
                check(ordinary.SequenceEqual(Render(() => World("DrawMap", batch, meadow, all, 2d, town))), $"Secret tree {pair.Secret:x} retains ordinary planting, canopy motion and leaf litter");
            }
            var field = wt.GetMethod("MeadowField", BindingFlags.Static | BindingFlags.NonPublic)!;
            float Field(int x, int y) => (float)field.Invoke(null, [x, y])!;
            check(Enumerable.Range(0, 120).All(i => Field(i * 17, i * 11) == Field(i * 17 + 2112, i * 11 + 1920)), "Ground variation wraps seamlessly across the six-by-six world");
            var dryScene = Scene(8);
            var dryHabitat = ((float Dry, float Cold, float Wood))World("OutdoorHabitatAt", dryScene.Map, new Vector2(9, 8))!;
            var snowHabitat = ((float Dry, float Cold, float Wood))World("OutdoorHabitatAt", catalog.CreateMap(13)!, new Vector2(6, 6))!;
            check(dryHabitat.Dry > .8f && dryHabitat.Cold == 0 && snowHabitat.Cold > .5f,
                "Habitat sound follows authored sand and snow footprints rather than whole-region labels");
            var riverMap = catalog.CreateMap(3)!;
            (float Gain, float Pan) Water(float x, float y) => ((float, float))World("OutdoorWaterSoundAt", riverMap, new Vector2(x, y))!;
            check(Water(10, 2).Gain > Water(8, 2).Gain && Water(5, 2).Gain == 0, "Outdoor river sound falls to silence inland");
            check(Water(9, 2).Pan > 0 && Water(12, 2).Pan < 0, "Outdoor water pans toward its actual bank");
            var forest = Scene(20);
            var leaves = Render(() => World("DrawOutdoorActivity", batch, forest, 2d, 0f, 1f)); Save("woodland-particles.png");
            check(leaves.Any(c => c.A > 0) && leaves.Count(c => c.A > 0) < 1000, "Woodland leaves and insects remain sparse native pixels");
            check(leaves.SequenceEqual(Render(() => World("DrawOutdoorActivity", batch, forest, 2d, 0f, 1f)))
                && !leaves.SequenceEqual(Render(() => World("DrawOutdoorActivity", batch, forest, 4d, 0f, 1f))), "Outdoor movement is deterministic and advances with scene time");
            foreach (var id in new[] { 8, 27, 20, 22 })
            {
                var scene = Scene(id); Set("timeOfDayMode", 1); Set("outdoorWeatherTime", 0d);
                for (var frame = 0; frame < 48; frame++) { Set("sceneTime", frame / 12d); Capture($"clip-{id}-{frame:D3}.png"); }
                Set("outdoorWeatherTime", RainTime()); Capture($"rain-{id}.png");
            }
            Audio("Update", false); Audio("StopOutdoorNature"); Audio("StopRiver");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
            Scene(20); Set("timeOfDayMode", 1); Set("outdoorWeatherTime", 0d);
            Call("UpdateCaveAmbience", 1d);
            var birds = Voice("outdoorBirds"); var wind = Voice("ambience");
            check(birds is { State: SoundState.Playing, IsLooped: true } && wind is { State: SoundState.Playing, IsLooped: true }, "Outdoor ambience plays bounded independent nature and wind loops");
            var birdLevel = birds!.Volume;
            Audio("UpdateOutdoorAmbience", 1f, 0f, 0f, 1f, 0f, true, 1d);
            check(ReferenceEquals(birds, Voice("outdoorBirds")) && ReferenceEquals(wind, Voice("ambience")) && birds.Volume < birdLevel, "Entering dry ground fades bird activity without restarting ambience");
            foreach (var gate in new[] { "paused", "mapOpen" })
            {
                Set(gate, true); Call("UpdateCaveAmbience", .1d);
                check(birds.State == SoundState.Paused && wind!.State == SoundState.Paused, $"Outdoor sound obeys {gate}");
                Set(gate, false); Call("UpdateCaveAmbience", .1d);
            }
            Audio("Update", false);
            check(birds.State == SoundState.Paused && wind!.State == SoundState.Paused, "Focus loss silences outdoor ambience");
            at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d);
            check(birds.State == SoundState.Paused, "Sound Off mutes outdoor nature");
            at.GetProperty("Enabled")!.SetValue(audio, true); Call("UpdateCaveAmbience", .1d);
            Set("timeOfDayMode", 3);
            for (var i = 0; i < 8; i++) Call("UpdateCaveAmbience", .5d);
            var insects = Voice("outdoorNight");
            check(birds.IsDisposed && insects is { State: SoundState.Playing, IsLooped: true } && ReferenceEquals(wind, Voice("ambience")),
                "Night crossfades daytime birds to quiet crickets while preserving wind playback");
            at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d);
            check(insects!.State == SoundState.Paused && wind!.State == SoundState.Paused, "Zero master volume silences both night layers");
            at.GetProperty("Volume")!.SetValue(audio, .5f);
            for (var i = 0; i < 8; i++) Audio("UpdateOutdoorAmbience", 0f, 1f, 0f, 0f, 0f, true, .5d);
            check(insects.IsDisposed && Voice("outdoorNight") is null && Voice("outdoorBirds") is null, "Cold snow habitat suppresses warm-weather wildlife audio");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Scene(27); Set("timeOfDayMode", 1); Set("outdoorWeatherTime", 0d);
            var setupMs = timer.Elapsed.TotalMilliseconds;
            Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(buffer);
            timer.Restart();
            for (var frame = 0; frame < 30; frame++)
            {
                Set("sceneTime", frame / 30d); Call("Draw", new GameTime());
                host.GraphicsDevice.GetBackBufferData(buffer);
            }
            File.WriteAllText(Path.Combine(directory, "render-timing.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                scene = "Ervyn with normal presentation effects", fixture_setup_ms = setupMs,
                synchronized_draw_average_ms = timer.Elapsed.TotalMilliseconds / 30,
                frames = 30, includes_gpu_readback = true, excludes_png_encoding = true,
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Scene(17); Call("UpdateCaveAmbience", .8d);
            check(birds.IsDisposed && Voice("outdoorBirds") is null, "Returning to Maynard releases the extra outdoor nature voice");
        }
        finally
        {
            Audio("StopOutdoorNature"); Audio("StopRiver"); Audio("UpdateAmbience", false, false, 0d);
            at.GetProperty("Enabled")!.SetValue(audio, enabled); at.GetProperty("Volume")!.SetValue(audio, volume);
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position);
            World("SynchronizeMonsters", Get("adventure"), false, new Rectangle(0, 0, 416, 384));
            Call("Draw", new GameTime());
        }
        Adventure Scene(int id)
        {
            var template = catalog.CreateMap(id)!;
            var start = (from x in Enumerable.Range(1, 11) from y in Enumerable.Range(1, 10)
                where template.Tile(new(x, y)) is (2 or 8 or 0x16 or 0x2d)
                    && !MonsterPlacements.ForMap(id).Any(p => p.Contains(new(x, y)))
                orderby Math.Abs(x - 6) + Math.Abs(y - 6) select new MapPosition(x, y)).First();
            var scene = new Adventure(new GameSession(catalog, location: new() { MapId = id, RegionX = (id - 1) % 6 - 4,
                RegionY = 2 - (id - 1) / 6, Position = start }));
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) scene.Visibility.RevealFrom(scene.Map, new(x, y));
            Set("session", scene.Session); Set("adventure", scene); Set("renderedMap", scene.Map);
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false); Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d);
            player.SnapTo(start); Call("RestorePosition", start);
            World("SynchronizeMonsters", scene, false, new Rectangle(0, 0, 416, 384));
            // Prepare the map without advancing the simulation or altering the user's save.
            Render(() => World("DrawMap", batch, scene.Map, Enumerable.Repeat(true, Map.TileCount).ToArray(), 2d, town));
            return scene;
        }
        double RainTime()
        {
            var weather = typeof(GameInput).Assembly.GetType("Yipe.Desktop.Rain")!.GetMethod("WeatherAmount")!;
            return Enumerable.Range(300, 360).First(t => (float)weather.Invoke(null, [(double)t])! > .99f);
        }
        Color[] Render(Action draw)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); draw(); batch.End(); host.GraphicsDevice.SetRenderTarget(null);
            var data = new Color[416 * 384]; target.GetData(data); return data;
        }
        void Save(string name) { using var file = File.Create(Path.Combine(directory, name)); target.SaveAsPng(file, 416, 384); }
        void Capture(string name)
        {
            Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(buffer); presented.SetData(buffer);
            using var file = File.Create(Path.Combine(directory, name)); presented.SaveAsPng(file, 1280, 720);
        }
    }
}
