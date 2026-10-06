using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class HellPresentationChecks
{
    public static void Run(GraphicsDevice device, object world, object town, MapCatalog catalog, string output, Action<bool, string> check)
    {
        var type = world.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var draw = type.GetMethod("Draw")!;
        var drawMap = type.GetMethod("DrawMap", flags)!;
        var scenery = type.GetMethod("DrawHellScenery", flags)!;
        var atmosphere = type.GetMethod("DrawHellAtmosphere", flags)!;
        var ground = type.GetMethod("DrawHellGround", flags)!;
        var prepare = type.GetMethod("PrepareHell", flags)!;
        var palette = (Color[])type.Assembly.GetType("Yipe.Desktop.RetroArt")!.GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var atlas = (Texture2D)type.GetField("hellProps", flags)!.GetValue(world)!;
        var pixels = new Color[atlas.Width * atlas.Height]; atlas.GetData(pixels);
        check(pixels.All(p => p.A == 0 || p.A == 255 && palette.Contains(p)), "Hell sprites compile to the shared palette and binary alpha");
        for (var cell = 0; cell < 8; cell++)
        {
            var ink = (from y in Enumerable.Range(0, 48) from x in Enumerable.Range(0, 48)
                where pixels[(cell / 4 * 48 + y) * atlas.Width + cell % 4 * 48 + x].A > 0 select new Point(x, y)).ToArray();
            check(ink.Length > 60 && ink.All(p => p.X > 0 && p.X < 47 && p.Y > 0 && p.Y < 47),
                $"Hell sprite {cell + 1} is complete with a clear frame border");
        }
        var directory = Path.Combine(output, "hell-biome"); Directory.CreateDirectory(directory);
        using (var file = File.Create(Path.Combine(directory, "native-hell-atlas.png"))) atlas.SaveAsPng(file, atlas.Width, atlas.Height);
        using var target = new RenderTarget2D(device, 416, 384);
        using var batch = new SpriteBatch(device);
        // The east door is visible from the lower room, but its crown reaches
        // into the still-hidden corridor above it.
        var doorGame = new Adventure(new GameSession(catalog, location: new()
        { MapId = 117, AlternateMapSet = true, RegionX = -4, RegionY = -1, Position = new(9, 5) }));
        var eastDoor = new MapPosition(11, 3);
        check(doorGame.Visibility.Contains(eastDoor) && !doorGame.Visibility.Contains(new(11, 2)),
            "East Hell door fixture keeps the corridor behind the visible door unexplored");
        var doorArt = Layer(() => scenery.Invoke(world, [batch, doorGame.Map, eastDoor, 1d]));
        var crown = Enumerable.Range(0, doorArt.Length).Where(i => i / 416 < eastDoor.Y * 32 && doorArt[i].A == 255).ToArray();
        var doorFrame = Layer(() => draw.Invoke(world, [batch, doorGame, new PlayerMotion(new MapPosition(9, 5)), town, 1d, false]));
        Save("east-door-unexplored.png");
        check(crown.Length > 0 && crown.All(i => doorFrame[i] == doorArt[i]),
            "The visible east Hell door keeps its complete crown beside an unexplored corridor");
        var seen = Enumerable.Range(0, Map.TileCount).Select(i => doorGame.Visibility.Contains(new(i / Map.Height, i % Map.Height))).ToArray();
        var mapFrame = Layer(() => drawMap.Invoke(world, [batch, doorGame.Map, seen, 1d]));
        Save("east-door-area-map.png");
        check(crown.All(i => mapFrame[i] == doorArt[i]), "The area map also preserves the complete known Hell door");
        var hidden = Enumerable.Range(0, doorFrame.Length).Where(i => !seen[i % 416 / 32 * Map.Height + i / 416 / 32]).ToArray();
        check(hidden.All(i => doorArt[i].A == 255 || doorFrame[i] == palette[0] && mapFrame[i] == palette[0]),
            "Restoring the known door crown reveals no terrain or objects in the unexplored corridor");
        var beforeDoorRender = GameSaveCodec.Encode(doorGame.Session, catalog);
        foreach (var tile in new ushort[] { 0x3e, 0x3f, 0x40 })
        {
            doorGame.Map.RestoreTile(eastDoor, tile);
            doorArt = Layer(() => scenery.Invoke(world, [batch, doorGame.Map, eastDoor, 2d]));
            doorFrame = Layer(() => draw.Invoke(world, [batch, doorGame, new PlayerMotion(new MapPosition(9, 5)), town, 2d, false]));
            check(crown.All(i => doorFrame[i] == doorArt[i]), $"Hell door {tile:x2} crown matches the full sprite's lighting");
        }
        check(GameSaveCodec.Encode(doorGame.Session, catalog).SequenceEqual(beforeDoorRender)
            && seen.SequenceEqual(Enumerable.Range(0, Map.TileCount).Select(i => doorGame.Visibility.Contains(new(i / Map.Height, i % Map.Height)))),
            "Rendering the door crown preserves gameplay, RNG and exploration visibility");
        doorGame.Map.OpenDoor(new(11, 4), eastDoor);
        doorFrame = Layer(() => draw.Invoke(world, [batch, doorGame, new PlayerMotion(new MapPosition(9, 5)), town, 1d, false]));
        mapFrame = Layer(() => drawMap.Invoke(world, [batch, doorGame.Map, seen, 1d]));
        check(hidden.All(i => doorFrame[i] == palette[0] && mapFrame[i] == palette[0]),
            "Opening the Hell door leaves no orphaned crown over unexplored cells");
        foreach (var id in new[] { 117, 118, 119, 120 })
            foreach (uint seed in id == 117 ? new uint[] { 1 } : [1, 7, 99])
            {
                var session = new GameSession(catalog, location: new()
                { MapId = id, AlternateMapSet = true, RegionX = id - 121, RegionY = -1, Position = new(6, 10) }, randomState: seed);
                var game = new Adventure(session);
                var snapshot = GameSaveCodec.Encode(session, catalog);
                var original = Enumerable.Range(0, Map.TileCount).Select(i => game.Map.Tile(new(i / Map.Height, i % Map.Height))).ToArray();
                Color[] Frame(double time, bool fullMap = false)
                {
                    return Layer(() =>
                    {
                        if (fullMap) drawMap.Invoke(world, [batch, game.Map, Enumerable.Repeat(true, Map.TileCount).ToArray(), time]);
                        else draw.Invoke(world, [batch, game, new PlayerMotion(new MapPosition(6, 10)), town, time, false]);
                    });
                }
                var partial = Frame(1);
                var unseen = Enumerable.Range(0, partial.Length).Where(i => !game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))).ToArray();
                check(unseen.All(i => partial[i] == palette[0]), $"Hell {id} seed {seed}: architecture and smoke cannot expose unrevealed cells");
                for (var y = 0; y < Map.Height; y++)
                    for (var x = 0; x < Map.Width; x++) game.Visibility.RevealFrom(game.Map, new(x, y));
                var first = Frame(1); var repeat = Frame(1); var later = Frame(1.75);
                check(first.SequenceEqual(repeat) && !first.SequenceEqual(later), $"Hell {id} seed {seed}: all animation follows scene time and freezes exactly");
                Save($"map-{id}-seed-{seed}.png");
                check(GameSaveCodec.Encode(session, catalog).SequenceEqual(snapshot)
                    && Enumerable.Range(0, Map.TileCount).All(i => game.Map.Tile(new(i / Map.Height, i % Map.Height)) == original[i]),
                    $"Hell {id} seed {seed}: art preserves the full gameplay snapshot, map and RNG");
                var lava = (Texture2D)type.GetField("hellLava", flags)!.GetValue(world)!;
                var hot = new Color[lava.Width * lava.Height]; lava.GetData(hot);
                var molten = Enumerable.Range(0, hot.Length).Where(i => hot[i].A != 0).ToArray();
                check(molten.Length > 100 && molten.All(i => game.Map.Tile(new(i % lava.Width % 416 / 32, i / lava.Width % 384 / 32)) == 0x24),
                    $"Hell {id} seed {seed}: every lava frame stays strictly within original fire-wall cells");
                if (id != 117 || seed != 1) continue;
                var monster = type.GetMethod("DrawWorldMonster", flags)!;
                foreach (var x in new[] { 4, 8 })
                {
                    var placement = game.ActivePlacements.Single(p => p.Position == new MapPosition(x, 7));
                    var sprite = Layer(() => monster.Invoke(world, [batch, placement.TypeId, placement.Position, 1]));
                    var body = Enumerable.Range(0, sprite.Length).Where(i => sprite[i].A == 255).ToArray();
                    check(body.Length > 0 && body.All(i => first[i] == sprite[i]),
                        $"Hell A4 monster at {x},7 keeps its complete silhouette beside the low ruins");
                    var ruin = Layer(() => scenery.Invoke(world, [batch, game.Map, new MapPosition(x, 8), 1d]));
                    check(ruin.Any(p => p.A > 0) && ruin.Take(8 * 32 * 416).All(p => p.A == 0),
                        $"Hell A4 ruin at {x},8 stays below the neighboring monster's feet");
                }
                var flame = type.GetMethod("DrawHellFlame", flags)!;
                Color[] Flame(int phase, double time, int size) => Layer(() => flame.Invoke(world, [batch, new Vector2(100, 100), phase, time, size]));
                var fire = Flame(23, 1, 14);
                check(fire.SequenceEqual(Flame(23, 1, 14)) && !fire.SequenceEqual(Flame(23, 1.5, 14))
                    && !fire.SequenceEqual(Flame(62, 1, 14)), "Hell flames freeze, animate and have independent local phases");
                check(Flame(23, 1, 7).Count(p => p.A > 0) < fire.Count(p => p.A > 0),
                    "Small vent flames and larger flame clusters have visibly different silhouettes");
                var glow = (Texture2D)type.GetField("hellGlow", flags)!.GetValue(world)!;
                var light = new Color[416 * 384]; glow.GetData(light);
                check(light.Any(p => p.A > 0) && light.All(p => p.A <= 57) && Enumerable.Range(0, light.Length)
                    .Any(i => game.Map.Tile(new(i % 416 / 32, i / 416 / 32)) != 0x24 && light[i].A > 0),
                    "Hell firelight softly reaches nearby walkable floor without an opaque veil");
                var air = Layer(() => atmosphere.Invoke(world, [batch, game, 1d]));
                var airLater = Layer(() => atmosphere.Invoke(world, [batch, game, 2d]));
                check(!air.SequenceEqual(airLater) && air.Count(p => p.A > 0) is > 20 and < 20000,
                    "Hell embers, ash, smoke and bubbles animate with sparse coverage");
                var coolAir = Layer(() => atmosphere.Invoke(world, [batch, new Adventure(new GameSession(catalog)), 1d]));
                check(coolAir.All(p => p.A == 0), "Hell atmosphere is absent from the village");
                var floorFirst = Layer(() => ground.Invoke(world, [batch, new Rectangle(5 * 32, 7 * 32, 32, 32), 1d]));
                var floorLater = Layer(() => ground.Invoke(world, [batch, new Rectangle(5 * 32, 7 * 32, 32, 32), 1.4d]));
                check(!floorFirst.SequenceEqual(floorLater), "Hell firelight fluctuates on the nearby floor independently of lava movement");
                var doors = new MapPosition(4, 5);
                var closed = Layer(() => scenery.Invoke(world, [batch, game.Map, doors, 1d]));
                game.Map.OpenDoor(new(5, 5), doors); prepare.Invoke(world, [game.Map]);
                var opened = Layer(() => scenery.Invoke(world, [batch, game.Map, doors, 1d]));
                check(closed.Any(p => p.A > 0) && opened.All(p => p.A == 0), "Opening a Hell door immediately removes its iron door artwork");
                game.Map.RestoreTile(doors, 0x3e); prepare.Invoke(world, [game.Map]);
                var hotCell = new MapPosition(4, 8);
                game.Map.RestoreTile(hotCell, 0x27); prepare.Invoke(world, [game.Map]); lava.GetData(hot);
                check(Enumerable.Range(0, 8).All(f => (from y in Enumerable.Range(8 * 32, 32) from x in Enumerable.Range(4 * 32, 32)
                    select hot[(f / 4 * 384 + y) * lava.Width + f % 4 * 416 + x]).All(p => p.A == 0)),
                    "Changed fire-wall terrain clears molten pixels from every cached animation frame");
                game.Map.RestoreTile(hotCell, 0x24); prepare.Invoke(world, [game.Map]);
                session.Player.HellGatekeeperState = HellGatekeeperState.Absent;
                var absent = Frame(1);
                session.Player.HellGatekeeperState = HellGatekeeperState.Waiting;
                var waiting = Frame(1); Save("gatekeeper-waiting.png");
                check(absent.Zip(waiting, (a, b) => a != b).Count(v => v) > 100,
                    "The waiting Gatekeeper remains visible beside the ruined entrance");
                Frame(1, fullMap: true); Save("area-map.png");
            }

        Color[] Layer(Action render)
        {
            device.SetRenderTarget(target); device.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); render(); batch.End(); device.SetRenderTarget(null);
            var data = new Color[416 * 384]; target.GetData(data); return data;
        }
        void Save(string name)
        {
            using var file = File.Create(Path.Combine(directory, name)); target.SaveAsPng(file, target.Width, target.Height);
        }
    }
}
