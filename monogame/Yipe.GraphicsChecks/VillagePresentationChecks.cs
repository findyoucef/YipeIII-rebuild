using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class VillagePresentationChecks
{
    internal static void Run(GraphicsDevice device, object world, object town, MapCatalog catalog,
        string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = world.GetType();
        var paths = type.GetMethod("VillagePathConnections", BindingFlags.Static | BindingFlags.NonPublic)!;
        var draw = type.GetMethod("Draw")!;
        var drawMap = type.GetMethod("DrawMap", flags)!;
        var footstep = type.GetMethod("FootstepAt", flags)!;
        var directory = Path.Combine(output, "villages"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(device, 416, 384);
        using var batch = new SpriteBatch(device);
        var steps = new[] { new Point(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
        foreach (var id in new[] { 8, 27 })
        {
            var start = id == 8 ? new MapPosition(7, 6) : new MapPosition(6, 7);
            var session = new GameSession(catalog, location: new()
            {
                MapId = id, MapLayer = 0, RegionX = (id - 1) % 6 - 4,
                RegionY = 2 - (id - 1) / 6, Position = start
            });
            var game = new Adventure(session); var map = game.Map;
            var before = GameSaveCodec.Encode(session, catalog);
            var cells = (from x in Enumerable.Range(0, Map.Width) from y in Enumerable.Range(0, Map.Height)
                select new MapPosition(x, y)).ToArray();
            foreach (var cell in cells) game.Visibility.RevealFrom(map, cell);
            int Road(MapPosition cell) => (int)paths.Invoke(null, [map, cell])!;
            bool Shop(MapPosition cell) => map.Tile(cell) is 1 or 5 or 6 or 7;
            bool Walkable(MapPosition cell) => map.Move(new(cell.X == 0 ? 1 : cell.X - 1, cell.Y), cell.X == 0 ? -1 : 1, 0).Moved;
            check(cells.Where(cell => Road(cell) != 0 && !Shop(cell)).All(Walkable),
                $"Town {id}: paving never advertises a route through blocked palms or buildings");
            var reached = new HashSet<MapPosition> { start }; var queue = new Queue<MapPosition>(); queue.Enqueue(start);
            while (queue.TryDequeue(out var cell))
                foreach (var step in steps)
                {
                    var next = new MapPosition(cell.X + step.X, cell.Y + step.Y);
                    if (Map.Contains(next) && Road(next) != 0 && Walkable(next) && reached.Add(next)) queue.Enqueue(next);
                }
            check(cells.Where(Shop).All(shop => steps.Any(step => reached.Contains(new(shop.X + step.X, shop.Y + step.Y)))),
                $"Town {id}: the connected roads reach a usable approach to every shop");
            var addedPath = id == 8 ? new MapPosition(7, 6) : new MapPosition(3, 6);
            var outside = id == 8 ? new MapPosition(11, 9) : new MapPosition(2, 2);
            check(footstep.Invoke(world, [map, addedPath])!.ToString() == "Stone"
                && footstep.Invoke(world, [map, outside])!.ToString() == (id == 8 ? "Sand" : "Grass"),
                $"Town {id}: new paving gets stone footsteps while adjoining soil keeps its material");
            var seen = Enumerable.Repeat(true, Map.TileCount).ToArray();
            var still = Render(() => drawMap.Invoke(world, [batch, map, seen, 1d, town]));
            check(still.SequenceEqual(Render(() => drawMap.Invoke(world, [batch, map, seen, 1d, town])))
                && !still.SequenceEqual(Render(() => drawMap.Invoke(world, [batch, map, seen, 3d, town]))),
                $"Town {id}: signs, smoke and plants animate with scene time and freeze at a fixed time");
            var knownShop = cells.First(Shop);
            Array.Clear(seen); seen[knownShop.X * Map.Height + knownShop.Y] = true;
            var fog = Render(() => drawMap.Invoke(world, [batch, map, seen, 1d, town]));
            check(Enumerable.Range(0, fog.Length).Where(i => !seen[(i % 416 / 32) * Map.Height + i / 416 / 32])
                .Select(i => fog[i]).Distinct().Count() == 1, $"Town {id}: decorations and building shadows cannot reveal unexplored map cells");
            for (var frame = 0; frame < 32; frame++)
            {
                Render(() => draw.Invoke(world, [batch, game, new PlayerMotion(start), town, frame / 8d, false, 0f]));
                using var file = File.Create(Path.Combine(directory, $"town-{id}-{frame:D2}.png"));
                target.SaveAsPng(file, 416, 384);
            }
            check(before.SequenceEqual(GameSaveCodec.Encode(session, catalog)),
                $"Town {id}: scenery, paths and animation preserve gameplay, quest state and RNG");
        }
        check(((int)paths.Invoke(null, [catalog.CreateMap(27)!, new MapPosition(12, 3)])! & 2) != 0,
            "Ervyn retains its original road exit into the eastern neighboring region");

        Color[] Render(Action draw)
        {
            device.SetRenderTarget(target); device.Clear(Color.Black);
            batch.Begin(samplerState: SamplerState.PointClamp); draw(); batch.End(); device.SetRenderTarget(null);
            var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
    }
}
