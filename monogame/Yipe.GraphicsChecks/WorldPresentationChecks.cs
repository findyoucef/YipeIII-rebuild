using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class WorldPresentationChecks
{
    public static void Run(GraphicsDevice device, object renderer, object town, MapCatalog catalog,
        string output, Action<bool, string> check)
    {
        var type = renderer.GetType();
        var draw = type.GetMethod("Draw")!;
        var ground = type.GetMethod("DrawGround", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var prepareWater = type.GetMethod("PrepareWater", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var prepareDesert = type.GetMethod("PrepareDesert", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var mountains = type.GetField("mountains", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        var prepareMountains = mountains.GetType().GetMethod("Prepare", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var reflection = town.GetType().GetMethod("DrawWorldReflection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var heroes = (Texture2D[])type.GetField("heroes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        var artType = type.Assembly.GetType("Yipe.Desktop.RetroArt", true)!;
        var palette = (Color[])artType.GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        for (var sheet = 0; sheet < heroes.Length; sheet++)
        {
            var atlas = heroes[sheet]; var pixels = new Color[atlas.Width * atlas.Height]; atlas.GetData(pixels);
            check(pixels.All(p => p.A == 0 || p.A == 255 && palette.Contains(p)), $"Character atlas {sheet + 1} uses native palette colors and binary alpha");
            using (var file = File.Create(Path.Combine(output, $"native-heroes-{sheet + 1}.png"))) atlas.SaveAsPng(file, atlas.Width, atlas.Height);
            var cells = atlas.Height / 32 * 4;
            for (var cell = 0; cell < cells; cell++)
            {
                var left = cell % 4 * 32; var top = cell / 4 * 32;
                var opaque = (from y in Enumerable.Range(0, 32) from x in Enumerable.Range(0, 32)
                    where pixels[(top + y) * atlas.Width + left + x].A > 0 select new Point(x, y)).ToArray();
                check(opaque.Length > 40 && opaque.Max(p => p.Y) == 27 && opaque.All(p => p.X > 0 && p.X < 31),
                    $"Character sheet {sheet + 1} pose {cell + 1} is complete and aligned at the shared foot anchor");
            }
            for (var character = 0; character < atlas.Height / 64; character++)
            {
                var first = pixels.AsSpan(character * 64 * atlas.Width, 32 * atlas.Width);
                var second = pixels.AsSpan((character * 64 + 32) * atlas.Width, 32 * atlas.Width);
                check(!first.SequenceEqual(second), $"Character sheet {sheet + 1} appearance {character + 1} has distinct walking poses");
            }
        }
        using var target = new RenderTarget2D(device, 416, 384);
        using var batch = new SpriteBatch(device);
        var directory = Path.Combine(output, "maps"); Directory.CreateDirectory(directory);
        var drawMonster = type.GetMethod("DrawWorldMonster", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var monsterAtlases = (Texture2D[])type.GetField("monsters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        for (var monster = 1; monster <= 44; monster++)
        {
            var atlas = monsterAtlases[(monster - 1) / 16];
            var source = new Color[atlas.Width * atlas.Height]; atlas.GetData(source);
            var frame = (monster - 1) % 16;
            var ink = (from y in Enumerable.Range(frame / 4 * 48, 48) from x in Enumerable.Range(frame % 4 * 48, 48)
                where source[y * atlas.Width + x].A == 255 select 1).Count();
            foreach (var scale in monster is >= 35 and <= 40 ? new[] { 1, 2 } : [1])
            {
                device.SetRenderTarget(target); device.Clear(Color.Transparent);
                batch.Begin(samplerState: SamplerState.PointClamp);
                drawMonster.Invoke(renderer, [batch, monster, new MapPosition(5, 7), scale]);
                batch.End(); device.SetRenderTarget(null);
                var pixels = new Color[416 * 384]; target.GetData(pixels);
                var opaque = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].A == 255).ToArray();
                var shadow = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].A is > 0 and < 255).ToArray();
                check(opaque.Length == ink * scale * scale && opaque.Max(i => i / 416) == (7 + scale) * 32 - 1
                    && shadow.All(i => pixels[i].A <= 57 && i % 416 >= 5 * 32 && i % 416 < (5 + scale) * 32
                        && i / 416 >= 7 * 32 && i / 416 < (7 + scale) * 32),
                    $"World monster {monster}, scale {scale}: the complete sprite meets the tile base, with restrained shadow inside its footprint");
                if (monster is 7 or 9)
                    check(shadow.Length > 0, $"Surface D4 monster {monster} has visible contact shading beneath its feet");
            }
        }
        var tiles = new SortedSet<ushort>();
        foreach (var id in catalog.Ids.Concat([118, 119, 120]).Order())
        {
            var session = new GameSession(catalog, location: new WorldLocation { MapId = id, Position = new(1, 1) });
            var adventure = new Adventure(session);
            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++)
                {
                    adventure.Visibility.RevealFrom(adventure.Map, new(x, y));
                    tiles.Add(adventure.Map.Tile(new(x, y)));
                }
            foreach (var link in adventure.Map.Events.Linked) if (link.Value >= 0) tiles.Add((ushort)link.Value);
            var random = session.Random.State;
            device.SetRenderTarget(target); device.Clear(Color.Black);
            batch.Begin(samplerState: SamplerState.PointClamp);
            draw.Invoke(renderer, [batch, adventure, new PlayerMotion(new MapPosition(1, 1)), town, 1d, false]);
            batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels);
            check(pixels.Count(p => p != Color.Black) > 416 * 384 / 2 && session.Random.State == random,
                $"Map {id} renders its revealed terrain, events and actors without consuming gameplay RNG");
            using var image = File.Create(Path.Combine(directory, $"map-{id:D3}.png")); target.SaveAsPng(image, target.Width, target.Height);
            var bridges = (from y in Enumerable.Range(0, Map.Height) from x in Enumerable.Range(0, Map.Width)
                where adventure.Map.Tile(new(x, y)) == 11 select new MapPosition(x, y)).ToArray();
            if (bridges.Length == 0) continue;
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            for (var y = 0; y < Map.Height; y++)
                for (var x = 0; x < Map.Width; x++) ground.Invoke(renderer, [batch, adventure.Map, new MapPosition(x, y), 1d]);
            batch.End(); device.SetRenderTarget(null); target.GetData(pixels);
            foreach (var bridge in bridges)
            {
                var actor = new PlayerMotion(bridge);
                device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
                reflection.Invoke(town, [batch, actor, 1d, adventure, renderer]); batch.End(); device.SetRenderTarget(null);
                var reflected = new Color[pixels.Length]; target.GetData(reflected);
                var ink = Enumerable.Range(0, reflected.Length).Where(i => reflected[i].A > 0).ToArray();
                var mask = (bool[])type.GetField("waterMask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
                check(ink.Length > 0 && ink.All(i => reflected[i].A < 80 && mask[i]),
                    $"Map {id} bridge {bridge.X},{bridge.Y} reflects the character faintly on visible water only");
                using var file = File.Create(Path.Combine(directory, $"reflection-{id:D3}-{bridge.X}-{bridge.Y}.png"));
                target.SaveAsPng(file, target.Width, target.Height);
            }
        }
        // Persistent/timed variants which need not appear in the initial templates.
        tiles.UnionWith([0x2a, 0x2b, 0x3c, 0x3d, 0x52, 0x54, 0x5d, 0x61, 0x62, 0x71]);
        var map = new Map(1, 0, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        Color[] Tile(ushort tile)
        {
            map.RestoreTile(new(1, 1), tile);
            prepareWater.Invoke(renderer, [map]);
            prepareDesert.Invoke(renderer, [map]);
            prepareMountains.Invoke(mountains, [map.Id, (Func<MapPosition, ushort>)map.Tile]);
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            ground.Invoke(renderer, [batch, map, new MapPosition(1, 1), 1d]); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        foreach (var tile in tiles) check(Tile(tile).Count(p => p.A > 0) >= 1024, $"Tile {tile:x2} has explicit ground artwork");
        foreach (var pair in new (ushort Ordinary, ushort Secret)[] { (2, 0x46), (3, 0x45), (0x22, 0x43), (0x23, 0x44), (0x25, 0x5a), (0x27, 0x47), (0x28, 0x48), (0x2d, 0x42) })
            check(Tile(pair.Ordinary).SequenceEqual(Tile(pair.Secret)), $"Hidden tile {pair.Secret:x2} preserves ordinary tile {pair.Ordinary:x2} appearance");
        map.RestoreTile(new(1, 1), 0x0c); map.RestoreTile(new(2, 1), 0x0c);
        prepareWater.Invoke(renderer, [map]);
        var water = (Texture2D)type.GetField("water", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        var texture = new Color[water.Width * water.Height]; water.GetData(texture);
        var pattern = (Color[])type.GetField("waterPattern", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        check(Enumerable.Range(40, 16).All(y => texture[y * water.Width + 63] == pattern[y % 32 * 32 + 31]
            && texture[y * water.Width + 64] == pattern[y % 32 * 32]),
            "Adjacent lake tiles share uninterrupted village-textured water without an internal bank");
        var poolPixel = texture[48 * water.Width + 48];
        map.RestoreTile(new(1, 1), 2); prepareWater.Invoke(renderer, [map]); water.GetData(texture);
        check(poolPixel.A == 255 && texture[48 * water.Width + 48].A == 0,
            "A changed map tile invalidates cached water without leaving a stale pool");
        var frozen = (Color[])texture.Clone(); prepareWater.Invoke(renderer, [map]); water.GetData(texture);
        check(frozen.SequenceEqual(texture), "Rebuilding or redrawing unchanged shorelines is deterministic");
        // The same scene time must freeze every wave; time changes only pixels inside water.
        Color[] WaterFrame(double time)
        {
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            var render = type.GetMethod("DrawWater", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var y = 0; y < Map.Height; y++)
                for (var x = 0; x < Map.Width; x++) render.Invoke(renderer, [batch, new Rectangle(x * 32, y * 32, 32, 32), time]);
            batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        var still = WaterFrame(1); var repeat = WaterFrame(1); var moving = WaterFrame(2);
        var waterMask = (bool[])type.GetField("waterMask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        var differences = Enumerable.Range(0, still.Length).Where(i => still[i] != moving[i]).ToArray();
        check(still.SequenceEqual(repeat) && differences.Length > 0 && differences.All(i => waterMask[i]),
            "World waves freeze with scene time and animate strictly inside the shared water mask");
        // Desert weather uses the same clock as water, but never gameplay RNG.
        var windMethod = type.GetMethod("DrawDesertWind", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sandLayer = (Texture2D)type.GetField("sandLayer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        Color[] SandPixels() { var pixels = new Color[416 * 384]; sandLayer.GetData(pixels); return pixels; }
        Color[] WindFrame(Adventure adventure, double time)
        {
            prepareDesert.Invoke(renderer, [adventure.Map]);
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            windMethod.Invoke(renderer, [batch, adventure, time]); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        var windGame = new Adventure(new GameSession(catalog, location: new() { MapId = 1, RegionX = -4, RegionY = 2, Position = new(6, 5) }));
        var beforeWind = GameSaveCodec.Encode(windGame.Session, catalog);
        var windFirst = WindFrame(windGame, 1); var windRepeat = WindFrame(windGame, 1); var windLater = WindFrame(windGame, 3);
        check(windFirst.SequenceEqual(windRepeat), "Paused desert dust and tumbleweeds stay pixel-identical at a fixed scene time");
        check(!windFirst.SequenceEqual(windLater) && windFirst.Count(p => p.A > 0 && p.A < 100) > 20,
            "Desert wind drifts over time with restrained translucent dust");
        var sandClear = (bool[])type.GetField("sandClear", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        check(Enumerable.Range(0, windFirst.Length).Where(i => windFirst[i].A > 0 || windLater[i].A > 0).All(i => sandClear[i]),
            "Dust, tumbleweeds and their shadows remain on clear sand, away from plants and obstacles");
        var weedFirst = Enumerable.Range(0, windFirst.Length).Where(i => windFirst[i].A == 255).ToArray();
        var weedLater = Enumerable.Range(0, windLater.Length).Where(i => windLater[i].A == 255).ToArray();
        check(weedFirst.Length > 10 && weedLater.Length > 10 && !weedFirst.SequenceEqual(weedLater),
            "Visible tumbleweed silhouettes roll and move across the desert");
        check(GameSaveCodec.Encode(windGame.Session, catalog).SequenceEqual(beforeWind), "Desert effects leave the complete gameplay snapshot and RNG unchanged");
        var stationarySand = SandPixels(); WindFrame(windGame, 20);
        check(SandPixels().SequenceEqual(stationarySand), "Wind animation cannot flicker or rebuild the static sand boundary");
        var villageWind = WindFrame(new Adventure(new GameSession(catalog)), 1);
        var underground = new Adventure(new GameSession(catalog, location: new() { MapId = 41, MapLayer = 1, RegionX = -4, RegionY = 2 }));
        underground.Map.RestoreTile(new(5, 5), 0x2d);
        check(villageWind.All(p => p.A == 0) && WindFrame(underground, 1).All(p => p.A == 0),
            "Desert weather stays out of the village and underground rooms");
        // Exercise the cache through a real ground draw after changing the same map instance.
        var grassBefore = Tile(2); var sandBefore = Tile(0x2d); var grassAfter = Tile(2);
        check(!sandBefore.SequenceEqual(grassBefore) && grassBefore.SequenceEqual(grassAfter),
            "Changing a desert tile restores its grass without stale sand or weather");
        var floraMethod = type.GetMethod("DrawFlora", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Color[] PalmFrame(double time)
        {
            device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            floraMethod.Invoke(renderer, [batch, 0, new MapPosition(4, 4), time]); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        var palmFirst = PalmFrame(0); var palmLater = PalmFrame(2);
        var roots = Enumerable.Range((4 * 32 - 15 + 32) * 416, 16 * 416).ToArray();
        check(!palmFirst.SequenceEqual(palmLater) && roots.Any(i => palmFirst[i].A > 0)
            && roots.All(i => palmFirst[i] == palmLater[i]), "Desert palm crowns sway while trunks and roots remain anchored");
        // Retain a short native-resolution animation for visual review of movement and density.
        var windDirectory = Path.Combine(output, "desert-wind"); Directory.CreateDirectory(windDirectory);
        for (var frame = 0; frame < 96; frame++)
        {
            device.SetRenderTarget(target); device.Clear(Color.Black); batch.Begin(samplerState: SamplerState.PointClamp);
            draw.Invoke(renderer, [batch, windGame, new PlayerMotion(new MapPosition(6, 5)), town, frame / 12d, false]);
            batch.End(); device.SetRenderTarget(null);
            using var file = File.Create(Path.Combine(windDirectory, $"frame-{frame:D3}.png"));
            target.SaveAsPng(file, target.Width, target.Height);
        }
        File.WriteAllText(Path.Combine(directory, "tile-coverage.txt"), string.Join(" ", tiles.Select(t => t.ToString("x2"))) + "\n");
    }
}
