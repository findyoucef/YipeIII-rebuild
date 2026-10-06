using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class MountainPresentationChecks
{
    public static void Run(GraphicsDevice device, object world, object town, MapCatalog catalog,
        string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        const int width = 416, height = 384;
        var type = world.GetType();
        var mountains = type.GetField("mountains", flags)!.GetValue(world)!;
        var prepare = mountains.GetType().GetMethod("Prepare", flags)!;
        var texture = (Texture2D)mountains.GetType().GetProperty("Texture", flags)!.GetValue(mountains)!;
        var palette = (Color[])type.Assembly.GetType("Yipe.Desktop.RetroArt")!
            .GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var drawMap = type.GetMethod("DrawMap", flags)!;
        var directory = Path.Combine(output, "mountains"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(device, width, height);
        using var batch = new SpriteBatch(device);
        var rendered = new Dictionary<int, Color[]>();
        var maps = Enumerable.Range(1, 36).ToDictionary(id => id, id => catalog.CreateMap(id)!);
        double Light(Color c) => c.R * .2126 + c.G * .7152 + c.B * .0722;

        var outdoors = (Texture2D)type.GetField("outdoors", flags)!.GetValue(world)!;
        var rock = new Color[32 * 32];
        outdoors.GetData(0, new Rectangle(32, 32, 32, 32), rock, 0, rock.Length);
        var buildings = (Texture2D)type.GetField("buildings", flags)!.GetValue(world)!;
        var cave = new Color[48 * 48];
        buildings.GetData(0, new Rectangle(0, 96, 48, 48), cave, 0, cave.Length);
        var townCave = new Color[cave.Length];
        ((Texture2D)town.GetType().GetField("town", flags)!.GetValue(town)!)
            .GetData(0, new Rectangle(0, 96, 48, 48), townCave, 0, townCave.Length);
        check(rock.All(c => c == palette[2] || c == palette[3] || c == palette[4])
            && rock.Average(Light) < 105 && rock.Count(c => c == palette[4]) is > 20 and < 110,
            "Mountain stone uses dark slate with sparse authored highlights, without green-gray fill");
        check(Math.Abs(rock.Average(Light) - cave.Where(c => c == palette[2] || c == palette[3] || c == palette[4]).Average(Light)) < 10,
            "Mountain stone and cave rim have matching average slate brightness");
        check(cave.SequenceEqual(townCave), "Village and world cave entrances share the same softened stone rim");

        var rimFixture = Enumerable.Repeat(palette[20], 144 * 144).ToArray();
        for (var y = 0; y < 48; y++)
            for (var x = 0; x < 48; x++)
                rimFixture[(96 + y) * 144 + x] = x is >= 10 and < 38 && y is >= 12 and < 44
                    ? x is >= 18 and < 30 && y >= 22 ? palette[0]
                    : y is 12 or 13 || x is 10 or 11 or 36 or 37 || y >= 42 ? palette[1] : palette[3]
                    : Color.Transparent;
        var originalRim = (Color[])rimFixture.Clone();
        type.Assembly.GetType("Yipe.Desktop.RetroArt")!.GetMethod("SoftenCaveRim", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [rimFixture, 144, 48]);
        check(Enumerable.Range(0, rimFixture.Length).All(i => rimFixture[i] == originalRim[i]
            || i / 144 is >= 96 and < 138 && i % 144 < 48 && originalRim[i] == palette[1] && rimFixture[i] == palette[2]),
            "Cave rim treatment only lifts exterior shadows; sprite silhouettes and other atlas cells stay intact");
        check((from y in Enumerable.Range(22, 20) from x in Enumerable.Range(18, 12)
            select rimFixture[(96 + y) * 144 + x]).All(c => c == palette[0])
            && rimFixture[(96 + 12) * 144 + 24] == palette[2],
            "Cave roof joins the slate wall while the recessed tunnel stays dark");

        Color[] Prepare(Map map)
        {
            prepare.Invoke(mountains, [map.Id, (Func<MapPosition, ushort>)map.Tile]);
            var pixels = new Color[width * height]; texture.GetData(pixels); return pixels;
        }
        static bool Rock(ushort tile) => tile is 3 or 0x39 or 0x45;
        foreach (var (id, map) in maps)
        {
            var pixels = Prepare(map); rendered[id] = pixels;
            check(pixels.All(p => p.A == 0 || p.A == 255 && palette.Contains(p)),
                $"Surface {id} mountains keep native palette colors and binary pixel edges");
            check(Enumerable.Range(0, pixels.Length).All(i => Rock(map.Tile(new(i % width / 32, i / width / 32)))
                || pixels[i].A == 0), $"Surface {id} mountains leave every non-mountain cell unobstructed");
            var centers = from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width)
                where Rock(map.Tile(new(x / 32, y / 32))) && x % 32 is >= 12 and < 20 && y % 32 is >= 12 and < 20
                select pixels[y * width + x];
            check(centers.All(p => p.A == 255), $"Surface {id} all blocked mountain cells retain a solid visible core");
            if (id is not (1 or 6 or 17 or 18 or 22 or 23 or 24)) continue;
            check(pixels.SequenceEqual(Prepare(map)), $"Surface {id} cached mountains remain deterministic");
            using (var file = File.Create(Path.Combine(directory, $"rock-{id:D3}.png"))) texture.SaveAsPng(file, width, height);
            var session = new GameSession(catalog, location: new()
            { MapId = id, RegionX = (id - 1) % 6 - 4, RegionY = 2 - (id - 1) / 6, Position = new(1, 1) });
            var snapshot = GameSaveCodec.Encode(session, catalog);
            device.SetRenderTarget(target); device.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp);
            drawMap.Invoke(world, [batch, session.CurrentMap, Enumerable.Repeat(true, Map.TileCount).ToArray(), 1d]);
            batch.End(); device.SetRenderTarget(null);
            using (var file = File.Create(Path.Combine(directory, $"area-{id:D3}.png"))) target.SaveAsPng(file, width, height);
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(session, catalog)),
                $"Surface {id} mountain/map rendering preserves the complete gameplay save and RNG");
        }

        var townMountains = town.GetType().GetField("mountains", flags)!.GetValue(town)!;
        prepare.Invoke(townMountains, [17, (Func<MapPosition, ushort>)maps[17].Tile]);
        var townTexture = (Texture2D)mountains.GetType().GetProperty("Texture", flags)!.GetValue(townMountains)!;
        var townPixels = new Color[width * height]; townTexture.GetData(townPixels);
        check(townPixels.SequenceEqual(rendered[17]), "Maynard gameplay and area maps share identical mountain and cave backing artwork");

        var comparisons = 0; var matched = true;
        foreach (var (id, map) in maps)
            foreach (var horizontal in new[] { true, false })
            {
                var column = (id - 1) % 6; var row = (id - 1) / 6;
                var nextId = horizontal ? row * 6 + (column + 1) % 6 + 1 : (row + 1) % 6 * 6 + column + 1;
                var next = maps[nextId];
                var span = horizontal ? Map.Height : Map.Width;
                var stride = horizontal ? Map.Width - 2 : Map.Height - 2;
                MapPosition Cell(int across, int along) => horizontal ? new(across, along) : new(along, across);
                for (var along = 1; along < span - 1; along++)
                {
                    // Authored overlap cells must agree before their artwork can agree.
                    if (!Enumerable.Range(along - 1, 3).All(a => Enumerable.Range(0, 2)
                        .All(b => map.Tile(Cell(stride + b, a)) == next.Tile(Cell(b, a))))) continue;
                    for (var across = 0; across < 2; across++)
                    {
                        if (!Rock(map.Tile(Cell(stride + across, along)))) continue;
                        for (var py = 0; py < 32; py++)
                            for (var px = 0; px < 32; px++)
                            {
                                var from = Cell((stride + across) * 32 + px, along * 32 + py);
                                var to = Cell(across * 32 + px, along * 32 + py);
                                matched &= rendered[id][from.Y * width + from.X] == rendered[nextId][to.Y * width + to.X];
                                comparisons++;
                            }
                    }
                }
            }
        check(comparisons > 10000 && matched,
            $"All {comparisons} matching mountain overlap pixels join across region edges and world wrap");

        var diagonal = rendered[18];
        var ridgeEdges = Enumerable.Range(192, 32).Select(y => Enumerable.Range(64, 160).First(x =>
            diagonal[y * width + x] == palette[2] || diagonal[y * width + x] == palette[3]
            || diagonal[y * width + x] == palette[4] || diagonal[y * width + x] == palette[39])).ToArray();
        check(ridgeEdges.Distinct().Count() >= 12 && ridgeEdges.Max() - ridgeEdges.Min() >= 24,
            "Surface F3 beside 2,6 has a long diagonal contour instead of a lightly rounded square step");

        var fixture = new Map(17, 0, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        for (var y = 2; y <= 8; y++)
            for (var x = 3; x <= 6; x++) fixture.RestoreTile(new(x, y), 3);
        var block = Prepare(fixture);
        var topLight = (from y in Enumerable.Range(240, 8) from x in Enumerable.Range(144, 48)
            select Light(block[y * width + x])).Average();
        var faceLight = (from y in Enumerable.Range(274, 7) from x in Enumerable.Range(144, 48)
            select Light(block[y * width + x])).Average();
        check(topLight - faceLight >= 18, "Mountain tops remain visibly lighter than the broad front cliff faces");
        check(block.All(p => p.A == 0 || p != palette[0] && p != palette[1]),
            "Exposed mountain rock and ground contacts never acquire an artificial near-black outline");
        var contacts = (from y in Enumerable.Range(9 * 32 - 7, 7)
            from x in Enumerable.Range(4 * 32, 2 * 32) select y * width + x).ToArray();
        check(contacts.Any(i => block[i] == palette[11]) && contacts.Any(i => block[i] == palette[10])
            && contacts.Any(i => block[i] == palette[3] || block[i] == palette[39]),
            "Meadow grass and moss overlap broken parts of the rock foot, leaving visible stone between them");
        check(Enumerable.Range(3 * 32, 5 * 32).All(y => Enumerable.Range(4 * 32 - 4, 8)
            .All(x => block[y * width + x].A == 255 && block[y * width + x] != palette[1])),
            "Adjacent mountain tiles form continuous rock without internal outlines or transparent seams");
        var leftEdges = Enumerable.Range(3 * 32, 5 * 32).Select(y => Enumerable.Range(3 * 32, 16)
            .First(x => block[y * width + x].A == 255)).Distinct().Count();
        check(leftEdges >= 3 && block[2 * 32 * width + 3 * 32].A == 0,
            "Exposed mountain borders have uneven pixel ledges and cut-back corners");
        fixture.RestoreTile(new(4, 4), 0x45);
        check(block.SequenceEqual(Prepare(fixture)), "Secret mountain passages do not change contours, texture or decoration");
        fixture.RestoreTile(new(4, 4), 2);
        check(Prepare(fixture)[144 * width + 144].A == 0, "Removing mountain terrain invalidates its cached silhouette");
        fixture.RestoreTile(new(4, 4), 3);
        check(block.SequenceEqual(Prepare(fixture)), "Restoring terrain reconstructs the same mountain without cache residue");

        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (!Rock(fixture.Tile(new(x, y)))) fixture.RestoreTile(new(x, y), 0x2d);
        var dry = Prepare(fixture);
        check(dry[2 * 32 * width + 3 * 32].A == 255 && dry[2 * 32 * width + 3 * 32].R > dry[2 * 32 * width + 3 * 32].B,
            "Desert cut-back mountain corners expose warm sand instead of green meadow");
        check(contacts.Where(i => block[i] == palette[11]).All(i => dry[i].R > dry[i].G && dry[i].G > dry[i].B)
            && contacts.All(i => dry[i] != palette[10] && dry[i] != palette[11]),
            "Desert rock feet use overlapping sand instead of meadow-green patches");
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (!Rock(fixture.Tile(new(x, y)))) fixture.RestoreTile(new(x, y), 8);
        var paved = Prepare(fixture);
        check(contacts.All(i => paved[i] != palette[10] && paved[i] != palette[11]),
            "Rock beside a paved approach does not grow a meadow fringe across it");
        var skirt = new Map(17, 0, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        skirt.RestoreTile(new(4, 4), 3); skirt.RestoreTile(new(5, 4), 0x67);
        var feet = Prepare(skirt);
        check(Enumerable.Range(138, 12).All(y => feet[y * width + 159].A == 255 && feet[y * width + 159] != palette[1])
            && skirt.Move(new(5, 5), 0, -1).Moved, "Mountain rock meets its authored walkable foothill without a grass seam");

        var seen = new bool[Map.TileCount]; seen[1 * Map.Height + 1] = true;
        device.SetRenderTarget(target); device.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
        drawMap.Invoke(world, [batch, maps[17], seen, 1d]); batch.End(); device.SetRenderTarget(null);
        var fog = new Color[width * height]; target.GetData(fog);
        check(Enumerable.Range(0, fog.Length).All(i => seen[i % width / 32 * Map.Height + i / width / 32] || fog[i] == palette[0]),
            "Connected mountain formations and cave sprites cannot expose unseen area-map cells");
    }
}
