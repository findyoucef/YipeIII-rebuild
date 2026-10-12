using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class IceCaveChecks
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
        var directory = Path.Combine(output, "ice-cave"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        var lightType = type.Assembly.GetType("Yipe.Desktop.PixelLighting")!;
        using var light = (IDisposable)Activator.CreateInstance(lightType, [host.GraphicsDevice, Get("pixel")!, 416, 384, world])!;
        var lightTarget = (RenderTarget2D)lightType.GetField("lightMap", flags)!.GetValue(light)!;
        int[] rooms = [41, 42, 47, 53, 81, 82, 87, 88, 93, 94, 99];
        var terrains = new Dictionary<int, Color[]>();
        var terrain = (Texture2D)wt.GetField("iceGround", flags)!.GetValue(world)!;
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            foreach (var id in rooms.Concat(new[] { 17, 51, 57, 59, 67, 117 }))
            {
                var game = Scene(id); var snapshot = GameSaveCodec.Encode(game.Session, catalog);
                var native = World(game, 1); Save(native, $"map-{id}.png");
                check(native.SequenceEqual(World(game, 1)), $"Ice review {id}: a frozen scene renders deterministically");
                Save(Light(game), $"light-{id}.png");
                Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("sceneTime", 1d);
                Gameplay(true, $"gameplay-{id}.png"); Gameplay(false, $"gameplay-{id}-unlit.png");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Ice review {id}: captures preserve full gameplay and RNG");
                if (rooms.Contains(id)) CheckRoom(game);
            }
            // Only compare authored matching overlap interiors. These coordinates account
            // for the two repeated tiles between neighboring regions.
            foreach (var id in rooms)
                foreach (var (next, dx, dy) in new[] { (id + 1, 11, 0), (id + 6, 0, 10) })
                {
                    if (!rooms.Contains(next) || id / 40 != next / 40) continue;
                    var a = catalog.CreateMap(id)!; var b = catalog.CreateMap(next)!;
                    var count = 0; var differences = 0;
                    for (var y = 0; y < (dy == 0 ? 12 : 2); y++) for (var x = 0; x < (dx == 0 ? 13 : 2); x++)
                    {
                        if (a.Tile(new(x + dx, y + dy)) is not (0x53 or 0x49) || b.Tile(new(x, y)) is not (0x53 or 0x49)) continue;
                        for (var py = 12; py < 24; py++) for (var px = 8; px < 24; px++)
                        {
                            count++;
                            if (terrains[id][((y + dy) * 32 + py) * 416 + (x + dx) * 32 + px]
                                != terrains[next][(y * 32 + py) * 416 + x * 32 + px]) differences++;
                        }
                    }
                    if (count > 0) check(differences == 0, $"Ice {id}/{next}: matching walkable region overlaps align ({differences}/{count})");
                }
            var room = Scene(53);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) room.Map.RestoreTile(new(x, y), 0x53);
            for (var x = 2; x <= 9; x++) room.Map.RestoreTile(new(x, 2), 0x22);
            var lamp = ((Point[])Invoke("IceLights", room.Map)!).First();
            var cell = new MapPosition(lamp.X / 32, lamp.Y / 32);
            for (var x = 2; x <= 9; x++) if (x != cell.X) room.Map.RestoreTile(new(x, 2), 0x53);
            Reveal(room); var clear = Light(room);
            room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x3e); var blocked = Light(room);
            check(clear[(lamp.Y + 43) * 416 + lamp.X].B > 0 && blocked[(lamp.Y + 43) * 416 + lamp.X].B == 0,
                "Ice crystal light is occluded by closed doors");
            room.Map.RestoreTile(new(cell.X, cell.Y + 1), 0x53);
            check(clear.SequenceEqual(Light(room)), "Opening the Ice door restores the exact light field");
            // Extend the rock to the lower boundary so the corner under test cannot
            // acquire its own south-facing crystal and illuminate its own shadow.
            for (var y = cell.Y + 1; y < Map.Height; y++) room.Map.RestoreTile(new(cell.X + 1, y), 0x22);
            var corner = Light(room); Save(corner, "corner-mask.png");
            var cornerPixels = (from y in Enumerable.Range((cell.Y + 1) * 32, 32)
                from x in Enumerable.Range((cell.X + 1) * 32, 32) select corner[y * 416 + x].B).ToArray();
            check(cornerPixels.Any(v => v > 0) && cornerPixels.Any(v => v == 0), "Ice light grazes the carved corner without square tile clipping");
            room.Map.RestoreTile(new(cell.X + 1, cell.Y + 1), 0x43);
            check(corner.SequenceEqual(Light(room)), "Ice secret rock cannot be identified by its contour shadow");
            room.Visibility.Initialize(new Map(53, 4, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(0, 0));
            check(Light(room).All(p => p.R == 0 && p.G == 0 && p.B == 0), "Undiscovered Ice crystals cast no visible light");
            foreach (var (monster, scale) in new[] { (21, 1), (23, 1), (11, 2) })
            {
                Scene(54); var outside = Actor(); var portrait = Portrait(); Scene(88); var inside = Actor();
                check(Enumerable.Range(0, inside.Length).All(i => (inside[i].A > 0) == (outside[i].A > 0))
                    && Enumerable.Range(0, inside.Length).Any(i => outside[i].A == 255 && inside[i] != outside[i]),
                    $"Ice creature {monster}: cool tint and contact shading preserve silhouette, hover and scale");
                check(portrait.SequenceEqual(Portrait()), $"Ice creature {monster}: battle artwork remains unchanged");
                Scene(54); check(outside.SequenceEqual(Actor()), $"Ice creature {monster}: leaving restores its original appearance");
                Color[] Actor() => Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, null!));
                Color[] Portrait() => Render(() => Invoke("DrawAnimatedMonster", batch, monster, new Rectangle(32, 32, 48, 48), 0, false, null!));
            }
            CheckReflections();
            CheckAudio();
            Scene(88); Set("miniatureMode", 2); Set("scanlines", true);
            for (var frame = 0; frame < 64; frame++) { Set("sceneTime", frame / 8d); Gameplay(true, $"play-{frame:D2}.png"); }
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            Call("Draw", new GameTime());
        }
        void CheckRoom(Adventure game)
        {
            var id = game.Map.Id; var snapshot = GameSaveCodec.Encode(game.Session, catalog);
            Invoke("PrepareIce", game.Map); var ground = new Color[416 * 384]; terrain.GetData(ground); terrains[id] = ground;
            var rock = (bool[])Invoke("IceStone", game.Map)!;
            var palette = (Color[])wt.GetField("IcePalette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            check(ground.All(p => p.A == 255 && palette.Contains(p)), $"Ice {id}: terrain uses the native frozen-material palette");
            var wall = new List<double>(); var floor = new List<double>();
            for (var y = 0; y < 384; y++) for (var x = 0; x < 416; x++)
            {
                if (x % 32 is < 8 or > 23 || y % 32 is < 6 or > 18) continue;
                var p = ground[y * 416 + x];
                (rock[y * 416 + x] ? wall : floor).Add(p.R * .2126 + p.G * .7152 + p.B * .0722);
            }
            check(wall.Average() - floor.Average() > 25, $"Ice {id}: the quiet floor remains clearly below the raised walls");
            check(ReferenceEquals(rock, Invoke("IceStone", game.Map)), $"Ice {id}: unchanged terrain reuses its cached silhouette");
            var air = Air(game, 1); Save(air, $"air-{id}.png");
            check(air.Count(p => p.A > 0) is > 0 and < 400 && !air.SequenceEqual(Air(game, 2.25)), $"Ice {id}: sparse frost/glints move with presentation time");
            game.Visibility.Initialize(game.Map, new(1, 1)); var partial = Air(game, 1);
            check(Enumerable.Range(0, partial.Length).All(i => partial[i].A == 0 || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                $"Ice {id}: atmosphere stays inside discovered terrain");
            Reveal(game);
            var lightBefore = Light(game);
            var aliases = new List<(MapPosition Cell, ushort Tile)>();
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
            {
                var tile = game.Map.Tile(new(x, y)); if (tile is not (0x43 or 0x44 or 0x49)) continue;
                aliases.Add((new(x, y), tile)); game.Map.RestoreTile(new(x, y), tile == 0x43 ? (ushort)0x22 : tile == 0x44 ? (ushort)0x23 : (ushort)0x53);
            }
            Invoke("PrepareIce", game.Map); var normal = new Color[ground.Length]; terrain.GetData(normal);
            check(ground.SequenceEqual(normal) && air.SequenceEqual(Air(game, 1)) && lightBefore.SequenceEqual(Light(game)),
                $"Ice {id}: secret rock and hidden floor have identical terrain, atmosphere and light");
            foreach (var entry in aliases) game.Map.RestoreTile(entry.Cell, entry.Tile);
            var changedCell = new MapPosition(6, 6); var oldTile = game.Map.Tile(changedCell);
            game.Map.RestoreTile(changedCell, oldTile is 0x22 or 0x43 ? (ushort)0x53 : (ushort)0x22);
            Invoke("PrepareIce", game.Map); terrain.GetData(normal);
            check(!ground.SequenceEqual(normal), $"Ice {id}: tile changes invalidate the terrain cache");
            game.Map.RestoreTile(changedCell, oldTile); Invoke("PrepareIce", game.Map); terrain.GetData(normal);
            check(ground.SequenceEqual(normal), $"Ice {id}: restoring tiles restores the exact terrain");
            check(((Point[])Invoke("IceLights", game.Map)!).Length > 0 && lightBefore.Any(p => p.B > p.R && p.R > 0)
                && lightType.GetField("caveMask", flags)!.GetValue(light) is Texture2D, $"Ice {id}: visible crystals cast cold contour light");
            var materials = new HashSet<string>();
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
                if (game.Map.Tile(new(x, y)) is 0x53 or 0x49) materials.Add(Invoke("FootstepAt", game.Map, new MapPosition(x, y))!.ToString()!);
            check(materials.SetEquals(["Stone"]), $"Ice {id}: hard frozen ground retains solid footfalls rather than water splashes");
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), $"Ice {id}: terrain, light and atmosphere never change gameplay or RNG");
        }
        void CheckReflections()
        {
            var game = Scene(53);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Map.RestoreTile(new(x, y), 0x53);
            Reveal(game); Invoke("PrepareIce", game.Map); player.SnapTo(new(4, 4)); player.Face(new(1, 0));
            var town = Get("town")!;
            Color[] Hero(double time = 1) => Render(() => town.GetType().GetMethod("DrawWorldReflection", flags)!
                .Invoke(town, [batch, player, time, game, world]));
            var before = GameSaveCodec.Encode(game.Session, catalog);
            var still = Hero(); Save(still, "reflection-hero.png");
            check(still.Any(p => p.A > 0) && Enumerable.Range(0, still.Length).All(i => still[i].A == 0 || i / 416 > 153),
                "Ice hero reflection begins below the boots and never draws over the character");
            check(still.SequenceEqual(Hero()) && still.SequenceEqual(Hero(2)), "Solid ice holds a still reflection without water ripples");
            player.MoveTo(new(5, 4)); player.Update(.125);
            var moving = Hero(); Save(moving, "reflection-hero-moving.png");
            check(!still.SequenceEqual(moving) && moving.SequenceEqual(Hero()), "Ice reflects the animated halfway position and freezes with the accepted step");
            player.SnapTo(new(4, 4));
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Map.RestoreTile(new(x, y), 0x49);
            Invoke("PrepareIce", game.Map);
            check(still.SequenceEqual(Hero()), "Hidden ice floor has exactly the same reflectivity as ordinary ice");
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Map.RestoreTile(new(x, y), 0x53);
            Invoke("PrepareIce", game.Map);
            foreach (var (monster, scale) in new[] { (21, 1), (23, 1), (11, 2) })
            {
                var reflected = Render(() => Invoke("DrawMonsterActor", batch, monster, new Vector2(4, 4), scale, 1d, false, null!, game));
                Save(reflected, $"reflection-monster-{monster}.png");
                check(reflected.Any(p => p.A > 0) && Enumerable.Range(0, reflected.Length).All(i => reflected[i].A == 0 || i / 416 > (4 + scale) * 32),
                    $"Ice reflection {monster}: grounded, hovering and large creatures stay below their ground anchor");
            }
            // A solid test sprite exposes clipping holes independently of the actor artwork.
            using var solid = new Texture2D(host.GraphicsDevice, 48, 48); solid.SetData(Enumerable.Repeat(Color.White, 48 * 48).ToArray());
            Color[] Patch() => Render(() => Invoke("DrawIceReflection", batch, game, solid, new Rectangle(0, 0, 48, 48), new Point(160, 154), 2, false, 0));
            var clear = Patch();
            check(clear.Where(p => p.A > 0).Select(p => p.A).Distinct().Count() > 10 && clear.Max(p => p.A) <= 97,
                "Ice reflection is restrained and fades through frost, cracks and distance");
            game.Map.RestoreTile(new(4, 5), 0x22); game.Map.RestoreTile(new(5, 5), 0x3e); game.Map.RestoreTile(new(6, 5), 0x3a);
            Invoke("PrepareIce", game.Map); var clipped = Patch(); var stone = (bool[])Invoke("IceStone", game.Map)!;
            check(!clipped.SequenceEqual(clear) && Enumerable.Range(0, clipped.Length).All(i => clipped[i].A == 0
                || !stone[i] && game.Map.Tile(new(i % 416 / 32, i / 416 / 32)) is 0x53 or 0x49 or 0x22 or 0x43 or 0x23 or 0x44),
                "Ice reflections stop at wall contours, closed doors and stairs");
            check(Enumerable.Range(0, clipped.Length).Any(i => clipped[i].A > 0 && !stone[i]
                && game.Map.Tile(new(i % 416 / 32, i / 416 / 32)) == 0x22),
                "Exposed ice at a carved rock corner reflects without a square tile cutoff");
            check(Enumerable.Range(192, 26).All(y => clipped[y * 416 + 176].A == 0),
                "A large ice reflection cannot reappear beyond the far side of a closed door");
            for (var x = 4; x <= 6; x++) game.Map.RestoreTile(new(x, 5), 0x53);
            Invoke("PrepareIce", game.Map); check(clear.SequenceEqual(Patch()), "Opening changed terrain restores the exact ice reflection mask");
            game.Visibility.Initialize(new Map(53, 4, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(4, 4));
            var fog = Patch(); check(Enumerable.Range(0, fog.Length).All(i => fog[i].A == 0
                || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))), "Ice reflection pixels cannot reveal undiscovered destinations");
            game.Visibility.Initialize(new Map(53, 4, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(0, 0));
            check(Hero().All(p => p.A == 0) && Render(() => Invoke("DrawIceMonsterReflections", batch, game, player, 1d)).All(p => p.A == 0),
                "Hidden actors cannot reveal themselves through an ice reflection");
            Reveal(game);
            check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), "Ice reflection rendering preserves the full adventure and RNG");
            var roamer = new RoamingMonster(23, new(4, 4));
            var pursuer = new PursuingMonster(21, new(7, 4), new(7, 4));
            typeof(Adventure).GetField("roaming", flags)!.SetValue(game, roamer);
            typeof(Adventure).GetProperty("Pursuer")!.SetValue(game, pursuer);
            Invoke("SynchronizeMonsters", game, false, null!);
            Color[] Roamers() => Render(() => Invoke("DrawRoamingActor", batch, game, 4, 1d, true));
            var planted = Roamers(); roamer.Position = new(5, 4); pursuer.Position = new(6, 4);
            Invoke("SynchronizeMonsters", game, true, null!); Invoke("UpdateMonsters", .125d);
            var accepted = GameSaveCodec.Encode(game.Session, catalog); var halfway = Roamers();
            check(planted.Any(p => p.A > 0) && !planted.SequenceEqual(halfway) && halfway.SequenceEqual(Roamers())
                && accepted.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)),
                "Roaming and pursuing reflections follow accepted visual travel without advancing gameplay");
            game = Scene(54);
            check(Render(() => Invoke("DrawIceReflection", batch, game, solid, new Rectangle(0, 0, 48, 48), new Point(160, 154), 2, false, 0)).All(p => p.A == 0),
                "Ice reflectivity never leaks into another cave");
            var walking = Scene(53); Set("miniatureMode", 2); Set("scanlines", true);
            var start = walking.Location.Position;
            var next = new[] { new MapPosition(start.X + 1, start.Y), new MapPosition(start.X - 1, start.Y) }
                .First(p => Map.Contains(p) && walking.Map.Tile(p) is 0x53 or 0x49
                    && !walking.ActivePlacements.Any(monster => monster.Contains(p)));
            player.SnapTo(start); player.MoveTo(next);
            for (var frame = 0; frame < 16; frame++)
            {
                if (frame > 0) player.Update(PlayerMotion.StepDuration / 15);
                Set("sceneTime", frame / 60d); Gameplay(true, $"ice-step-{frame:D2}.png");
            }
        }

        void CheckAudio()
        {
            var audio = Get("audio")!; var at = audio.GetType();
            var enabled = (bool)at.GetProperty("Enabled")!.GetValue(audio)!; var volume = (float)at.GetProperty("Volume")!.GetValue(audio)!;
            SoundEffectInstance? Voice() => (SoundEffectInstance?)at.GetField("ambience", flags)!.GetValue(audio);
            try
            {
                var game = Scene(53); var before = GameSaveCodec.Encode(game.Session, catalog);
                at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, .5f);
                at.GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]); Call("UpdateCaveAmbience", .1d); var voice = Voice()!;
                check(voice is not null && voice.State == SoundState.Playing && voice.IsLooped && voice.Volume < .09f,
                    "Ice ambience starts with one quietly fading recorded loop");
                Call("UpdateCaveAmbience", .8d);
                check(Math.Abs(voice!.Volume - .09f) < .001f, "Ice ambience respects the restrained master-volume mix");
                Scene(41); Call("UpdateCaveAmbience", .1d); check(ReferenceEquals(voice, Voice()), "Upper Ice rooms retain the existing ambient voice");
                Scene(88); Call("UpdateCaveAmbience", .1d);
                check(ReferenceEquals(voice, Voice()) && Math.Abs(voice.Volume - .08f) < .001f, "Deep Ice rooms quietly reduce the same loop without restarting it");
                Set("paused", true); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Pause silences Ice ambience");
                Set("paused", false); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Playing, "Resume continues Ice ambience");
                Set("mapOpen", true); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Map view silences Ice ambience"); Set("mapOpen", false);
                Call("ShowActions"); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Action overlays silence Ice ambience"); Set("overlay", null);
                Call("UpdateCaveAmbience", .1d); at.GetMethod("Update")!.Invoke(audio, [false]); check(voice.State == SoundState.Paused, "Focus loss silences Ice ambience");
                at.GetProperty("Enabled")!.SetValue(audio, false); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Sound Off silences Ice ambience");
                at.GetProperty("Enabled")!.SetValue(audio, true); at.GetProperty("Volume")!.SetValue(audio, 0f); Call("UpdateCaveAmbience", .1d); check(voice.State == SoundState.Paused, "Zero master volume silences Ice ambience");
                at.GetProperty("Volume")!.SetValue(audio, .5f); Scene(57); Call("UpdateCaveAmbience", .1d);
                check(voice.IsDisposed && Voice() is { State: SoundState.Playing }, "Leaving Ice for Earth replaces the ambient voice");
                Scene(53); Call("UpdateCaveAmbience", .1d); var returning = Voice()!;
                check(ReferenceEquals(at.GetField("ambienceTrack", flags)!.GetValue(audio), at.GetField("iceAmbience", flags)!.GetValue(audio)), "Returning to Ice selects the recorded ice loop");
                Scene(54); Call("UpdateCaveAmbience", .1d); check(returning.IsDisposed && Voice() is { State: SoundState.Playing }, "Entering Final Cave replaces the ice loop");
                check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), "Ice audio preserves gameplay and RNG");
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
        Color[] Air(Adventure game, double time) => Render(() => Invoke("DrawIceAtmosphere", batch, game, time));
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
