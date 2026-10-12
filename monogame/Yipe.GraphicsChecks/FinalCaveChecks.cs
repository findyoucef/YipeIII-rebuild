using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class FinalCaveChecks
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
        var directory = Path.Combine(output, "final-cave"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var lightType = type.Assembly.GetType("Yipe.Desktop.PixelLighting")!;
        using var light = (IDisposable)Activator.CreateInstance(lightType, [host.GraphicsDevice, Get("pixel")!, 416, 384, world])!;
        var lightTarget = (RenderTarget2D)lightType.GetField("lightMap", flags)!.GetValue(light)!;
        int[] rooms = [43, 48, 49, 54, 55, 61, 83, 84, 85, 86, 89, 91, 92, 97, 98];
        var terrains = new Dictionary<int, Color[]>();
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            foreach (var id in rooms.Concat(new[] { 17, 51, 53, 57, 59, 62, 67, 117 }))
            {
                var game = Scene(id); var snapshot = GameSaveCodec.Encode(game.Session, catalog);
                Save(World(game, 1), $"map-{id}.png"); Save(Light(game), $"light-{id}.png");
                Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("sceneTime", 1d);
                Gameplay(true, $"gameplay-{id}.png"); Gameplay(false, $"gameplay-{id}-unlit.png");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Final review {id}: rendering preserves full gameplay and RNG");
                if (rooms.Contains(id)) CheckRoom(game);
            }
            CheckBoundaries(); CheckAudio();
            var moving = Scene(54); Set("miniatureMode", 2); Set("scanlines", true);
            var frozen = GameSaveCodec.Encode(moving.Session, catalog);
            for (var frame = 0; frame < 48; frame++) { Set("sceneTime", frame / 8d); Gameplay(true, $"motion-{frame:D2}.png"); }
            check(frozen.SequenceEqual(GameSaveCodec.Encode(moving.Session, catalog)), "Final atmosphere captures preserve the complete adventure and RNG");
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
            var image = World(game, 1); var originalLight = Light(game);
            check(image.SequenceEqual(World(game, 1)), $"Final {id}: frozen presentation is deterministic");
            var texture = (Texture2D)wt.GetField("finalGround", flags)!.GetValue(world)!;
            Color[] Ground() { Invoke("PrepareFinal", game.Map); var pixels = new Color[416 * 384]; texture.GetData(pixels); return pixels; }
            var ground = Ground(); terrains[id] = ground;
            var palette = (Color[])wt.GetField("FinalPalette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var rock = (bool[])Invoke("FinalRock", game.Map)!;
            check(ground.All(c => c.A == 255 && palette.Contains(c)), $"Final {id}: terrain uses only its native rock/crimson palette");
            check(ReferenceEquals(rock, Invoke("FinalRock", game.Map)), $"Final {id}: unchanged terrain reuses the cached contour");
            var walls = new List<float>(); var floor = new List<float>();
            for (var y = 0; y < 384; y++) for (var x = 0; x < 416; x++)
            {
                if (x % 32 is < 8 or > 23 || y % 32 is < 6 or > 18) continue;
                var c = ground[y * 416 + x]; (rock[y * 416 + x] ? walls : floor).Add(c.R * .2126f + c.G * .7152f + c.B * .0722f);
            }
            check(walls.Average() > floor.Average() * 1.35f, $"Final {id}: broad quiet floor slabs sit below detailed raised walls");
            check(originalLight.Any(c => c.R > c.G * 3 && c.R > c.B * 2), $"Final {id}: embedded crimson crystals supply the local light");
            var sources = (Point[])Invoke("FinalLights", game.Map)!;
            check(sources.Length > 0 && sources.All(p => rock[p.Y * 416 + p.X] && ground[p.Y * 416 + p.X].R > ground[p.Y * 416 + p.X].G * 2),
                $"Final {id}: every light has a visible crystal embedded at its source");
            var originals = new List<(MapPosition Cell, ushort Tile)>();
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
            {
                var p = new MapPosition(x, y); var tile = game.Map.Tile(p); originals.Add((p, tile));
                if (tile is 0x22 or 0x43) game.Map.RestoreTile(p, tile == 0x22 ? (ushort)0x43 : (ushort)0x22);
                else if (tile is 0x28 or 0x48) game.Map.RestoreTile(p, tile == 0x28 ? (ushort)0x48 : (ushort)0x28);
            }
            check(image.SequenceEqual(World(game, 1)) && originalLight.SequenceEqual(Light(game)),
                $"Final {id}: secret wall/floor aliases have identical art, atmosphere and contour lighting");
            foreach (var (cell, tile) in originals) game.Map.RestoreTile(cell, tile);
            var changedCell = originals.First(t => t.Tile == 0x22).Cell;
            game.Map.RestoreTile(changedCell, 0x28);
            check(!ground.SequenceEqual(Ground()) && !ReferenceEquals(rock, Invoke("FinalRock", game.Map)),
                $"Final {id}: opening rock invalidates terrain and silhouette together");
            game.Map.RestoreTile(changedCell, 0x22);
            check(ground.SequenceEqual(Ground()) && originalLight.SequenceEqual(Light(game)), $"Final {id}: restoring terrain restores its exact art and lighting");
            check(Invoke("FootstepAt", game.Map, game.Location.Position)!.ToString() == "Stone", $"Final {id}: worn rock retains stone footsteps");
            var air = Air(game, 1);
            check(air.Count(c => c.A > 0) is > 0 and < 250 && air.SequenceEqual(Air(game, 1)) && !air.SequenceEqual(Air(game, 2)),
                $"Final {id}: sparse pixel ash/grit/haze advances only with presentation time");
            game.Visibility.Initialize(game.Map, new(1, 1)); var partial = Air(game, 1);
            check(Enumerable.Range(0, partial.Length).All(i => partial[i].A == 0 || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                $"Final {id}: atmosphere never reveals undiscovered terrain");
            Reveal(game);
            check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Final {id}: routes, secrets, encounters, rescues and full saved state/RNG remain intact");
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
                        var material = Normalize(a.Tile(new(x + dx, y + dy)));
                        if (material is not (0x22 or 0x28) || material != Normalize(b.Tile(new(x, y)))) continue;
                        // Preserve authored overlap differences; compare material where neighborhoods match.
                        var matching = true;
                        for (var sy = -1; sy <= 1; sy++) for (var sx = -1; sx <= 1; sx++)
                        {
                            var ap = new MapPosition(x + dx + sx, y + dy + sy); var bp = new MapPosition(x + sx, y + sy);
                            if (Normalize(NeighborTile(a, ap)) != Normalize(NeighborTile(b, bp))) matching = false;
                        }
                        if (!matching) continue;
                        for (var py = 12; py < 24; py++) for (var px = 8; px < 24; px++)
                        {
                            count++;
                            if (terrains[id][((y + dy) * 32 + py) * 416 + (x + dx) * 32 + px] != terrains[next][(y * 32 + py) * 416 + x * 32 + px]) different++;
                        }
                    }
                    if (count > 0) check(different == 0, $"Final {id}/{next}: matching overlaps retain identical rock, crystals and floor ({different}/{count})");
                }
            static ushort Normalize(ushort tile) => tile switch { 0x48 => 0x28, 0x43 => 0x22, _ => tile };
            ushort NeighborTile(Map map, MapPosition p)
            {
                if (Map.Contains(p)) return map.Tile(p);
                var dx = p.X < 0 ? -1 : p.X >= 13 ? 1 : 0;
                var dy = p.Y < 0 ? -1 : p.Y >= 12 ? 1 : 0;
                var layer = (map.Id - 1) / 40; var region = (map.Id - 1) % 40;
                var id = layer * 40 + (region / 6 + dy + 6) % 6 * 6 + (region % 6 + dx + 6) % 6 + 1;
                // Bottom wall faces also depend on the following room. Some
                // authored overlaps meet different caves; they are not identical neighborhoods.
                return rooms.Contains(id) ? catalog.CreateMap(id)!.Tile(new(p.X - dx * 11, p.Y - dy * 10))
                    : map.Tile(new(Math.Clamp(p.X, 0, 12), Math.Clamp(p.Y, 0, 11)));
            }
            var room = Scene(54);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) room.Map.RestoreTile(new(x, y), 0x28);
            for (var x = 2; x < 10; x++) room.Map.RestoreTile(new(x, 2), 0x22);
            var lamp = ((Point[])Invoke("FinalLights", room.Map)!).First(); var cell = new MapPosition(lamp.X / 32, lamp.Y / 32);
            for (var x = 2; x < 10; x++) if (x != cell.X) room.Map.RestoreTile(new(x, 2), 0x28);
            Reveal(room); var clear = Light(room); room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x3e); var blocked = Light(room);
            check(clear[(lamp.Y + 43) * 416 + lamp.X].R > 0 && blocked[(lamp.Y + 43) * 416 + lamp.X].R == 0,
                "Final crystal light stops at a closed door");
            room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x28);
            check(clear.SequenceEqual(Light(room)), "Opening a Final Cave door restores the exact contour light");
            for (var y = cell.Y + 1; y < 12; y++) room.Map.RestoreTile(new(cell.X + 1, y), 0x22);
            var corner = Light(room); Save(corner, "corner-mask.png");
            var values = (from y in Enumerable.Range((cell.Y + 1) * 32, 32) from x in Enumerable.Range((cell.X + 1) * 32, 32)
                select corner[y * 416 + x].R).ToArray();
            check(values.Any(c => c > 0) && values.Any(c => c == 0), "Final light follows carved rock corners without a square cutoff");
            room.Visibility.Initialize(new Map(54, 6, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(0, 0));
            check(Light(room).All(c => c.R == 0), "Undiscovered Final Cave crystals cannot reveal hidden terrain");
            room = Scene(54); var p = new MapPosition(6, 5);
            Color[] Tile(ushort tile)
            {
                room.Map.RestoreTile(p, tile); Invoke("PrepareFinal", room.Map);
                return Render(() => Invoke("DrawGround", batch, room.Map, p, 1d));
            }
            var stable = Tile(0x28); var cracked = Tile(0x29); var crumbling = Tile(0x2a); var pit = Tile(0x2b);
            check(!stable.SequenceEqual(cracked) && cracked.SequenceEqual(crumbling) && !cracked.SequenceEqual(pit),
                "Final unstable floors and opened holes retain their original visible warning stages");
            check(stable.SequenceEqual(Tile(0x52)), "Untriggered linked floor remains visually hidden");
            foreach (var (monster, scale) in new[] { (18, 1), (19, 1), (20, 2) })
            {
                Scene(17); var outside = Actor(); var portrait = Portrait(); Scene(54); var inside = Actor();
                check(Enumerable.Range(0, inside.Length).All(i => (inside[i].A > 0) == (outside[i].A > 0))
                    && Enumerable.Range(0, inside.Length).Any(i => inside[i].A == 255 && inside[i] != outside[i]),
                    $"Final creature {monster}: ambient tint/contact shadow preserve silhouette, hover and scale");
                check(portrait.SequenceEqual(Portrait()), $"Final creature {monster}: battle artwork is unchanged");
                Scene(17); check(outside.SequenceEqual(Actor()), $"Final creature {monster}: its outdoor appearance returns on leaving");
                Color[] Actor() => Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, null!));
                Color[] Portrait() => Render(() => Invoke("DrawAnimatedMonster", batch, monster, new Rectangle(32, 32, 48, 48), 0, false, null!));
            }
            room = Scene(62);
            check(Air(room, 1).All(c => c.A == 0), "Final Cave atmosphere stays out of Water Cave");
        }
        void CheckAudio()
        {
            var audio = Get("audio")!; var at = audio.GetType();
            var enabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!; var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
            SoundEffectInstance? Voice() => at.GetField("ambience", flags)!.GetValue(audio) as SoundEffectInstance;
            try
            {
                var game = Scene(54); var before = GameSaveCodec.Encode(game.Session, catalog);
                at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
                at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]); Call("UpdateCaveAmbience", 1d); var voice = Voice()!;
                check(voice.State == SoundState.Playing && ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("finalAmbience", flags)!.GetValue(audio)),
                    "Final Cave selects its dedicated recorded ambience on the real audio device");
                check(voice.Volume is > 0 and <= .095f && voice.IsLooped, "Final audio stays quiet, looped and under master volume");
                Scene(98); Call("UpdateCaveAmbience", .1d);
                check(ReferenceEquals(voice, Voice()), "Upper/deep Final Cave transitions retain the same continuous loop");
                foreach (var field in new[] { "paused", "mapOpen", "worldMapOpen" })
                {
                    Set(field, true); if (field == "worldMapOpen") Set("mapOpen", true);
                    Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, $"Final ambience pauses for {field}");
                    Set(field, false); Set("mapOpen", false); Call("UpdateCaveAmbience", .1d);
                    check(voice.State == SoundState.Playing && ReferenceEquals(voice, Voice()), "Final ambience resumes the same voice");
                }
                Call("ShowActions"); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Action panels pause Final ambience");
                Set("overlay", null); Call("UpdateCaveAmbience", .1d);
                at.GetMethod("Update")!.Invoke(audio, [false]); check(voice.State == SoundState.Paused, "Focus loss pauses Final ambience");
                Call("UpdateCaveAmbience", .1d); at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d);
                check(voice.State == SoundState.Paused, "Sound Off silences Final ambience");
                at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d);
                check(voice.State == SoundState.Paused, "Zero master volume silences Final ambience");
                at.GetProperty("Volume")!.SetValue(audio, .5f); Scene(62); Call("UpdateCaveAmbience", .1d);
                check(voice.IsDisposed && Voice() is { State: SoundState.Playing }, "Leaving Final for Water replaces the old ambient voice");
                var water = Voice()!; at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
                check(water.IsDisposed && Voice() is null, "No ambient track releases the active voice");
                check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), "Final audio lifecycle preserves the complete adventure and RNG");
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
        Color[] Air(Adventure game, double time) => Render(() => Invoke("DrawFinalAtmosphere", batch, game, time));
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
