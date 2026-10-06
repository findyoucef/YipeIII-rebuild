using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class EarthPresentationChecks
{
    public static void Run(GraphicsDevice device, object world, object town, MapCatalog catalog, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = world.GetType();
        var draw = type.GetMethod("Draw")!;
        var drawMap = type.GetMethod("DrawMap", flags)!;
        var prepare = type.GetMethod("PrepareEarth", flags)!;
        var prepareWater = type.GetMethod("PrepareWater", flags)!;
        var drawWater = type.GetMethod("DrawWater", flags)!;
        var reflection = town.GetType().GetMethod("DrawWorldReflection", flags)!;
        var atmosphere = type.GetMethod("DrawEarthAtmosphere", flags)!;
        var texture = (Texture2D)type.GetField("earthGround", flags)!.GetValue(world)!;
        var palette = (Color[])type.Assembly.GetType("Yipe.Desktop.RetroArt")!.GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var directory = Path.Combine(output, "earth-cave"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(device, 416, 384);
        using var batch = new SpriteBatch(device);
        foreach (var id in new[] { 57, 63, 69, 75, 103, 109, 115 })
        {
            var layer = (id - 1) / 40; var cell = id - layer * 40 - 1;
            var session = new GameSession(catalog, location: new()
            { MapId = id, MapLayer = layer, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = new(1, 1) });
            var game = new Adventure(session);
            var snapshot = GameSaveCodec.Encode(session, catalog);
            var clock = Stopwatch.StartNew(); prepare.Invoke(world, [game.Map]); clock.Stop();
            Console.WriteLine($"Earth {id} terrain preparation: {clock.Elapsed.TotalMilliseconds:F1} ms");
            var original = Ground();
            check(original.All(p => p.A == 255), $"Earth {id} terrain fills every native pixel without gaps");
            check(original.All(p => palette.Contains(p)), $"Earth {id} terrain and landmarks use only the shared retro palette");
            var wallLight = new List<double>(); var floorLight = new List<double>();
            for (var y = 0; y < 384; y++)
                for (var x = 0; x < 416; x++)
                {
                    if (x % 32 is < 8 or > 23 || y % 32 is < 6 or > 18) continue;
                    var pixel = original[y * 416 + x]; var tile = game.Map.Tile(new(x / 32, y / 32));
                    (tile is 0x22 or 0x43 ? wallLight : floorLight).Add(pixel.R * .2126 + pixel.G * .7152 + pixel.B * .0722);
                }
            var contrast = wallLight.Average() - floorLight.Average();
            Console.WriteLine($"Earth {id} wall/floor luminance separation: {contrast:F1}/255");
            check(contrast >= 30, $"Earth {id} wall tops remain clearly brighter than the walkable floor");
            clock.Restart(); for (var repeat = 0; repeat < 100; repeat++) prepare.Invoke(world, [game.Map]); clock.Stop();
            Console.WriteLine($"Earth {id} 100 cached preparations: {clock.Elapsed.TotalMilliseconds:F1} ms");
            check(original.SequenceEqual(Ground()), $"Earth {id} cached preparation is deterministic");
            var partialAir = Layer(() => atmosphere.Invoke(world, [batch, game, 1d]));
            check(Enumerable.Range(0, partialAir.Length).All(i => partialAir[i].A == 0
                || game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                $"Earth {id} ambient effects never reveal unexplored tiles");
            var seen = Enumerable.Range(0, Map.TileCount).Select(i => game.Visibility.Contains(new(i / Map.Height, i % Map.Height))).ToArray();
            Layer(() => drawMap.Invoke(world, [batch, game.Map, seen, 1d])); Save($"map-{id}-partial.png");
            for (var y = 0; y < Map.Height; y++)
                for (var x = 0; x < Map.Width; x++) game.Visibility.RevealFrom(game.Map, new(x, y));
            var first = Frame(1); var repeatFrame = Frame(1); var later = Frame(1.75);
            check(first.SequenceEqual(repeatFrame) && !first.SequenceEqual(later),
                $"Earth {id} animation freezes at a fixed clock and advances with scene time");
            Frame(1); Save($"map-{id}.png");
            var mask = (bool[])((bool[])type.GetField("waterMask", flags)!.GetValue(world)!).Clone();
            var wet = Enumerable.Range(0, mask.Length).Where(i => mask[i]).ToArray();
            var wetTiles = wet.Select(i => new MapPosition(i % 416 / 32, i / 416 / 32)).Distinct().ToArray();
            check(wet.Length is > 500 and < 2500 && wetTiles.All(p => game.Map.Tile(p) is 0x27 or 0x47
                && game.Map.Move(new(p.X - 1, p.Y), 1, 0).Moved),
                $"Earth {id} small puddles cover only ordinary walkable floor and need no water equipment");
            check(wetTiles.All(p => !game.Map.Events.Chests.Contains(p)
                && !game.Map.Events.Depth.Any(d => d.HasPosition && d.Position == p)
                && (!game.Map.Events.Princess.HasPosition || game.Map.Events.Princess.Position != p)),
                $"Earth {id} puddles leave treasure, stairs and rescue positions dry");
            var water = (Texture2D)type.GetField("water", flags)!.GetValue(world)!;
            var waterPixels = new Color[mask.Length]; water.GetData(waterPixels);
            check(wet.All(i => waterPixels[i].R > waterPixels[i].G && waterPixels[i].G > waterPixels[i].B)
                && wet.Select(i => waterPixels[i]).Distinct().Count() is >= 2 and <= 5,
                $"Earth {id} still water uses a small warm palette with discrete shades");
            var waterFirst = WaterFrame(1); var waterLater = WaterFrame(2);
            check(waterFirst.SequenceEqual(WaterFrame(1)) && !waterFirst.SequenceEqual(waterLater)
                && Enumerable.Range(0, mask.Length).All(i => waterFirst[i] == waterLater[i] || mask[i]),
                $"Earth {id} puddle ripples freeze and animate strictly inside water");
            MapPosition[] poolCenters = id switch
            {
                57 => [new(3, 4), new(8, 8)], 63 => [new(5, 5), new(9, 3)],
                69 => [new(8, 6), new(4, 2)], 75 => [new(7, 6), new(8, 2)],
                103 => [new(6, 5), new(8, 8)], 109 => [new(7, 5), new(7, 1)],
                _ => [new(4, 8), new(8, 2)],
            };
            foreach (var center in poolCenters)
            {
                var actor = new PlayerMotion(center);
                var reflected = Reflection(actor, 1);
                var ink = Enumerable.Range(0, reflected.Length).Where(i => reflected[i].A > 0).ToArray();
                check(ink.Length > 20 && ink.All(i => mask[i] && reflected[i].A < 80
                    && game.Visibility.Contains(new(i % 416 / 32, i / 416 / 32))),
                    $"Earth {id} puddle {center.X},{center.Y} reflects the character faintly inside visible water");
                check(reflected.SequenceEqual(Reflection(actor, 1)) && !reflected.SequenceEqual(Reflection(actor, 2)),
                    $"Earth {id} puddle {center.X},{center.Y} reflection freezes and ripples with scene time");
            }
            Layer(() => draw.Invoke(world, [batch, game, new PlayerMotion(poolCenters[0]), town, 1d, false]));
            Save($"puddle-{id}.png");
            var hidden = new Adventure(new GameSession(catalog, location: new()
            { MapId = id, MapLayer = layer, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = new(1, 1) }));
            // A sealed visibility fixture keeps both puddles unexplored.
            hidden.Visibility.Initialize(new Map(id, 1, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(0, 0));
            check(Layer(() => reflection.Invoke(town, [batch, new PlayerMotion(poolCenters[0]), 1d, hidden, world])).All(p => p.A == 0),
                $"Earth {id} reflection cannot reveal an unexplored puddle");
            var changedCell = poolCenters[0]; var changedTile = game.Map.Tile(changedCell);
            game.Map.RestoreTile(changedCell, 0x22); prepareWater.Invoke(world, [game.Map]);
            var clipped = (bool[])type.GetField("waterMask", flags)!.GetValue(world)!;
            check((from py in Enumerable.Range(changedCell.Y * 32, 32) from px in Enumerable.Range(changedCell.X * 32, 32)
                select clipped[py * 416 + px]).All(v => !v), $"Earth {id} a new wall removes cached puddle water immediately");
            game.Map.RestoreTile(changedCell, changedTile); prepareWater.Invoke(world, [game.Map]);
            check(mask.SequenceEqual((bool[])type.GetField("waterMask", flags)!.GetValue(world)!),
                $"Earth {id} restoring the floor restores the exact puddle shoreline");
            if (id == 57)
            {
                var actor = new PlayerMotion(new(3, 3)); var dry = Reflection(actor, 1);
                actor.MoveTo(new(3, 4));
                check(dry.All(p => p.A == 0) && dry.SequenceEqual(Reflection(actor, 1)),
                    "Accepting a step toward an Earth puddle does not pop the reflection ahead of the character");
                var strengths = new List<int>();
                for (var frame = 0; frame < 30; frame++)
                { actor.Update(PlayerMotion.StepDuration / 30); strengths.Add(Reflection(actor, 1).Max(p => (int)p.A)); }
                check(strengths.Max() > 30 && strengths.Any(s => s > 0 && s < strengths.Max() / 2),
                    "Walking into an Earth puddle fades the reflection through intermediate strengths");
                actor.SnapTo(new(3, 6));
                check(Reflection(actor, 1).All(p => p.A == 0), "The Earth reflection disappears after leaving the puddle");
            }
            var air = Layer(() => atmosphere.Invoke(world, [batch, game, 1d]));
            check(air.Count(p => p.A > 0) is > 0 and < 5000, $"Earth {id} ambient animation stays sparse");
            var floors = (from y in Enumerable.Range(0, Map.Height) from x in Enumerable.Range(0, Map.Width)
                where game.Map.Tile(new(x, y)) == 0x27
                select string.Join(',', from py in Enumerable.Range(0, 32) from px in Enumerable.Range(0, 32)
                    select original[(y * 32 + py) * 416 + x * 32 + px].PackedValue)).ToArray();
            check(floors.Distinct().Count() > floors.Length * .9, $"Earth {id} open terrain has distinct patches rather than repeated tiles");
            var secret = new List<(MapPosition Position, ushort Tile)>();
            for (var y = 0; y < Map.Height; y++)
                for (var x = 0; x < Map.Width; x++)
                {
                    var position = new MapPosition(x, y); var tile = game.Map.Tile(position);
                    if (tile is not (0x43 or 0x47)) continue;
                    secret.Add((position, tile)); game.Map.RestoreTile(position, tile == 0x43 ? (ushort)0x22 : (ushort)0x27);
                }
            prepare.Invoke(world, [game.Map]); prepareWater.Invoke(world, [game.Map]);
            check(original.SequenceEqual(Ground()) && mask.SequenceEqual((bool[])type.GetField("waterMask", flags)!.GetValue(world)!)
                && air.SequenceEqual(Layer(() => atmosphere.Invoke(world, [batch, game, 1d]))),
                $"Earth {id} secret terrain and ambient details are visually identical to ordinary terrain");
            foreach (var entry in secret) game.Map.RestoreTile(entry.Position, entry.Tile);
            prepare.Invoke(world, [game.Map]);
            var wall = new MapPosition(0, 0);
            game.Map.RestoreTile(wall, 0x27); prepare.Invoke(world, [game.Map]);
            check(!original.SequenceEqual(Ground()), $"Earth {id} changed terrain invalidates the cached formation");
            game.Map.RestoreTile(wall, 0x22); prepare.Invoke(world, [game.Map]);
            check(original.SequenceEqual(Ground()) && GameSaveCodec.Encode(session, catalog).SequenceEqual(snapshot),
                $"Earth {id} rendering and cache restoration preserve the full gameplay snapshot and RNG");
            if (id == 109)
                for (var frame = 0; frame < 40; frame++) { Frame(frame / 8d); Save($"damp-frame-{frame:D2}.png"); }

            Color[] Frame(double time) => Layer(() => draw.Invoke(world,
                [batch, game, new PlayerMotion(new MapPosition(1, 1)), town, time, false]));
            Color[] Reflection(PlayerMotion actor, double time) => Layer(() => reflection.Invoke(town, [batch, actor, time, game, world]));
            Color[] WaterFrame(double time) => Layer(() =>
            {
                for (var y = 0; y < Map.Height; y++)
                    for (var x = 0; x < Map.Width; x++) drawWater.Invoke(world, [batch, new Rectangle(x * 32, y * 32, 32, 32), time]);
            });
        }
        foreach (var id in new[] { 17, 41, 117 })
        {
            var game = new Adventure(new GameSession(catalog, location: new() { MapId = id, Position = new(1, 1) }));
            check(Layer(() => atmosphere.Invoke(world, [batch, game, 1d])).All(p => p.A == 0),
                $"Earth effects are absent from other biome {id}");
        }
        Color[] Ground() { var data = new Color[416 * 384]; texture.GetData(data); return data; }
        Color[] Layer(Action render)
        {
            device.SetRenderTarget(target); device.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); render(); batch.End(); device.SetRenderTarget(null);
            var data = new Color[416 * 384]; target.GetData(data); return data;
        }
        void Save(string name)
        {
            using var file = File.Create(Path.Combine(directory, name)); target.SaveAsPng(file, 416, 384);
        }
    }
}
