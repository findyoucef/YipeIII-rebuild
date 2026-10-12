using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class FireCaveChecks
{
    internal static void Run(Game host, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = host.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(host);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(host, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(host, args);
        var saved = new[] { "session", "adventure", "renderedMap", "journal", "cameraPosition", "sceneTime", "paused", "pauseAmount",
            "overlay", "mapOpen", "worldMapOpen", "saving", "loading", "studioSplashRemaining", "walkingStep", "pendingDirection",
            "miniatureMode", "scanlines", "vignette", "pixelLighting", "messageRemaining", "timeOfDayMode" }.ToDictionary(n => n, Get);
        var world = Get("world")!; var wt = world.GetType(); var town = Get("town")!;
        var player = (PlayerMotion)Get("player")!; var position = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!;
        object? Invoke(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var terrain = (Texture2D)wt.GetField("fireGround", flags)!.GetValue(world)!;
        var palette = (Color[])wt.GetField("FirePalette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var directory = Path.Combine(output, "fire-cave"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        var lightType = type.Assembly.GetType("Yipe.Desktop.PixelLighting")!;
        using var light = (IDisposable)Activator.CreateInstance(lightType, [host.GraphicsDevice, Get("pixel")!, 416, 384, world])!;
        var lightTarget = (RenderTarget2D)lightType.GetField("lightMap", flags)!.GetValue(light)!;
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var presentedPixels = new Color[1280 * 720];
        var terrains = new Dictionary<int, Color[]>();
        var audio = Get("audio")!; var at = audio.GetType();
        var soundEnabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!;
        var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
        SoundEffectInstance? Voice() => (SoundEffectInstance?)at.GetField("ambience", flags)!.GetValue(audio);
        void Silence() => at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            foreach (var id in new[] { 67, 71, 72, 73, 95, 100, 101, 107, 111, 112, 113 })
            {
                var game = Scene(id); var snapshot = GameSaveCodec.Encode(game.Session, catalog);
                Invoke("PrepareFire", game.Map); var original = Ground(); terrains[id] = original;
                var lit = Light(game); Save(lit, $"light-{id}.png");
                check(lit.Any(p => p.R > 2 * p.G / 3 && p.G > p.B && p.A > 0), $"Fire {id}: molten openings cast warm local light");
                check(original.All(p => p.A == 255 && palette.Contains(p)), $"Fire {id}: opaque native terrain uses the restrained volcanic palette");
                var molten = (bool[])wt.GetField("fireMolten", flags)!.GetValue(world)!;
                check(molten.Count(p => p) > 150 && Enumerable.Range(0, molten.Length)
                    .All(i => !molten[i] || game.Map.Tile(new(i % 416 / 32, i / 416 / 32)) is 0x25 or 0x5a),
                    $"Fire {id}: substantial exposed magma stays inside existing blocked rock");
                Invoke("PrepareFire", game.Map);
                check(original.SequenceEqual(Ground()), $"Fire {id}: terrain cache is stable");
                var pixels = World(game, 1); var later = World(game, 1.8);
                check(pixels.SequenceEqual(World(game, 1)) && !pixels.SequenceEqual(later),
                    $"Fire {id}: magma and particles animate with scene time and repeat exactly when paused");
                Save(pixels, $"map-{id}.png");
                var sources = (Point[])Invoke("FireLights", game.Map)!;
                var nearHeat = (float)Invoke("FireHeatAt", sources[0].ToVector2())!;
                check(nearHeat > .8f && (bool)wt.GetProperty("HasHeat", flags)!.GetValue(world)!, $"Fire {id}: visible magma activates localized heat");
                var air = Air(game, 1); check(air.Any(p => p.A > 0) && air.Count(p => p.A > 0) < 1000,
                    $"Fire {id}: embers, ash and smoke remain sparse native pixels");
                game.Visibility.Initialize(game.Map, new(1, 1)); var fog = Air(game, 1); var fogLight = Light(game);
                check(Enumerable.Range(0, fog.Length).All(i => game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32)) || (fog[i].A == 0 && fogLight[i].A == 0)),
                    $"Fire {id}: particles and light respect undiscovered terrain");
                var dark = new Map(id, 3, Enumerable.Repeat((ushort)0x25, Map.TileCount).ToArray());
                for (var y = 0; y < 12; y++)
                {
                    game.Visibility.Initialize(dark, new(6, y));
                    if (sources.All(p => !game.Visibility.Contains(new(p.X / 32, p.Y / 32)))) break;
                }
                check(Light(game).All(p => p.A == 0), $"Fire {id}: hidden magma sources cast no visible light");
                Reveal(game);
                var aliases = new List<(MapPosition Cell, ushort Tile)>();
                for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
                {
                    var cell = new MapPosition(x, y); var tile = game.Map.Tile(cell);
                    if (tile is not (0x25 or 0x48)) continue;
                    aliases.Add((cell, tile)); game.Map.RestoreTile(cell, tile == 0x25 ? (ushort)0x5a : (ushort)0x28);
                }
                Invoke("PrepareFire", game.Map);
                check(original.SequenceEqual(Ground()) && lit.SequenceEqual(Light(game)), $"Fire {id}: secret wall and floor aliases have identical appearance");
                foreach (var entry in aliases) game.Map.RestoreTile(entry.Cell, entry.Tile);
                var source = sources[0]; var changed = new MapPosition(source.X / 32, source.Y / 32); var before = game.Map.Tile(changed);
                game.Map.RestoreTile(changed, 0x28); Invoke("PrepareFire", game.Map);
                check(!original.SequenceEqual(Ground()) && !lit.SequenceEqual(Light(game)), $"Fire {id}: removing source rock invalidates magma and light caches");
                game.Map.RestoreTile(changed, before); Invoke("PrepareFire", game.Map);
                check(original.SequenceEqual(Ground()) && lit.SequenceEqual(Light(game)), $"Fire {id}: restoring source rock restores its exact rendering");
                Set("pixelLighting", true); Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("sceneTime", 1d);
                Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(presentedPixels);
                check(presentedPixels.All(p => p.A == 255), $"Fire {id}: heat and the default presentation preserve opaque output");
                presented.SetData(presentedPixels);
                using (var file = File.Create(Path.Combine(directory, $"gameplay-{id}.png"))) presented.SaveAsPng(file, 1280, 720);
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Fire {id}: the complete save, hazards, encounters and RNG are unchanged");
            }
            // Original borders often change wall layout at transitions. Compare
            // the shared walkable overlap interior, away from different wall feet.
            foreach (var (left, right) in new[] { (71, 72), (72, 73), (100, 101), (111, 112), (112, 113) })
            {
                var a = catalog.CreateMap(left)!; var b = catalog.CreateMap(right)!; var count = 0; var differences = 0;
                for (var y = 0; y < 12; y++) for (var x = 0; x < 2; x++)
                {
                    if (a.Tile(new(x + 11, y)) is not (0x28 or 0x48) || b.Tile(new(x, y)) is not (0x28 or 0x48)) continue;
                    for (var py = 12; py < 24; py++) for (var px = 8; px < 24; px++)
                    {
                        count++;
                        if (terrains[left][(y * 32 + py) * 416 + (x + 11) * 32 + px]
                            != terrains[right][(y * 32 + py) * 416 + x * 32 + px]) differences++;
                    }
                }
                check(count > 0 && differences == 0, $"Fire {left}/{right}: shared walkable overlap stays world-aligned ({differences}/{count} differ)");
            }

            var room = Scene(72); World(room, 1);
            var rendered = World(room, 1); var colors = rendered.ToHashSet();
            var heat = Get("heat")!;
            Color[] Heat(double time)
            {
                var texture = (Texture2D)heat.GetType().GetMethod("Apply")!.Invoke(heat,
                    [batch, target, world, Vector2.Zero, player.VisualPosition * 32, time, true, 1f])!;
                host.GraphicsDevice.SetRenderTarget(null); var data = new Color[texture.Width * texture.Height]; texture.GetData(data); return data;
            }
            var hot = Heat(1);
            check(hot.All(colors.Contains) && !hot.SequenceEqual(Heat(2)) && hot.SequenceEqual(Heat(1)),
                "Fire heat moves existing pixel colours, stays crisp and freezes exactly with scene time");
            Set("sceneTime", 1d); Set("timeOfDayMode", 1); Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(presentedPixels);
            var day = (Color[])presentedPixels.Clone(); Set("timeOfDayMode", 3); Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(presentedPixels);
            check(day.SequenceEqual(presentedPixels), "Fire Cave keeps its internal heat and lighting through surface night"); Set("timeOfDayMode", 1);

            // Exercise the room-wide pass on real terrain/actors with black and
            // transparent controls; illumination must not add a flat red veil.
            room.Visibility.Initialize(room.Map, new(1, 1));
            var shaded = World(room, 1);
            shaded[0] = Color.Black; shaded[1] = Color.Transparent;
            using (var input = new Texture2D(host.GraphicsDevice, 416, 384))
            {
                input.SetData(shaded);
                var upper = Ambient(false); var lower = Ambient(true);
                var black = 0; var changedBlack = 0; var changedAlpha = 0; var brighter = 0;
                for (var i = 0; i < shaded.Length; i++)
                {
                    if (shaded[i].R == 0 && shaded[i].G == 0 && shaded[i].B == 0)
                    { black++; if (upper[i] != shaded[i] || lower[i] != shaded[i]) changedBlack++; }
                    if (upper[i].A != shaded[i].A || lower[i].A != shaded[i].A) changedAlpha++;
                    if (upper[i].R > shaded[i].R) brighter++;
                }
                var deepGain = lower.Sum(p => (long)p.R) - upper.Sum(p => (long)p.R);
                check(black > 0 && changedBlack == 0 && changedAlpha == 0 && brighter > 1000 && deepGain > 0,
                    $"Room-wide red glow preserves black/transparent controls and alpha, with stronger deep bounce (black {black}, changed {changedBlack}/{changedAlpha}, bright {brighter}, deep {deepGain})");
                Color[] Ambient(bool deep)
                {
                    host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
                    batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
                    batch.Draw(input, Vector2.Zero, Color.White); batch.End();
                    lightType.GetMethod("ApplyFireAmbient")!.Invoke(light, [batch, deep]);
                    host.GraphicsDevice.SetRenderTarget(null);
                    var data = new Color[416 * 384]; target.GetData(data); return data;
                }
            }

            var materials = new HashSet<string>();
            foreach (var id in new[] { 67, 72, 95, 113 })
            {
                var floor = Scene(id).Map;
                for (var x = 1; x < 12; x++) for (var y = 1; y < 11; y++)
                    if (floor.Tile(new(x, y)) == 0x28) materials.Add(Invoke("FootstepAt", floor, new MapPosition(x, y))!.ToString()!);
            }
            check(materials.SetEquals(["Stone", "Sand"]), "Fire footsteps use recorded stone and loose-grit variants on their matching materials");

            // A separated volcanic face casts a partial-cell shadow, and doors
            // still block/restoring them invalidates the shared contour mask.
            var corner = Scene(72);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) corner.Map.RestoreTile(new(x, y), 0x28);
            corner.Map.RestoreTile(new(3, 4), 0x25); Reveal(corner); var clearLight = Light(corner);
            corner.Map.RestoreTile(new(4, 3), 0x25); var cornerLight = Light(corner);
            check(cornerLight[126 * 416 + 130].R > 0 && cornerLight[118 * 416 + 140].R == 0,
                "Volcanic light grazes a carved corner but stops inside the adjacent solid face");
            corner.Map.RestoreTile(new(4, 3), 0x28); corner.Map.RestoreTile(new(3, 5), 0x3e);
            check(clearLight[183 * 416 + 112].R > 0 && Light(corner)[183 * 416 + 112].R == 0,
                "Volcanic light cannot pass through a closed door");
            corner.Map.RestoreTile(new(3, 5), 0x28);
            check(clearLight.SequenceEqual(Light(corner)), "Opening the volcanic light path removes its cached door shadow");

            room = Scene(72); var beforeAudio = GameSaveCodec.Encode(room.Session, catalog);
            check((string)at.GetProperty("Error")!.GetValue(audio)! == "", "Volcanic ambience loads on the real audio device");
            var sound = (SoundEffect)at.GetField("fireAmbience", flags)!.GetValue(audio)!;
            check(sound.Duration.TotalSeconds is > 57 and < 59, "Volcanic ambience retains the 58-second sample-based loop");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f); Silence();
            Call("UpdateCaveAmbience", .1d); var voice = Voice()!; var quiet = voice.Volume; Call("UpdateCaveAmbience", .8d);
            check(voice.IsLooped && voice.State == SoundState.Playing && voice.Volume > quiet && voice.Volume < .16f,
                "Volcanic ambience fades in below actions and uses one looping voice");
            Set("paused", true); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Pause silences volcanic ambience");
            Set("paused", false); Call("UpdateCaveAmbience", .1d);
            check(ReferenceEquals(voice, Voice()) && voice.State == SoundState.Playing, "Resume preserves the same volcanic voice");
            Set("mapOpen", true); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "The map silences volcanic ambience");
            Set("mapOpen", false); Call("ShowActions"); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "Action and combat overlays silence volcanic ambience"); Set("overlay", null);
            Call("UpdateCaveAmbience", .1d); at.GetMethod("Update")!.Invoke(audio, [false]);
            check(voice.State == SoundState.Paused, "Focus loss pauses volcanic ambience");
            at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "Sound Off suppresses volcanic ambience");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "Master volume zero suppresses volcanic ambience");
            at.GetProperty("Volume")!.SetValue(audio, .5f); Call("UpdateCaveAmbience", .1d);
            var upperAmount = (float)Invoke("FireAmbienceAt", room)!;
            var deep = Scene(113); var deepAmount = (float)Invoke("FireAmbienceAt", deep)!;
            check(deepAmount > upperAmount, "The deep chambers have stronger volcanic ambience at the representative route");
            Call("UpdateCaveAmbience", .1d); check(ReferenceEquals(voice, Voice()), "Moving between Fire Cave rooms keeps one continuous ambience voice");
            Scene(59); Call("UpdateCaveAmbience", .1d); var breedingVoice = Voice();
            check(voice.IsDisposed && breedingVoice is not null && !ReferenceEquals(voice, breedingVoice), "Entering the breeding grounds switches to its original cave loop");
            Scene(54); Call("UpdateCaveAmbience", .1d);
            check(Voice() is { State: SoundState.Playing } && breedingVoice!.IsDisposed, "Entering Final Cave replaces the old ambient voice");
            check(beforeAudio.SequenceEqual(GameSaveCodec.Encode(room.Session, catalog)), "Volcanic audio updates preserve gameplay and RNG");
            foreach (var id in new[] { 17, 53, 57, 59, 60, 105, 117, 121 })
            {
                var outside = Scene(id);
                check(Air(outside, 1).All(p => p.A == 0), $"Volcanic particles remain absent in unrelated map {id}");
            }
            foreach (var (monster, scale) in new[] { (12, 1), (14, 1), (16, 2) })
            {
                Scene(57); var outside = Actor(); var portrait = Portrait();
                Scene(72); var inside = Actor();
                check(Enumerable.Range(0, inside.Length).All(i => (inside[i].A > 0) == (outside[i].A > 0))
                    && Enumerable.Range(0, inside.Length).Any(i => outside[i].A == 255 && inside[i] != outside[i]),
                    $"Volcanic creature {monster}: warm integration preserves outline, hover and scale");
                check(portrait.SequenceEqual(Portrait()), $"Volcanic creature {monster}: battle art is unchanged");
                Scene(57); check(outside.SequenceEqual(Actor()), $"Volcanic creature {monster}: leaving restores the ordinary appearance");
                Color[] Actor() => Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, null!));
                Color[] Portrait() => Render(() => Invoke("DrawAnimatedMonster", batch, monster, new Rectangle(32, 32, 48, 48), 0, false, null!));
            }
            room = Scene(113);
            for (var frame = 0; frame < 48; frame++)
            {
                Save(World(room, frame / 8d), $"frame-{frame:D2}.png");
                Set("sceneTime", frame / 8d); Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(presentedPixels);
                presented.SetData(presentedPixels);
                using var file = File.Create(Path.Combine(directory, $"play-{frame:D2}.png")); presented.SaveAsPng(file, 1280, 720);
            }
        }
        finally
        {
            Silence(); at.GetProperty("Enabled")!.SetValue(audio, soundEnabled); at.GetProperty("Volume")!.SetValue(audio, volume);
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        Adventure Scene(int id)
        {
            var layer = id >= 117 ? 3 : (id - 1) / 40; var cell = id - (layer == 3 ? 98 : layer * 40) - 1;
            var map = catalog.CreateMap(id)!; var at = new MapPosition(6, id == 113 ? 7 : 5);
            if (map.Tile(at) is 0x25 or 0x23 or 0x22) at = new(5, 5);
            var scene = new Adventure(new GameSession(catalog, location: new() { MapId = id, MapLayer = layer,
                AlternateMapSet = id >= 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = at }));
            Set("session", scene.Session); Set("adventure", scene); Reveal(scene); Call("SynchronizeWorld", false); return scene;
        }
        static void Reveal(Adventure scene) { for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) scene.Visibility.RevealFrom(scene.Map, new(x, y)); }
        Color[] Ground() { var pixels = new Color[416 * 384]; terrain.GetData(pixels); return pixels; }
        Color[] Light(Adventure scene)
        {
            lightType.GetMethod("Prepare")!.Invoke(light, [batch, scene, Matrix.Identity, 1d]); host.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; lightTarget.GetData(pixels); return pixels;
        }
        Color[] Render(Action draw)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); draw(); batch.End(); host.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        Color[] World(Adventure scene, double time) => Render(() => Invoke("Draw", batch, scene, player, town, time, false, 0f));
        Color[] Air(Adventure scene, double time) => Render(() => Invoke("DrawFireAtmosphere", batch, scene, time));
        void Save(Color[] pixels, string name)
        {
            using var texture = new Texture2D(host.GraphicsDevice, 416, 384); texture.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); texture.SaveAsPng(file, 416, 384);
        }
    }
}
