using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class BreedingGroundChecks
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
        void Invoke(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var terrain = (Texture2D)wt.GetField("breedingGround", flags)!.GetValue(world)!;
        var palette = (Color[])type.Assembly.GetType("Yipe.Desktop.RetroArt")!.GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var soilPalette = (Color[])wt.GetField("BreedingSoilPalette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var audio = Get("audio")!; var at = audio.GetType();
        var enabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!; var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
        SoundEffectInstance? Voice() => (SoundEffectInstance?)at.GetField("ambience", flags)!.GetValue(audio);
        void Ambience(bool eligible, bool playing, double elapsed) => at.GetMethod("UpdateAmbience")!.Invoke(audio, [eligible, playing, elapsed]);
        var directory = Path.Combine(output, "breeding-grounds"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        var lightType = type.Assembly.GetType("Yipe.Desktop.PixelLighting")!;
        using var light = (IDisposable)Activator.CreateInstance(lightType, [host.GraphicsDevice, Get("pixel")!, 416, 384, world])!;
        var lightTarget = (RenderTarget2D)lightType.GetField("lightMap", flags)!.GetValue(light)!;
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var presentedPixels = new Color[1280 * 720];
        var terrains = new Dictionary<int, Color[]>();
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            foreach (var id in new[] { 59, 60, 65, 66, 105, 106 })
            {
                var game = Scene(id); var snapshot = GameSaveCodec.Encode(game.Session, catalog);
                Invoke("PrepareBreeding", game.Map); var original = Ground(); terrains[id] = original;
                var lit = Light(game);
                Save(lit, $"light-{id}.png");
                check(lit.Any(p => p.G > p.R && p.A > 0) && (id == 59 || lit.Any(p => p.R > p.G && p.A > 0)),
                    $"Breeding {id}: authored fungi cast cool light and entrance/holding lamps cast warm light");
                check(original.All(p => p.A == 255 && (palette.Contains(p) || soilPalette.Contains(p))),
                    $"Breeding {id}: terrain and props use opaque pixels from the shared palette and mineral-earth/slate ramp");
                var wall = new List<double>(); var floor = new List<double>();
                for (var y = 0; y < 384; y++) for (var x = 0; x < 416; x++)
                {
                    if (x % 32 is < 9 or > 22 || y % 32 is < 5 or > 15) continue;
                    var p = original[y * 416 + x];
                    (game.Map.Tile(new(x / 32, y / 32)) is 0x23 or 0x44 ? wall : floor).Add(p.R * .2126 + p.G * .7152 + p.B * .0722);
                }
                check(wall.Average() - floor.Average() > 30, $"Breeding {id}: wall tops stay clearly brighter than the walkable earth");
                for (var repeat = 0; repeat < 30; repeat++) Invoke("PrepareBreeding", game.Map);
                check(original.SequenceEqual(Ground()), $"Breeding {id}: cached terrain is stable");
                var animation = Air(game, .2);
                check(animation.SequenceEqual(Air(game, .2)) && !animation.SequenceEqual(Air(game, 1.2))
                    && animation.Count(p => p.A > 0) is > 0 and < 100,
                    $"Breeding {id}: sparse drips, grit and spores freeze with scene time");
                game.Visibility.Initialize(game.Map, new(1, 1));
                var partial = Air(game, .2);
                check(Enumerable.Range(0, partial.Length).All(i => partial[i].A == 0 || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                    $"Breeding {id}: ambient pixels cannot reveal unexplored cells");
                var partialLight = Light(game);
                check(Enumerable.Range(0, partialLight.Length).All(i => partialLight[i].A == 0 || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                    $"Breeding {id}: fungi and lamps cannot light undiscovered cells");
                game.Visibility.Initialize(new Map(id, 7, Enumerable.Repeat((ushort)0x23, Map.TileCount).ToArray()), new(0, 0));
                check(Light(game).All(p => p.A == 0), $"Breeding {id}: undiscovered light sources cast no visible light");
                var seen = new bool[Map.TileCount]; seen[1 * Map.Height + 1] = true;
                var fog = Render(() => Invoke("DrawMap", batch, game.Map, seen, 1d, town));
                check(Enumerable.Range(0, fog.Length).All(i => seen[i % 416 / 32 * Map.Height + i / 416 / 32] || fog[i] == palette[0]),
                    $"Breeding {id}: area-map scenery stays behind the fog mask");
                Reveal(game);
                var secret = new List<(MapPosition Cell, ushort Tile)>();
                for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
                {
                    var cell = new MapPosition(x, y); var tile = game.Map.Tile(cell);
                    if (tile is not (0x44 or 0x48)) continue;
                    secret.Add((cell, tile)); game.Map.RestoreTile(cell, tile == 0x44 ? (ushort)0x23 : (ushort)0x28);
                }
                Invoke("PrepareBreeding", game.Map);
                check(original.SequenceEqual(Ground()) && animation.SequenceEqual(Air(game, .2)) && lit.SequenceEqual(Light(game)),
                    $"Breeding {id}: secret aliases have identical terrain and atmosphere");
                foreach (var entry in secret) game.Map.RestoreTile(entry.Cell, entry.Tile);
                var changedCell = new MapPosition(0, 0); var previous = game.Map.Tile(changedCell);
                game.Map.RestoreTile(changedCell, 0x28); Invoke("PrepareBreeding", game.Map);
                check(!original.SequenceEqual(Ground()), $"Breeding {id}: changing terrain invalidates cached rock");
                game.Map.RestoreTile(changedCell, previous); Invoke("PrepareBreeding", game.Map);
                check(original.SequenceEqual(Ground()), $"Breeding {id}: restoring rock restores its exact appearance");
                Save(World(game, 1), $"map-{id}.png");
                Set("pixelLighting", true); Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("sceneTime", 1d);
                Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(presentedPixels);
                check(presentedPixels.All(p => p.A == 255), $"Breeding {id}: local light and the default presentation remain opaque");
                presented.SetData(presentedPixels);
                using (var file = File.Create(Path.Combine(directory, $"gameplay-{id}.png"))) presented.SaveAsPng(file, 1280, 720);
                Set("pixelLighting", false); Call("Draw", new GameTime()); host.GraphicsDevice.GetBackBufferData(presentedPixels);
                presented.SetData(presentedPixels);
                using (var file = File.Create(Path.Combine(directory, $"gameplay-{id}-unlit.png"))) presented.SaveAsPng(file, 1280, 720);
                Set("pixelLighting", true);
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Breeding {id}: rendering preserves quests, encounters, the complete save and RNG");
            }

            // Authored region overlaps must keep their terrain colors continuous.
            foreach (var (left, right) in new[] { (59, 60), (105, 106) })
            {
                var a = catalog.CreateMap(left)!; var b = catalog.CreateMap(right)!; var count = 0; var differences = 0;
                for (var y = 1; y < 11; y++)
                {
                    // Some original border layouts differ. Compare continuous terrain
                    // only where the full neighboring contour is authored identically.
                    if (!Enumerable.Range(y - 1, 3).All(row => Enumerable.Range(0, 2)
                        .All(x => a.Tile(new(x + 11, row)) == b.Tile(new(x, row))))) continue;
                    for (var x = 0; x < 2; x++)
                    {
                        if (a.Tile(new(x + 11, y)) != b.Tile(new(x, y))) continue;
                        for (var py = 0; py < 32; py++) for (var px = 0; px < 32; px++)
                        {
                            count++;
                            if (terrains[left][(y * 32 + py) * 416 + (x + 11) * 32 + px]
                                == terrains[right][(y * 32 + py) * 416 + x * 32 + px]) continue;
                            if (differences++ < 8) Console.WriteLine($"Overlap {left}/{right} differs at {x * 32 + px},{y * 32 + py}");
                        }
                    }
                }
                check(count > 1000 && differences == 0, $"Breeding {left}/{right}: matching overlap terrain joins without a color seam ({differences}/{count} differ)");
            }
            foreach (var id in new[] { 17, 57, 53, 117, 121, 127, 133 })
            {
                var unrelated = new Adventure(new GameSession(catalog, location: new() { MapId = id, Position = new(1, 1) }));
                check(Air(unrelated, 1).All(p => p.A == 0), $"Breeding effects stay out of unrelated map {id}, including other environment-7 maps");
            }

            // A lamp shining past a chipped corner must be clipped within a tile,
            // with a small penumbra, and still respect solid rock and closed doors.
            var corner = Scene(60);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) corner.Map.RestoreTile(new(x, y), 0x28);
            corner.Map.RestoreTile(new(3, 5), 0x23); Reveal(corner);
            var openLight = Light(corner);
            check(openLight[140 * 416 + 112].R > 0,
                "Breeding lamp: round light reaches above the source without a horizontal cutoff");
            corner.Map.RestoreTile(new(4, 4), 0x23);
            var cornerLight = Light(corner);
            Save(cornerLight, "corner-light.png");
            check(cornerLight[158 * 416 + 130].R > 0 && cornerLight[146 * 416 + 147].R == 0,
                "Breeding corner: light grazes the carved corner but stops inside the same rock tile");
            check(Enumerable.Range(0, cornerLight.Length).Any(i => cornerLight[i].R > 0 && cornerLight[i].R < openLight[i].R),
                "Breeding corner: shadow edges contain a native-pixel partial-light step");
            corner.Map.RestoreTile(new(4, 4), 0x44);
            check(cornerLight.SequenceEqual(Light(corner)), "Breeding corner: a secret rock casts the same contour shadow");
            corner.Map.RestoreTile(new(4, 4), 0x28);
            corner.Map.RestoreTile(new(4, 5), 0x3e);
            check(openLight[175 * 416 + 164].R > 0 && Light(corner)[175 * 416 + 164].R == 0,
                "Breeding door: a closed door still blocks the floor behind it");
            corner.Map.RestoreTile(new(4, 5), 0x28);
            check(openLight.SequenceEqual(Light(corner)), "Breeding door: opening the path invalidates the light mask without stale shadows");
            Invoke("PrepareBreeding", corner.Map); var bareFloor = Ground();
            corner.Map.RestoreTile(new(7, 5), 0x23); Invoke("PrepareBreeding", corner.Map);
            var edgedFloor = Ground();
            check(edgedFloor[160 * 416 + 224] == bareFloor[160 * 416 + 224]
                && edgedFloor[160 * 416 + 255] == bareFloor[160 * 416 + 255],
                "Breeding floor: cut stone corners retain bare earth instead of a square dark border");

            // Biome lighting changes the world sprite's colour and ground contact,
            // not its native outline, hover height, scale or atlas used in battle.
            foreach (var (monster, scale) in new[] { (2, 1), (21, 1), (40, 2) })
            {
                Scene(57); var outside = Actor(); var portrait = Portrait();
                Scene(59); var inside = Actor();
                check(Enumerable.Range(0, inside.Length).All(i => (inside[i].A > 0) == (outside[i].A > 0))
                    && Enumerable.Range(0, inside.Length).Any(i => outside[i].A == 255 && inside[i] != outside[i])
                    && Enumerable.Range(0, inside.Length).Any(i => outside[i].A is > 0 and < 255 && inside[i].A > outside[i].A),
                    $"Cave creature {monster}: ambient colour and stronger contact preserve its outline, scale and hover position");
                check(portrait.SequenceEqual(Portrait()), $"Cave creature {monster}: environmental colour leaves battle artwork unchanged");
                Scene(57);
                check(outside.SequenceEqual(Actor()), $"Cave creature {monster}: leaving restores its exact ordinary appearance");
                Color[] Actor() => Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, null!));
                Color[] Portrait() => Render(() => Invoke("DrawAnimatedMonster", batch, monster, new Rectangle(32, 32, 48, 48), 0, false, null!));
            }

            var room = Scene(60); Invoke("PrepareBreeding", room.Map);
            check(wt.GetMethod("FootstepAt", flags)!.Invoke(world, [room.Map, new MapPosition(7, 8)])!.ToString() == "Sand",
                "Breeding packed earth uses recorded granular footsteps");
            var beforeAudio = GameSaveCodec.Encode(room.Session, catalog);
            check((string)at.GetProperty("Error")!.GetValue(audio)! == "", "The licensed cave loop loads on the real audio device");
            var sound = (SoundEffect)at.GetField("caveAmbience", flags)!.GetValue(audio)!;
            check(sound.Duration.TotalSeconds is > 90 and < 96, "The complete supplied loop is retained without a short repeated excerpt");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
            Ambience(false, false, 0); Call("UpdateCaveAmbience", .1d); var voice = Voice()!; var quiet = voice.Volume;
            Call("UpdateCaveAmbience", .7d);
            check(voice.State == SoundState.Playing && voice.IsLooped && voice.Volume > quiet && voice.Volume <= .1f,
                "Cave ambience fades in below effects volume and uses one looping voice");
            Set("paused", true); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "The production pause gate pauses cave ambience"); Set("paused", false);
            Call("UpdateCaveAmbience", .1d);
            check(ReferenceEquals(voice, Voice()) && voice.State == SoundState.Playing, "Resume reuses the loop instead of restarting or stacking voices");
            Set("mapOpen", true); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "The area-map gate pauses cave ambience"); Set("mapOpen", false);
            Call("ShowActions"); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "Action/combat overlays silence ambience through the production gate"); Set("overlay", null);
            Call("UpdateCaveAmbience", .1d); at.GetMethod("Update")!.Invoke(audio, [false]);
            check(voice.State == SoundState.Paused, "Focus loss pauses the ambient voice immediately");
            at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "Sound Off suppresses cave ambience");
            at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d);
            check(voice.State == SoundState.Paused, "Master volume zero suppresses cave ambience");
            at.GetProperty("Volume")!.SetValue(audio, .5f); Call("UpdateCaveAmbience", .1d);
            Scene(54); Call("UpdateCaveAmbience", .1d);
            check(Voice() is { State: SoundState.Playing } && voice.IsDisposed, "Entering Final Cave replaces the breeding loop");
            check(beforeAudio.SequenceEqual(GameSaveCodec.Encode(room.Session, catalog)), "Ambient playback and UI gates preserve the complete session and RNG");
            room = Scene(60);
            for (var frame = 0; frame < 48; frame++) Save(World(room, frame / 8d), $"frame-{frame:D2}.png");
        }
        finally
        {
            Ambience(false, false, 0); at.GetProperty("Enabled")!.SetValue(audio, enabled); at.GetProperty("Volume")!.SetValue(audio, volume);
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        Adventure Scene(int id)
        {
            var layer = id >= 117 ? 3 : (id - 1) / 40; var cell = id - (layer == 3 ? 98 : layer * 40) - 1;
            var scene = new Adventure(new GameSession(catalog, location: new() { MapId = id, MapLayer = layer,
                AlternateMapSet = id >= 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6,
                Position = id == 60 ? new(4, 10) : new(8, 8) }));
            Set("session", scene.Session); Set("adventure", scene); Reveal(scene); Call("SynchronizeWorld", false); return scene;
        }
        static void Reveal(Adventure scene) { for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) scene.Visibility.RevealFrom(scene.Map, new(x, y)); }
        Color[] Ground() { var pixels = new Color[416 * 384]; terrain.GetData(pixels); return pixels; }
        Color[] Light(Adventure scene)
        {
            lightType.GetMethod("Prepare")!.Invoke(light, [batch, scene, Matrix.Identity, 1d]);
            host.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; lightTarget.GetData(pixels); return pixels;
        }
        Color[] Render(Action draw)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); draw(); batch.End(); host.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        Color[] World(Adventure scene, double time) => Render(() => Invoke("Draw", batch, scene, player, town, time, false, 0f));
        Color[] Air(Adventure scene, double time) => Render(() => Invoke("DrawBreedingAtmosphere", batch, scene, time));
        void Save(Color[] pixels, string name)
        {
            target.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); target.SaveAsPng(file, 416, 384);
        }
    }
}
