using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class HeatPresentationChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        var type = game.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        var fields = new[] { "session", "adventure", "cameraPosition", "paused", "pauseAmount", "overlay",
            "mapOpen", "worldMapOpen", "sceneTime", "miniatureMode", "scanlines", "vignette", "messageRemaining", "journal", "selectionY" };
        var saved = fields.ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!;
        var position = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var world = Get("world")!;
        var heat = Get("heat")!;
        var apply = heat.GetType().GetMethod("Apply")!;
        var draw = type.GetMethod("Draw", flags)!;
        var update = type.GetMethod("Update", flags)!;
        var miniature = Get("miniature")!;
        var source = (Texture2D)miniature.GetType().GetProperty("Result")!.GetValue(miniature)!;
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        var directory = Path.Combine(output, "heat"); Directory.CreateDirectory(directory);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null);
            Set("mapOpen", false); Set("miniatureMode", 2); Set("scanlines", false); Set("messageRemaining", 0d);
            Set("vignette", false);
            foreach (var id in new[] { 1, 7, 117, 118, 119, 120, 17, 41 })
            {
                var layer = id >= 117 ? 0 : (id - 1) / 40;
                var cell = id >= 117 ? id - 99 : id - 1 - layer * 40;
                var session = new GameSession(catalog, location: new()
                {
                    MapId = id, Position = new(6, 5), MapLayer = layer, AlternateMapSet = id >= 117,
                    RegionX = cell % 6 - 4, RegionY = 2 - cell / 6,
                });
                Set("session", session); Set("adventure", new Adventure(session));
                player.SnapTo(new(6, 5)); Set("cameraPosition", new Vector2(48, 86));
                var adventure = (Adventure)Get("adventure")!;
                for (var x = 0; x < Map.Width; x++)
                    for (var y = 0; y < Map.Height; y++) adventure.Visibility.RevealFrom(adventure.Map, new(x, y));
                var snapshot = GameSaveCodec.Encode(session, catalog);
                var screen = Frame(1);
                var hot = id is 1 or 7 or >= 117;
                var hasHeat = id != 17 && (bool)world.GetType().GetProperty("HasHeat", flags)!.GetValue(world)!;
                check(hasHeat == hot, $"Heat presentation selects the expected biome in map {id}");
                Save(screen, $"map-{id:D3}.png");
                Set("vignette", true);
                var vignette = Frame(1);
                Save(vignette, $"map-{id:D3}-vignette.png");
                Set("vignette", false);
                check((from y in Enumerable.Range(300, 120) from x in Enumerable.Range(560, 160) select y * 1280 + x)
                    .All(i => screen[i] == vignette[i]) && !screen.SequenceEqual(vignette),
                    $"Map {id} vignette combines with heat and miniature while keeping the central scene unchanged");
                if (hot)
                {
                    var first = Refract(1); var repeated = Refract(1); var later = Refract(2);
                    check(first.SequenceEqual(repeated) && !first.SequenceEqual(later),
                        $"Map {id} heat freezes at a fixed clock and refracts scenery as time advances");
                    if (id is >= 117 and <= 120)
                    {
                        var raw = new Color[1280 * 720]; source.GetData(raw);
                        var colors = raw.Select(p => p.PackedValue).ToHashSet();
                        check(first.All(p => colors.Contains(p.PackedValue)),
                            $"Hell {id} heat moves existing pixel colors without blending its gate and rock edges");
                        var native = (Texture2D)miniature.GetType().GetProperty("World")!.GetValue(miniature)!;
                        var nativePixels = new Color[native.Width * native.Height]; native.GetData(nativePixels);
                        check(Enumerable.Range(0, raw.Length).All(i => raw[i] == nativePixels[i / 1280 / 4 * native.Width + i % 1280 / 4]),
                            $"Hell {id} retains every native pixel exactly even with Strong miniature selected");
                    }
                    if (id == 1)
                    {
                        var raw = new Color[1280 * 720]; source.GetData(raw);
                        var near = Refract(1 + 1d / 60);
                        double Change(Color[] a, Color[] b) => a.Zip(b, (p, q) => Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B)).Average();
                        check(Change(first, near) < Change(first, later) * .25,
                            "Heat refraction moves smoothly between display frames without a stepped clock");
                        var hero = player.VisualPosition * 32 + new Vector2(16, 12) - (Vector2)Get("cameraPosition")!;
                        var heroPixels = from y in Enumerable.Range((int)(hero.Y - 12) * 4, 24 * 4)
                            from x in Enumerable.Range((int)(hero.X - 12) * 4, 24 * 4) select y * 1280 + x;
                        check(heroPixels.All(i => first[i] == raw[i]), "Heat preserves the character and nearby scenery pixel-for-pixel");
                        check(first.All(p => p.A == 255), "Heat fills the complete viewport without transparent seams");
                        Set("paused", true); Set("pauseAmount", 1f);
                        Set("selectionY", (int)Get("selected")! * 10f);
                        var paused = Frame(1);
                        update.Invoke(game, [new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.2))]);
                        check((double)Get("sceneTime")! == 1 && paused.SequenceEqual(Frame(1)),
                            "The paused game freezes heat, camera and other scene animation together");
                        var menuLater = Frame(2);
                        check((from y in Enumerable.Range(6 * 4, 168 * 4) from x in Enumerable.Range(34 * 4, 252 * 4)
                            select y * 1280 + x).All(i => paused[i] == menuLater[i]),
                            "Heat never distorts the opaque pause panel or its text");
                        Set("paused", false); Set("pauseAmount", 0f);
                        Set("miniatureMode", 0); Save(Frame(1), "desert-miniature-off.png");
                        Set("miniatureMode", 2);
                        var journal = new MapJournal(); journal.Observe(adventure); Set("journal", journal);
                        Set("mapOpen", true); Set("worldMapOpen", false); Save(Frame(1), "desert-area-map.png");
                        Set("mapOpen", false);
                    }
                    if (id == 7)
                    {
                        var raw = new Color[1280 * 720]; source.GetData(raw);
                        check((from y in Enumerable.Range(8 * 4, 164 * 4) from x in Enumerable.Range(260 * 4, 52 * 4)
                            select y * 1280 + x).All(i => first[i] == raw[i]),
                            "Desert heat leaves the distant grassy side of a mixed region untouched");
                    }
                }
                check(GameSaveCodec.Encode(session, catalog).SequenceEqual(snapshot), $"Heat rendering map {id} preserves the complete gameplay state and RNG");
                if (id is >= 117 and <= 120)
                {
                    Set("scanlines", true); Save(Frame(1), $"hell-{id}-defaults.png");
                    Set("scanlines", false); Set("miniatureMode", 0); Save(Frame(1), $"hell-{id}-crisp.png");
                    Set("scanlines", true); Save(Frame(1), $"hell-{id}-miniature-off.png"); Set("scanlines", false);
                    Set("miniatureMode", 2);
                }
                if (id is 1 or 117)
                {
                    Set("miniatureMode", 0);
                    var heatOnly = Frame(1);
                    Set("vignette", true); var heatVignette = Frame(1);
                    check((from y in Enumerable.Range(300, 120) from x in Enumerable.Range(560, 160) select y * 1280 + x)
                        .All(i => heatOnly[i] == heatVignette[i]) && !heatOnly.SequenceEqual(heatVignette),
                        $"Map {id} heat also composes correctly with vignette when miniature is off");
                    Set("vignette", false); Set("miniatureMode", 2);
                }
                if (id == 117)
                {
                    var keeper = session.Player.HellGatekeeperState;
                    session.Player.HellGatekeeperState = HellGatekeeperState.Waiting;
                    player.SnapTo(new(6, 1)); Set("cameraPosition", new Vector2(48, 0));
                    Save(Frame(1), "hell-entrance.png");
                    player.SnapTo(new(6, 2));
                    Save(Frame(1), "hell-a4-6-2.png");
                    Set("scanlines", true); Save(Frame(1), "hell-a4-defaults.png");
                    Set("miniatureMode", 0); Save(Frame(1), "hell-a4-sharp.png"); Set("miniatureMode", 2);
                    session.Player.HellGatekeeperState = keeper;
                    player.SnapTo(new(6, 5)); Set("cameraPosition", new Vector2(48, 86));
                    Save(Frame(1), "hell-path-defaults.png");
                    player.SnapTo(new(6, 7)); Set("cameraPosition", new Vector2(48, 150));
                    Save(Frame(1), "hell-a4-6-7-defaults.png");
                    var doorway = new Adventure(new GameSession(catalog, location: new()
                    { MapId = 117, AlternateMapSet = true, RegionX = -4, RegionY = -1, Position = new(9, 5) }));
                    Set("session", doorway.Session); Set("adventure", doorway);
                    player.SnapTo(new(9, 5)); Set("cameraPosition", new Vector2(96, 64));
                    Save(Frame(1), "hell-east-door-defaults.png");
                    Set("session", session); Set("adventure", adventure);
                    player.SnapTo(new(6, 5)); Set("cameraPosition", new Vector2(48, 86)); Set("scanlines", false);
                }
                if (id is not (1 or 117)) continue;
                var frames = Path.Combine(directory, id == 1 ? "desert" : "hell"); Directory.CreateDirectory(frames);
                for (var frame = 0; frame < 144; frame++) Save(Frame(frame / 24d), Path.Combine(frames, $"frame-{frame:D3}.png"));
            }
            var d4 = new GameSession(catalog, location: new()
            { MapId = 22, RegionX = -1, RegionY = -1, Position = new(5, 6) });
            var d4Snapshot = GameSaveCodec.Encode(d4, catalog);
            Set("session", d4); Set("adventure", new Adventure(d4));
            player.SnapTo(d4.Location.Position); Set("cameraPosition", new Vector2(16, 118));
            Set("scanlines", true); Save(Frame(1), "surface-d4-defaults.png");
            check(GameSaveCodec.Encode(d4, catalog).SequenceEqual(d4Snapshot),
                "Surface D4 at 5,6 renders the grounded monsters without changing gameplay state or RNG");
            foreach (var (id, entry) in new (int, MapPosition)[]
                { (57, new(3, 4)), (63, new(5, 5)), (69, new(8, 6)), (75, new(7, 6)),
                  (103, new(6, 5)), (109, new(7, 5)), (115, new(5, 8)) })
            {
                var layer = (id - 1) / 40; var cell = id - layer * 40 - 1;
                var earth = new GameSession(catalog, location: new()
                { MapId = id, MapLayer = layer, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = entry });
                var adventure = new Adventure(earth);
                var snapshot = GameSaveCodec.Encode(earth, catalog);
                for (var x = 0; x < Map.Width; x++)
                    for (var y = 0; y < Map.Height; y++) adventure.Visibility.RevealFrom(adventure.Map, new(x, y));
                Set("session", earth); Set("adventure", adventure); player.SnapTo(entry);
                Set("cameraPosition", new Vector2(Math.Clamp(entry.X * 32 - 144, 0, 96), Math.Clamp(entry.Y * 32 - 74, 0, 204)));
                Save(Frame(1), $"earth-{id:D3}-defaults.png");
                if (id == 57)
                {
                    var frames = Path.Combine(directory, "earth-puddle-walk"); Directory.CreateDirectory(frames);
                    player.SnapTo(new(3, 3));
                    for (var frame = 0; frame < 64; frame++)
                    {
                        if (frame is 8 or 32 or 40)
                        {
                            var step = adventure.Map.Move(player.Position, 0, 1);
                            check(step.Moved, "The Earth E3 puddle route accepts a walking step without water equipment");
                            player.MoveTo(step.Position);
                        }
                        player.Update(1d / 32);
                        Save(Frame(1 + frame / 32d), Path.Combine(frames, $"frame-{frame:D2}.png"));
                    }
                    check(!player.IsMoving && player.Position == new MapPosition(3, 6),
                        "The Earth E3 walk-through retains the approved walking pace and reaches dry ground");
                    player.SnapTo(entry);
                }
                if (id == 109)
                {
                    Set("miniatureMode", 0); Save(Frame(1), "earth-109-crisp.png"); Set("miniatureMode", 2);
                    Set("paused", true); Set("pauseAmount", 1f); Set("selectionY", (int)Get("selected")! * 10f);
                    var paused = Frame(1);
                    update.Invoke(game, [new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.2))]);
                    check((double)Get("sceneTime")! == 1 && paused.SequenceEqual(Frame(1)),
                        "Pausing Earth Cave freezes its ambience, camera and presentation together");
                    Save(paused, "earth-paused.png"); Set("paused", false); Set("pauseAmount", 0f);
                }
                check(GameSaveCodec.Encode(earth, catalog).SequenceEqual(snapshot),
                    $"Earth Cave {id} default-effects view preserves complete gameplay state and RNG");
            }
        }
        finally
        {
            foreach (var pair in saved) Set(pair.Key, pair.Value);
            player.SnapTo(position);
        }

        Color[] Frame(double time)
        {
            Set("sceneTime", time);
            draw.Invoke(game, [new GameTime(TimeSpan.FromSeconds(time), TimeSpan.Zero)]);
            var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels;
        }
        Color[] Refract(double time)
        {
            var target = (Texture2D)apply.Invoke(heat, [batch, source, world, Get("cameraPosition")!,
                player.VisualPosition * 32 + new Vector2(16, 12), time,
                ((GameSession)Get("session")!).CurrentMap.Id is >= 117 and <= 120])!;
            game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[1280 * 720]; target.GetData(pixels); return pixels;
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels);
            using var file = File.Create(Path.IsPathRooted(name) ? name : Path.Combine(directory, name));
            capture.SaveAsPng(file, 1280, 720);
        }
    }
}
