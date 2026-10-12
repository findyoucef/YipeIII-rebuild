using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class MaynardAtmosphereChecks
{
    internal static void Run(Game host, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = host.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(host);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(host, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(host, args);
        var saved = new[] { "session", "adventure", "renderedMap", "map", "town", "journal", "cameraPosition", "sceneTime", "paused", "pauseAmount",
            "overlay", "mapOpen", "worldMapOpen", "saving", "loading", "studioSplashRemaining", "walkingStep", "pendingDirection",
            "miniatureMode", "scanlines", "vignette", "pixelLighting", "messageRemaining", "timeOfDayMode" }.ToDictionary(n => n, Get);
        var player = (PlayerMotion)Get("player")!; var position = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!;
        var townScene = new Adventure(new GameSession(catalog, location: new() { MapId = 17, RegionX = 0, RegionY = 0, Position = new(3, 3) }));
        var before = GameSaveCodec.Encode(townScene.Session, catalog);
        var tt = Get("town")!.GetType();
        using var town = (IDisposable)Activator.CreateInstance(tt, [host.GraphicsDevice, new TownMap(townScene.Map), Get("contentDirectory")!, catalog])!;
        object? Town(string name, params object[] args) => tt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(town, args);
        var audio = Get("audio")!; var at = audio.GetType();
        var enabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!; var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
        SoundEffectInstance? Voice(string name) => (SoundEffectInstance?)at.GetField(name, flags)!.GetValue(audio);
        var directory = Path.Combine(output, "maynard-atmosphere"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var pixels = new Color[1280 * 720];
        try
        {
            Set("town", town); Set("map", new TownMap(townScene.Map)); Set("journal", new MapJournal());
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("timeOfDayMode", 1);
            Scene(17);
            var leaf = Leaves(2); Save("leaves.png");
            check(leaf.Count(p => p.A > 0) is > 40 and < 350, "Maynard leaves are visible but occupy a sparse number of native pixels");
            check(leaf.SequenceEqual(Leaves(2.01)) && !leaf.SequenceEqual(Leaves(3)), "Leaves flutter on a discrete scene clock and freeze exactly");
            check(Enumerable.Range(0, leaf.Length).Where(i => leaf[i].A > 0).All(i =>
                townScene.Map.Tile(new(i % 416 / 32, i / 416 / 32)) is 2 or 8 or 22 or 25 or 26 or 50), "Leaves stay over open grass and paths");
            // Isolate leaves/scenery from the King, which has separate NPC coverage.
            var native = Render(() => Town("Draw", batch, player, 2d, false, 0f, null!)); Save("native-town.png");
            var buildings = Render(() => { for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) Town("DrawBuilding", batch, townScene.Map.Tile(new(x, y)), new MapPosition(x, y), 2d, false); });
            check(Enumerable.Range(0, buildings.Length).Where(i => buildings[i].A == 255).All(i => native[i] == buildings[i]), "Leaves cannot cover opaque shop and castle artwork");
            var hero = Render(() => Town("DrawHero", batch, player, 2d, false, false));
            check(Enumerable.Range(0, hero.Length).Where(i => hero[i].A == 255).All(i => native[i] == hero[i]), "Leaves preserve the player's complete silhouette");
            (float Gain, float Pan) River(float x, float y) => ((float, float))Town("RiverSoundAt", new Vector2(x, y))!;
            check(River(3, 3).Gain > .95f && River(2, 6).Gain > .95f, "Both bridges retain the sound of water beneath them");
            var amounts = new[] { River(2, 6).Gain, River(3, 6).Gain, River(4, 6).Gain, River(6, 6).Gain };
            check(amounts.Zip(amounts.Skip(1), (a, b) => a > b).All(v => v) && amounts[^1] == 0, "River loudness falls away to silence inland");
            check(River(0, 6).Pan > 0 && River(4, 6).Pan < 0, "The river pans toward the correct bank");
            var smooth = Enumerable.Range(0, 81).Select(i => River(2 + i / 20f, 6).Gain).ToArray();
            check(smooth.Zip(smooth.Skip(1), (a, b) => Math.Abs(a - b)).Max() < .05f, "River distance follows interpolated walking positions without tile-sized jumps");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
            at.GetMethod("Update")!.Invoke(audio, [false]); at.GetMethod("StopRiver")!.Invoke(audio, null);
            at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
            Call("UpdateCaveAmbience", .1d); var ambient = Voice("ambience")!; var river = Voice("river")!;
            check(ambient is not null && river is not null && ambient.IsLooped && river.IsLooped && ambient.State == SoundState.Playing && river.State == SoundState.Playing,
                "Maynard starts independent outdoor and nearby-river recordings");
            var firstRiver = river!.Volume; Call("UpdateCaveAmbience", .8d);
            check(Math.Abs(ambient!.Volume - .09f) < .001f && river.Volume > firstRiver && river.Volume <= .12f, "Town and river loops fade in at restrained master-volume levels");
            foreach (var gate in new[] { "paused", "mapOpen" })
            {
                Set(gate, true); Call("UpdateCaveAmbience", .1d);
                check(ambient.State == SoundState.Paused && river.State == SoundState.Paused, $"{gate} silences both Maynard ambient layers");
                Set(gate, false); Call("UpdateCaveAmbience", .1d);
            }
            check(ReferenceEquals(ambient, Voice("ambience")) && ReferenceEquals(river, Voice("river"))
                && ambient.State == SoundState.Playing && river.State == SoundState.Playing, "Resume continues both loops without restarting or stacking voices");
            Call("ShowActions"); Call("UpdateCaveAmbience", .1d);
            check(ambient.State == SoundState.Paused && river.State == SoundState.Paused, "Action and shop overlays silence Maynard ambience"); Set("overlay", null);
            Call("UpdateCaveAmbience", .1d); at.GetMethod("Update")!.Invoke(audio, [false]);
            check(ambient.State == SoundState.Paused && river.State == SoundState.Paused, "Focus loss pauses both Maynard ambient layers");
            at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d);
            check(ambient.State == SoundState.Paused && river.State == SoundState.Paused, "Sound Off mutes both Maynard layers");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d);
            check(ambient.State == SoundState.Paused && river.State == SoundState.Paused, "Zero master volume mutes both Maynard layers");
            at.GetProperty("Volume")!.SetValue(audio, .5f); Call("UpdateCaveAmbience", .8d); var riverVolume = river.Volume;
            at.GetProperty("Volume")!.SetValue(audio, .25f); Call("UpdateCaveAmbience", 0d);
            check(Math.Abs(river.Volume - riverVolume / 2) < .001f && Math.Abs(ambient.Volume - .045f) < .001f, "Master volume scales both recordings immediately");
            at.GetProperty("Volume")!.SetValue(audio, .5f); Call("UpdateCaveAmbience", 0d);
            at.GetMethod("Play")!.Invoke(audio, [12, false]); Call("UpdateCaveAmbience", 0d);
            check(Math.Abs(river.Volume - riverVolume * .4f) < .001f && Math.Abs(ambient.Volume - .036f) < .001f, "Action sounds duck both layers without masking their cues");
            at.GetMethod("Update")!.Invoke(audio, [false]); Call("UpdateCaveAmbience", .1d);
            Set("timeOfDayMode", 3); Call("UpdateCaveAmbience", .8d); var night = Voice("ambience")!;
            check(ambient.IsDisposed && ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("townNightAmbience", flags)!.GetValue(audio))
                && ReferenceEquals(river, Voice("river")), "Night replaces daytime birds with quieter recorded wind and preserves the river");
            Set("timeOfDayMode", 1); Call("UpdateCaveAmbience", .8d); ambient = Voice("ambience")!;
            check(night.IsDisposed && ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("townAmbience", flags)!.GetValue(audio)), "Day restores the bird-and-wind ambience");
            player.SnapTo(new(10, 8)); Call("UpdateCaveAmbience", .1d);
            check(river.Volume > 0 && river.Volume < riverVolume, "Walking inland fades the river rather than cutting it off");
            for (var i = 0; i < 24; i++) Call("UpdateCaveAmbience", .1d);
            check(river.IsDisposed && Voice("river") is null && ReferenceEquals(ambient, Voice("ambience")), "Distant river playback is released while town ambience continues");
            player.SnapTo(new(2, 6)); Call("UpdateCaveAmbience", .8d); river = Voice("river")!;
            Scene(117); Call("UpdateCaveAmbience", .8d);
            check(river.IsDisposed && Voice("river") is null && ambient.IsDisposed && Voice("ambience")!.State == SoundState.Playing, "Entering Hell releases both town layers and selects Hell ambience");
            Scene(17); Call("UpdateCaveAmbience", .8d); river = Voice("river")!; ambient = Voice("ambience")!;
            var leavingRiverVolume = river.Volume;
            Scene(8); Call("UpdateCaveAmbience", .1d);
            check(ReferenceEquals(river, Voice("river")) && river.Volume < leavingRiverVolume && ambient.IsDisposed
                && Voice("ambience") is { State: SoundState.Playing }, "Leaving Maynard selects habitat ambience and smoothly fades the former river");
            for (var i = 0; i < 24; i++) Call("UpdateCaveAmbience", .1d);
            check(river.IsDisposed && Voice("river") is null, "The previous town river reaches silence and releases its voice on dry ground");
            check(before.SequenceEqual(GameSaveCodec.Encode(townScene.Session, catalog)), "Maynard leaves and positional audio preserve complete gameplay and RNG");
            Scene(17); Set("sceneTime", 2d); Capture("bridge-day.png");
            foreach (var (name, viewpoint) in new[] { ("castle", new MapPosition(11, 2)), ("cave", new MapPosition(1, 2)) })
            {
                player.Face(new(0, -1)); Call("RestorePosition", viewpoint);
                Capture(name + "-day.png");
                Set("timeOfDayMode", 3); Capture(name + "-night.png"); Set("timeOfDayMode", 1);
            }
            player.SnapTo(new(5, 5)); Call("RestorePosition", new MapPosition(5, 5)); Capture("square-day.png");
            var captureBefore = GameSaveCodec.Encode(townScene.Session, catalog);
            Set("timeOfDayMode", 3); Capture("square-night.png"); Set("timeOfDayMode", 1);
            for (var frame = 0; frame < 96; frame++) { Set("sceneTime", frame / 12d); Capture($"play-{frame:D3}.png"); }
            check(captureBefore.SequenceEqual(GameSaveCodec.Encode(townScene.Session, catalog)), "Maynard atmosphere rendering and audio preserve complete gameplay and RNG");
        }
        finally
        {
            at.GetMethod("StopOutdoorNature")!.Invoke(audio, null);
            at.GetMethod("StopRiver")!.Invoke(audio, null); at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
            at.GetProperty("Enabled")!.SetValue(audio, enabled); at.GetProperty("Volume")!.SetValue(audio, volume);
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        void Scene(int id)
        {
            var scene = id == 17 ? townScene : new Adventure(new GameSession(catalog, location: new() { MapId = id, AlternateMapSet = id >= 117,
                RegionX = id >= 117 ? id - 121 : (id - 1) % 6 - 4, RegionY = id >= 117 ? -1 : 2 - (id - 1) / 6, Position = new(3, 3) }));
            Set("session", scene.Session); Set("adventure", scene); Set("renderedMap", scene.Map);
            Call("SynchronizeWorld", false);
        }
        Color[] Leaves(double time) => Render(() => Town("DrawLeaves", batch, time));
        Color[] Render(Action draw)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); draw(); batch.End(); host.GraphicsDevice.SetRenderTarget(null);
            var data = new Color[416 * 384]; target.GetData(data); return data;
        }
        void Save(string name) { using var file = File.Create(Path.Combine(directory, name)); target.SaveAsPng(file, 416, 384); }
        void Capture(string name)
        {
            Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(pixels); presented.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); presented.SaveAsPng(file, 1280, 720);
        }
    }
}
