using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class WaterCaveChecks
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
            "miniatureMode", "scanlines", "vignette", "pixelLighting", "messageRemaining", "timeOfDayMode", "iceDirection" }.ToDictionary(n => n, Get);
        var world = Get("world")!; var wt = world.GetType();
        object? Invoke(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var player = (PlayerMotion)Get("player")!; var position = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!;
        var directory = Path.Combine(output, "water-cave"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var lightType = type.Assembly.GetType("Yipe.Desktop.PixelLighting")!;
        using var light = (IDisposable)Activator.CreateInstance(lightType, [host.GraphicsDevice, Get("pixel")!, 416, 384, world])!;
        var lightTarget = (RenderTarget2D)lightType.GetField("lightMap", flags)!.GetValue(light)!;
        int[] rooms = [44, 45, 46, 50, 56, 62, 68, 74, 90, 96, 102, 108, 114];
        var terrains = new Dictionary<int, Color[]>();
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            foreach (var id in rooms.Concat(new[] { 17, 51, 53, 54, 57, 59, 67, 117 }))
            {
                var game = Scene(id); var snapshot = GameSaveCodec.Encode(game.Session, catalog);
                Save(World(game, 1), $"map-{id}.png"); Save(Light(game), $"light-{id}.png");
                Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("sceneTime", 1d);
                Gameplay(true, $"gameplay-{id}.png"); Gameplay(false, $"gameplay-{id}-unlit.png");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Water review {id}: rendering preserves full gameplay and RNG");
                if (rooms.Contains(id)) CheckRoom(game);
            }
            CheckBoundaries();
            CheckAudio();
            var moving = Scene(62); Set("miniatureMode", 2); Set("scanlines", true);
            var frozen = GameSaveCodec.Encode(moving.Session, catalog);
            for (var frame = 0; frame < 48; frame++) { Set("sceneTime", frame / 8d); Gameplay(true, $"motion-{frame:D2}.png"); }
            check(frozen.SequenceEqual(GameSaveCodec.Encode(moving.Session, catalog)), "Water atmosphere capture preserves raft positions, monsters and gameplay RNG");
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        void CheckRoom(Adventure game)
        {
            var id = game.Map.Id; var before = GameSaveCodec.Encode(game.Session, catalog);
            var image = World(game, 1);
            check(image.SequenceEqual(World(game, 1)), $"Water {id}: frozen presentation is deterministic");
            var texture = (Texture2D)wt.GetField("waterCaveGround", flags)!.GetValue(world)!;
            Color[] Ground() { Invoke("PrepareWaterCave", game.Map); var pixels = new Color[416 * 384]; texture.GetData(pixels); return pixels; }
            var ground = Ground(); terrains[id] = ground;
            var palette = (Color[])wt.GetField("WaterCavePalette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var rock = (bool[])Invoke("WaterCaveRock", game.Map)!;
            check(ground.All(c => c.A == 255 && palette.Contains(c)), $"Water {id}: native limestone terrain stays in its coherent palette");
            check(ReferenceEquals(rock, Invoke("WaterCaveRock", game.Map)), $"Water {id}: unchanged terrain reuses the cached contour");
            var walls = new List<float>(); var floor = new List<float>();
            for (var y = 0; y < 384; y++) for (var x = 0; x < 416; x++)
            {
                if (x % 32 is < 8 or > 23 || y % 32 is < 6 or > 18) continue;
                var c = ground[y * 416 + x]; (rock[y * 416 + x] ? walls : floor).Add(c.R * .2126f + c.G * .7152f + c.B * .0722f);
            }
            check(walls.Average() > floor.Average() * 1.25f, $"Water {id}: the quiet eroded floor sits below the raised limestone walls");
            var originalLight = Light(game);
            check(originalLight.Any(c => c.G > c.R && c.G > 0), $"Water {id}: visible algae motivates local turquoise light");
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
            {
                var p = new MapPosition(x, y); var tile = game.Map.Tile(p);
                if (tile == 0x25) game.Map.RestoreTile(p, 0x5a);
                else if (tile == 0x53) game.Map.RestoreTile(p, 0x49);
            }
            check(image.SequenceEqual(World(game, 1)) && originalLight.SequenceEqual(Light(game)),
                $"Water {id}: hidden wall/floor aliases have identical art and contour lighting");
            // Restore the exact authored/initialized terrain from an independent session.
            var restored = new GameSession(catalog, location: new() { MapId = id, MapLayer = (id - 1) / 40,
                RegionX = (id - 1) % 40 % 6 - 4, RegionY = 2 - (id - 1) % 40 / 6, Position = game.Location.Position });
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Map.RestoreTile(new(x, y), restored.CurrentMap.Tile(new(x, y)));
            var cell = (from y in Enumerable.Range(0, 12) from x in Enumerable.Range(0, 13) let p = new MapPosition(x, y)
                where game.Map.Tile(p) == 0x25 select p).First();
            game.Map.RestoreTile(cell, 0x53); var changed = Ground();
            check(!ground.SequenceEqual(changed) && !ReferenceEquals(rock, Invoke("WaterCaveRock", game.Map)),
                $"Water {id}: opening rock invalidates terrain and its contour together");
            game.Map.RestoreTile(cell, 0x25);
            check(ground.SequenceEqual(Ground()), $"Water {id}: restoring terrain restores its exact pixels");
            var wet = (from y in Enumerable.Range(0, 12) from x in Enumerable.Range(0, 13) let p = new MapPosition(x, y)
                where game.Map.Tile(p) is 0x0c or 0x0d select p).ToArray();
            if (wet.Length > 0)
                check(wet.All(p => Invoke("FootstepAt", game.Map, p)!.ToString()
                    == (game.Map.Events.Overlay is { Value: > 0 } raft && raft.Position == p ? "Wood" : "Water")),
                    $"Water {id}: actual pools retain splash contacts and rafts use wood contacts");
            check(Invoke("FootstepAt", game.Map, game.Location.Position)!.ToString() == "Stone", $"Water {id}: limestone routes retain stone contacts and normal traction");
            check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Water {id}: topology, secrets, encounters and full saved state remain intact");
        }
        void CheckBoundaries()
        {
            foreach (var id in rooms)
                foreach (var (next, dx, dy) in new[] { (id + 1, 11, 0), (id + 6, 0, 10) })
                {
                    if (!rooms.Contains(next) || (id - 1) / 40 != (next - 1) / 40) continue;
                    var a = catalog.CreateMap(id)!; var b = catalog.CreateMap(next)!; var count = 0; var different = 0;
                    for (var y = 0; y < (dy == 0 ? 12 : 2); y++) for (var x = 0; x < (dx == 0 ? 13 : 2); x++)
                    {
                        if (a.Tile(new(x + dx, y + dy)) is not (0x53 or 0x49) || b.Tile(new(x, y)) is not (0x53 or 0x49)) continue;
                        // Some original overlap rows intentionally have different adjacent
                        // banks/walls. Compare material continuity only where the authored
                        // neighborhood agrees; do not require new art to erase that layout.
                        var matching = true;
                        for (var sy = -1; sy <= 1; sy++) for (var sx = -1; sx <= 1; sx++)
                        {
                            var ap = new MapPosition(x + dx + sx, y + dy + sy); var bp = new MapPosition(x + sx, y + sy);
                            if (Map.Contains(ap) && Map.Contains(bp) && Normalize(a.Tile(ap)) != Normalize(b.Tile(bp))) matching = false;
                        }
                        if (!matching) continue;
                        for (var py = 12; py < 24; py++) for (var px = 8; px < 24; px++)
                        {
                            count++;
                            if (terrains[id][((y + dy) * 32 + py) * 416 + (x + dx) * 32 + px] != terrains[next][(y * 32 + py) * 416 + x * 32 + px]) different++;
                        }
                    }
                    if (count > 0) check(different == 0, $"Water {id}/{next}: matching authored overlap neighborhoods retain identical limestone and sediment ({different}/{count})");
                }
            static ushort Normalize(ushort tile) => tile switch { 0x49 => 0x53, 0x5a => 0x25, _ => tile };
            var room = Scene(44);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) room.Map.RestoreTile(new(x, y), 0x53);
            for (var x = 2; x < 10; x++) room.Map.RestoreTile(new(x, 2), 0x25);
            var lamp = ((Point[])Invoke("WaterCaveLights", room.Map)!).First(); var cell = new MapPosition(lamp.X / 32, lamp.Y / 32);
            for (var x = 2; x < 10; x++) if (x != cell.X) room.Map.RestoreTile(new(x, 2), 0x53);
            Reveal(room); var clear = Light(room); room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x3e); var blocked = Light(room);
            check(clear[(lamp.Y + 43) * 416 + lamp.X].G > 0 && blocked[(lamp.Y + 43) * 416 + lamp.X].G == 0,
                "Water algae light stops at a closed door");
            room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x53);
            check(clear.SequenceEqual(Light(room)), "Opening a Water Cave door restores the exact contour light");
            for (var y = cell.Y + 1; y < 12; y++) room.Map.RestoreTile(new(cell.X + 1, y), 0x25);
            var corner = Light(room); Save(corner, "corner-mask.png");
            var values = (from y in Enumerable.Range((cell.Y + 1) * 32, 32) from x in Enumerable.Range((cell.X + 1) * 32, 32)
                select corner[y * 416 + x].G).ToArray();
            check(values.Any(c => c > 0) && values.Any(c => c == 0), "Water light follows the carved corner instead of a square tile cutoff");
            room.Visibility.Initialize(new Map(44, 5, Enumerable.Repeat((ushort)0x25, Map.TileCount).ToArray()), new(0, 0));
            check(Light(room).All(c => c.G == 0), "Undiscovered Water Cave algae cannot reveal hidden terrain");

            room = Scene(62); Invoke("PrepareWaterCave", room.Map);
            Color[] Air(double time) => Render(() => Invoke("DrawWaterCaveAtmosphere", batch, room, time));
            var air = Air(1);
            check(air.Any(c => c.A > 0) && air.SequenceEqual(Air(1)) && !air.SequenceEqual(Air(2)),
                "Water drips, submerged caustics and thin mist animate only with presentation time");
            room.Visibility.Initialize(new Map(62, 5, Enumerable.Repeat((ushort)0x25, Map.TileCount).ToArray()), new(0, 0));
            check(Air(1).All(c => c.A == 0), "Water atmosphere cannot draw through fog");
            room = Scene(54);
            check(Air(1).All(c => c.A == 0), "Water atmosphere never leaks into the Final Cave");
            foreach (var (monster, scale) in new[] { (25, 1), (27, 1), (28, 2) })
            {
                Scene(54); var outside = Actor(); var portrait = Portrait(); Scene(62); var inside = Actor();
                check(Enumerable.Range(0, inside.Length).All(i => (inside[i].A > 0) == (outside[i].A > 0))
                    && Enumerable.Range(0, inside.Length).Any(i => inside[i].A == 255 && inside[i] != outside[i]),
                    $"Water creature {monster}: damp tint and contact shadow preserve silhouette and hover");
                check(portrait.SequenceEqual(Portrait()), $"Water creature {monster}: battle artwork remains unchanged");
                Scene(54); check(outside.SequenceEqual(Actor()), $"Water creature {monster}: its original palette returns outside the cave");
                Color[] Actor() => Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, null!));
                Color[] Portrait() => Render(() => Invoke("DrawAnimatedMonster", batch, monster, new Rectangle(32, 32, 48, 48), 0, false, null!));
            }
            room = Scene(44); var town = Get("town")!; var tt = town.GetType();
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) room.Map.RestoreTile(new(x, y), 0x0d);
            Invoke("PrepareWater", room.Map); player.SnapTo(new(5, 5));
            Color[] Reflection(double time) => Render(() => tt.GetMethod("DrawWorldReflection", flags)!.Invoke(town, [batch, player, time, room, world]));
            var reflected = Reflection(1); Save(reflected, "pool-reflection.png");
            check(reflected.Any(c => c.A > 0) && !reflected.SequenceEqual(Reflection(2)), "Water pools reflect the actual hero pose with subtle pixel ripples");
            room.Map.RestoreTile(new(5, 6), 0x25); Invoke("PrepareWater", room.Map); reflected = Reflection(1);
            check(Enumerable.Range(0, reflected.Length).All(i => reflected[i].A == 0 || (bool)Invoke("WaterAt", i % 416, i / 416)!),
                "Pool reflections are clipped to actual water at banks and walls");
            var dry = Scene(44); var dryBefore = GameSaveCodec.Encode(dry.Session, catalog);
            World(dry, 1); check((float)Invoke("WaterCaveWetness", dry.Map, new Vector2(6, 7))! > (float)Invoke("WaterCaveWetness", dry.Map, new Vector2(1, 1))!,
                "Water ambience grows gently near the actual pools");
            check(dryBefore.SequenceEqual(GameSaveCodec.Encode(dry.Session, catalog)), "Water proximity and reflection calculations preserve gameplay/RNG");
        }
        void CheckAudio()
        {
            var audio = Get("audio")!; var at = audio.GetType();
            var enabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!; var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
            SoundEffectInstance? Voice() => at.GetField("ambience", flags)!.GetValue(audio) as SoundEffectInstance;
            try
            {
                var game = Scene(62); var before = GameSaveCodec.Encode(game.Session, catalog);
                at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
                at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]); Call("UpdateCaveAmbience", 1d); var voice = Voice()!;
                check(voice.State == SoundState.Playing && ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("waterCaveAmbience", flags)!.GetValue(audio)),
                    "Water Cave selects its dedicated recorded ambience on the real audio device");
                check(voice.Volume is > 0 and <= .1f && voice.IsLooped, "Water Cave audio stays quiet, looped and under master volume");
                foreach (var field in new[] { "paused", "mapOpen", "worldMapOpen" })
                {
                    // World-map presentation is gated by mapOpen in production.
                    Set(field, true); if (field == "worldMapOpen") Set("mapOpen", true);
                    Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, $"Water ambience pauses for {field}");
                    Set(field, false); Set("mapOpen", false); Call("UpdateCaveAmbience", .1d);
                    check(voice.State == SoundState.Playing && ReferenceEquals(voice, Voice()), "Water ambience resumes the same voice without restarting");
                }
                Call("ShowActions"); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Action panels pause Water ambience");
                Set("overlay", null); Call("UpdateCaveAmbience", .1d);
                at.GetMethod("Update")!.Invoke(audio, [false]); check(voice.State == SoundState.Paused, "Focus loss pauses Water ambience");
                Call("UpdateCaveAmbience", .1d); at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d);
                check(voice.State == SoundState.Paused, "Sound Off silences Water ambience");
                at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d);
                check(voice.State == SoundState.Paused, "Zero master volume silences Water ambience");
                at.GetProperty("Volume")!.SetValue(audio, .5f); Scene(53); Call("UpdateCaveAmbience", .1d);
                check(voice.IsDisposed && Voice() is { State: SoundState.Playing }, "Leaving Water for Ice replaces the old ambient voice");
                Scene(54); Call("UpdateCaveAmbience", .1d); check(Voice() is { State: SoundState.Playing } && ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("finalAmbience", flags)!.GetValue(audio)), "Entering Final Cave selects its own ambient track");
                check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), "Water audio lifecycle preserves the complete adventure and RNG");
            }
            finally
            {
                at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
                at.GetProperty("Enabled")!.SetValue(audio, enabled); at.GetProperty("Volume")!.SetValue(audio, volume);
            }
        }
        Adventure Scene(int id)
        {
            var layer = id == 117 ? 0 : (id - 1) / 40; var cell = id == 117 ? 18 : id - layer * 40 - 1;
            var map = catalog.CreateMap(id)!; var placements = MonsterPlacements.ForMap(id);
            var at = (from y in Enumerable.Range(1, 10) from x in Enumerable.Range(1, 11)
                let p = new MapPosition(x, y) where map.Tile(p) is 2 or 0x53 or 0x49 or 0x27 or 0x28 or 0x47 or 0x48
                    && !placements.Any(m => m.Contains(p)) && !map.Events.Chests.Contains(p)
                    && !map.Events.Depth.Any(d => d.HasPosition && d.Position == p)
                orderby Math.Abs(x - 6) + Math.Abs(y - 5) select p).FirstOrDefault(new MapPosition(6, 5));
            var game = new Adventure(new GameSession(catalog, location: new() { MapId = id, MapLayer = layer,
                AlternateMapSet = id == 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = at }));
            Set("session", game.Session); Set("adventure", game); Reveal(game); Call("SynchronizeWorld", false); return game;
        }
        static void Reveal(Adventure game) { for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Visibility.RevealFrom(game.Map, new(x, y)); }
        Color[] Light(Adventure game)
        {
            lightType.GetMethod("Prepare")!.Invoke(light, [batch, game, Matrix.Identity, 1d]); host.GraphicsDevice.SetRenderTarget(null);
            var data = new Color[416 * 384]; lightTarget.GetData(data); return data;
        }
        Color[] World(Adventure game, double time) => Render(() => Invoke("Draw", batch, game, player, Get("town")!, time, false, 0f));
        Color[] Render(Action draw)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); draw(); batch.End(); host.GraphicsDevice.SetRenderTarget(null);
            var data = new Color[416 * 384]; target.GetData(data); return data;
        }
        void Save(Color[] data, string name)
        {
            using var texture = new Texture2D(host.GraphicsDevice, 416, 384); texture.SetData(data);
            using var file = File.Create(Path.Combine(directory, name)); texture.SaveAsPng(file, 416, 384);
        }
        void Gameplay(bool lit, string name)
        {
            Set("pixelLighting", lit); Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; host.GraphicsDevice.GetBackBufferData(pixels); presented.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); presented.SaveAsPng(file, 1280, 720);
        }
    }
}
