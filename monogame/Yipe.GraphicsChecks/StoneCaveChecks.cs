using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class StoneCaveChecks
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
        var world = Get("world")!; var wt = world.GetType();
        object? Invoke(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var player = (PlayerMotion)Get("player")!; var position = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!;
        var directory = Path.Combine(output, "stone-cave"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        var lightType = type.Assembly.GetType("Yipe.Desktop.PixelLighting")!;
        using var light = (IDisposable)Activator.CreateInstance(lightType, [host.GraphicsDevice, Get("pixel")!, 416, 384, world])!;
        var lightTarget = (RenderTarget2D)lightType.GetField("lightMap", flags)!.GetValue(light)!;
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var pixels = new Color[1280 * 720];
        var terrain = (Texture2D)wt.GetField("stoneGround", flags)!.GetValue(world)!;
        var terrains = new Dictionary<int, Color[]>();
        var audio = Get("audio")!; var at = audio.GetType();
        var enabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!; var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
        SoundEffectInstance? Voice() => (SoundEffectInstance?)at.GetField("ambience", flags)!.GetValue(audio);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            foreach (var id in new[] { 51, 52, 58, 64, 70, 76, 104, 110, 116 })
            {
                var game = Scene(id); var snapshot = GameSaveCodec.Encode(game.Session, catalog);
                var originalTiles = (from x in Enumerable.Range(0, 13) from y in Enumerable.Range(0, 12)
                    select game.Map.Tile(new(x, y))).ToArray();
                Invoke("PrepareStone", game.Map); var ground = new Color[416 * 384]; terrain.GetData(ground); terrains[id] = ground;
                var palette = (Color[])wt.GetField("StonePalette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                check(ground.All(p => p.A == 255 && palette.Contains(p)), $"Stone {id}: native terrain uses only the restrained stone palette");
                var wall = new List<double>(); var floor = new List<double>();
                for (var y = 0; y < 384; y++) for (var x = 0; x < 416; x++)
                {
                    if (x % 32 is < 8 or > 23 || y % 32 is < 6 or > 18) continue;
                    var p = ground[y * 416 + x];
                    (game.Map.Tile(new(x / 32, y / 32)) is 0x23 or 0x44 ? wall : floor).Add(p.R * .2126 + p.G * .7152 + p.B * .0722);
                }
                var contrast = wall.Average() - floor.Average();
                Console.WriteLine($"Stone {id}: wall/floor contrast {contrast:F1}/255");
                check(contrast >= 30, $"Stone {id}: walls stay clearly raised above the quieter floor");
                var water = ((bool[])wt.GetField("waterMask", flags)!.GetValue(world)!).ToArray();
                check(id == 51 ? water.Count(v => v) > 12000 : water.All(v => !v), $"Stone {id}: original flooded/dry topology is retained");
                var native = World(game, 1); Save(native, $"map-{id}.png");
                check(native.SequenceEqual(World(game, 1)) && !native.SequenceEqual(World(game, 1.75)), $"Stone {id}: scene time freezes and advances the native atmosphere");
                var air = Air(game, 1);
                check(air.Count(p => p.A > 0) is > 0 and < 1200, $"Stone {id}: atmospheric movement stays sparse");
                game.Visibility.Initialize(game.Map, new(1, 1));
                var partial = Air(game, 1);
                check(Enumerable.Range(0, partial.Length).All(i => partial[i].A == 0 || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                    $"Stone {id}: dust and drips never reveal unexplored cells");
                Reveal(game);
                var secret = new List<(MapPosition Cell, ushort Tile)>();
                for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
                {
                    var tile = game.Map.Tile(new(x, y));
                    if (tile is not (0x44 or 0x47)) continue;
                    secret.Add((new(x, y), tile)); game.Map.RestoreTile(new(x, y), tile == 0x44 ? (ushort)0x23 : (ushort)0x27);
                }
                Invoke("PrepareStone", game.Map); var normal = new Color[ground.Length]; terrain.GetData(normal);
                check(ground.SequenceEqual(normal) && air.SequenceEqual(Air(game, 1)), $"Stone {id}: secrets have identical terrain and atmosphere");
                foreach (var entry in secret) game.Map.RestoreTile(entry.Cell, entry.Tile);
                var changedCell = new MapPosition(6, 6); var oldTile = game.Map.Tile(changedCell);
                game.Map.RestoreTile(changedCell, oldTile is 0x23 or 0x44 ? (ushort)0x27 : (ushort)0x23);
                Invoke("PrepareStone", game.Map); terrain.GetData(normal);
                check(!ground.SequenceEqual(normal), $"Stone {id}: changed terrain invalidates the cache");
                game.Map.RestoreTile(changedCell, oldTile); Invoke("PrepareStone", game.Map); terrain.GetData(normal);
                check(ground.SequenceEqual(normal) && water.SequenceEqual((bool[])wt.GetField("waterMask", flags)!.GetValue(world)!),
                    $"Stone {id}: restoring terrain restores the exact ground and water masks");
                var lamps = (Point[])Invoke("StoneLamps", game.Map)!;
                var lit = Light(game); Save(lit, $"light-{id}.png");
                check(lamps.Length > 0 && lit.Any(p => p.R > 0), $"Stone {id}: visible cave sources cast local light");
                check(lightType.GetField("caveMask", flags)!.GetValue(light) is Texture2D, $"Stone {id}: lights use the native contour mask");
                var materials = new HashSet<string>();
                for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
                    if (game.Map.Tile(new(x, y)) is 0x27 or 0x47 or 0x0c or 0x0d) materials.Add(Invoke("FootstepAt", game.Map, new MapPosition(x, y))!.ToString()!);
                check(materials.SetEquals(id == 51 ? ["Stone", "Water"] : ["Stone"]), $"Stone {id}: dry bedrock uses stone steps and flooded ground uses water");
                Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("sceneTime", 1d); Set("pixelLighting", true);
                Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(pixels); presented.SetData(pixels);
                using (var file = File.Create(Path.Combine(directory, $"gameplay-{id}.png"))) presented.SaveAsPng(file, 1280, 720);
                check(pixels.All(p => p.A == 255), $"Stone {id}: the default presentation stays opaque");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Stone {id}: lighting and presentation preserve gameplay and RNG");
                check(originalTiles.SequenceEqual(from x in Enumerable.Range(0, 13) from y in Enumerable.Range(0, 12) select game.Map.Tile(new(x, y))),
                    $"Stone {id}: doors, secrets, water and the original tile layout remain unchanged");
            }
            // Compare only matching original walkable overlap interiors.
            foreach (var (upper, lower) in new[] { (52, 58), (58, 64), (64, 70), (70, 76), (104, 110), (110, 116) })
            {
                var a = catalog.CreateMap(upper)!; var b = catalog.CreateMap(lower)!; var count = 0; var differences = 0;
                for (var x = 0; x < 13; x++) for (var y = 0; y < 2; y++)
                {
                    if (a.Tile(new(x, y + 10)) is not (0x27 or 0x47) || b.Tile(new(x, y)) is not (0x27 or 0x47)) continue;
                    for (var py = 12; py < 24; py++) for (var px = 8; px < 24; px++)
                    {
                        count++;
                        if (terrains[upper][((y + 10) * 32 + py) * 416 + x * 32 + px]
                            != terrains[lower][(y * 32 + py) * 416 + x * 32 + px]) differences++;
                    }
                }
                check(count > 0 && differences == 0, $"Stone {upper}/{lower}: shared walkable overlap stays aligned ({differences}/{count})");
            }
            var west = catalog.CreateMap(51)!; var east = catalog.CreateMap(52)!;
            var overlapPixels = 0; var overlapDifferences = 0;
            for (var y = 0; y < 12; y++) for (var x = 0; x < 2; x++)
            {
                if (west.Tile(new(x + 11, y)) != 0x27 || east.Tile(new(x, y)) != 0x27) continue;
                for (var py = 12; py < 24; py++) for (var px = 8; px < 24; px++)
                {
                    overlapPixels++;
                    if (terrains[51][(y * 32 + py) * 416 + (x + 11) * 32 + px]
                        != terrains[52][(y * 32 + py) * 416 + x * 32 + px]) overlapDifferences++;
                }
            }
            check(overlapPixels > 0 && overlapDifferences == 0, $"Stone 51/52: flooded-chamber exit has aligned dry floor ({overlapDifferences}/{overlapPixels})");
            // Use production source selection on a controlled wall shelf.
            var room = Scene(58);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) room.Map.RestoreTile(new(x, y), 0x27);
            for (var x = 2; x <= 9; x++) room.Map.RestoreTile(new(x, 2), 0x23);
            var lamp = ((Point[])Invoke("StoneLamps", room.Map)!).First();
            var cell = new MapPosition(lamp.X / 32, lamp.Y / 32);
            for (var x = 2; x <= 9; x++)
                if (x != cell.X) room.Map.RestoreTile(new(x, 2), 0x27);
            Reveal(room); var clear = Light(room);
            room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x3e); var blocked = Light(room);
            check(clear[(lamp.Y + 48) * 416 + lamp.X].R > 0 && blocked[(lamp.Y + 48) * 416 + lamp.X].R == 0,
                "Stone contour light stops behind a closed door");
            room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x27);
            check(clear.SequenceEqual(Light(room)), "Opening the Stone door restores the cached light exactly");
            room.Map.RestoreTile(new(cell.X + 1, cell.Y + 1), 0x23); var corner = Light(room);
            Save(corner, "corner-mask.png");
            var cornerPixels = (from y in Enumerable.Range((cell.Y + 1) * 32, 32)
                from x in Enumerable.Range((cell.X + 1) * 32, 32) select corner[y * 416 + x].R).ToArray();
            check(cornerPixels.Any(v => v > 0) && cornerPixels.Any(v => v == 0), "Stone corner occlusion divides actual rock contours inside a tile");
            room.Map.RestoreTile(new(cell.X + 1, cell.Y + 1), 0x44);
            check(corner.SequenceEqual(Light(room)), "Stone secret rock has exactly the ordinary contour shadow");
            var stone = Invoke("StoneRock", room.Map); Invoke("PrepareStone", room.Map);
            check(ReferenceEquals(stone, Invoke("StoneRock", room.Map)), "Stone's unchanged stone mask remains cached");
            room.Visibility.Initialize(new Map(58, 2, Enumerable.Repeat((ushort)0x23, Map.TileCount).ToArray()), new(0, 0));
            check(Light(room).All(p => p.R == 0 && p.G == 0 && p.B == 0), "Stone lighting cannot expose an undiscovered source");

            foreach (var (monster, scale) in new[] { (10, 1), (35, 1), (11, 2) })
            {
                Scene(54); var outside = Actor(); var portrait = Portrait(); Scene(58); var inside = Actor();
                check(Enumerable.Range(0, inside.Length).All(i => (inside[i].A > 0) == (outside[i].A > 0))
                    && Enumerable.Range(0, inside.Length).Any(i => outside[i].A == 255 && inside[i] != outside[i]),
                    $"Stone creature {monster}: mineral tint and contact shading preserves silhouette and scale");
                check(portrait.SequenceEqual(Portrait()), $"Stone creature {monster}: battle artwork stays unchanged");
                Scene(54); check(outside.SequenceEqual(Actor()), $"Stone creature {monster}: leaving restores its original appearance");
                Color[] Actor() => Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, null!));
                Color[] Portrait() => Render(() => Invoke("DrawAnimatedMonster", batch, monster, new Rectangle(32, 32, 48, 48), 0, false, null!));
            }
            room = Scene(58); var beforeAudio = GameSaveCodec.Encode(room.Session, catalog);
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
            at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]); Call("UpdateCaveAmbience", .1d); var voice = Voice()!;
            check(voice is not null && voice.State == SoundState.Playing && voice.IsLooped && voice.Volume < .075f,
                "Stone ambience starts with a quiet fade and a single looping voice");
            var firstVolume = voice!.Volume; Call("UpdateCaveAmbience", .8d);
            check(voice.Volume > firstVolume && Math.Abs(voice.Volume - .075f) < .001f, "Stone ambience fades to its restrained master-volume level");
            Scene(64); Call("UpdateCaveAmbience", .1d);
            check(ReferenceEquals(voice, Voice()), "Stone upper-room transitions retain the same looping voice");
            Set("paused", true); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Pause silences Stone ambience");
            Set("paused", false); Call("UpdateCaveAmbience", .1d); check(ReferenceEquals(voice, Voice()) && voice.State == SoundState.Playing, "Resume continues the Stone loop");
            Set("mapOpen", true); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Map view silences Stone ambience"); Set("mapOpen", false);
            Call("ShowActions"); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Action/combat overlays silence Stone ambience"); Set("overlay", null);
            Call("UpdateCaveAmbience", .1d); at.GetMethod("Update")!.Invoke(audio, [false]); check(voice.State == SoundState.Paused, "Focus loss pauses Stone ambience");
            at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Sound Off mutes Stone ambience");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Zero master volume mutes Stone ambience");
            at.GetProperty("Volume")!.SetValue(audio, .5f); Scene(51); Call("UpdateCaveAmbience", .8d); var deep = Voice()!;
            check(voice.IsDisposed && deep.State == SoundState.Playing && ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("deepEarthAmbience", flags)!.GetValue(audio)),
                "Entering the flooded Stone chamber switches to the existing wetter recording");
            Scene(57); Call("UpdateCaveAmbience", .1d); var earth = Voice()!;
            check(deep.IsDisposed && earth.State == SoundState.Playing, "Stone to Earth selects the Earth ambience");
            Scene(72); Call("UpdateCaveAmbience", .1d); var fire = Voice()!;
            check(earth.IsDisposed && fire.State == SoundState.Playing, "Earth to Fire replaces rather than stacks ambience");
            Scene(59); Call("UpdateCaveAmbience", .1d); var breeding = Voice()!;
            check(fire.IsDisposed && breeding.State == SoundState.Playing, "Fire to breeding grounds preserves the original ambience route");
            Scene(58); Call("UpdateCaveAmbience", .8d); var returning = Voice()!;
            check(ReferenceEquals(breeding, returning) && returning.State == SoundState.Playing && Math.Abs(returning.Volume - .075f) < .001f,
                "Stone reuses the licensed air recording without stacking voices and applies its quieter level");
            Scene(54); Call("UpdateCaveAmbience", .1d); check(returning.IsDisposed && Voice() is { State: SoundState.Playing }, "Entering Final Cave replaces the old ambient voice");
            check(beforeAudio.SequenceEqual(GameSaveCodec.Encode(room.Session, catalog)), "Stone audio never changes gameplay or RNG");
            room = Scene(58);
            for (var frame = 0; frame < 40; frame++)
            {
                Set("sceneTime", frame / 8d); Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(pixels); presented.SetData(pixels);
                using var file = File.Create(Path.Combine(directory, $"play-{frame:D2}.png")); presented.SaveAsPng(file, 1280, 720);
            }
        }
        finally
        {
            at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
            at.GetProperty("Enabled")!.SetValue(audio, enabled); at.GetProperty("Volume")!.SetValue(audio, volume);
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        Adventure Scene(int id)
        {
            var layer = (id - 1) / 40; var cell = id - layer * 40 - 1;
            var map = catalog.CreateMap(id)!;
            var placements = MonsterPlacements.ForMap(id);
            var at = (from y in Enumerable.Range(1, 10) from x in Enumerable.Range(1, 11)
                let p = new MapPosition(x, y) where map.Tile(p) is 0x27 or 0x28 or 0x29 or 0x2a or 0x47 or 0x48
                    && !placements.Any(m => m.Contains(p)) && !map.Events.Chests.Contains(p)
                orderby Math.Abs(x - 6) + Math.Abs(y - 5) select p).FirstOrDefault(new MapPosition(6, 5));
            var scene = new Adventure(new GameSession(catalog, location: new() { MapId = id, MapLayer = layer,
                RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = at }));
            Set("session", scene.Session); Set("adventure", scene); Reveal(scene); Call("SynchronizeWorld", false); return scene;
        }
        static void Reveal(Adventure scene) { for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) scene.Visibility.RevealFrom(scene.Map, new(x, y)); }
        Color[] Light(Adventure scene)
        {
            lightType.GetMethod("Prepare")!.Invoke(light, [batch, scene, Matrix.Identity, 1d]); host.GraphicsDevice.SetRenderTarget(null);
            var data = new Color[416 * 384]; lightTarget.GetData(data); return data;
        }
        Color[] World(Adventure scene, double time) => Render(() => Invoke("Draw", batch, scene, player, Get("town")!, time, false, 0f));
        Color[] Air(Adventure scene, double time) => Render(() => Invoke("DrawStoneAtmosphere", batch, scene, time));
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
    }
}
