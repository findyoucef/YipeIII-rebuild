using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    internal static readonly Color[] WaterCavePalette =
    [
        new(17, 28, 31), new(30, 43, 46), new(44, 59, 60), new(52, 66, 66),
        new(61, 74, 70), new(72, 84, 77), new(35, 55, 58), new(49, 71, 73),
        new(66, 91, 91), new(85, 110, 105), new(105, 128, 116), new(130, 146, 127),
        new(77, 84, 71), new(22, 42, 45), new(30, 88, 88), new(50, 138, 129),
        new(111, 200, 168), new(12, 37, 49), new(17, 52, 65), new(21, 67, 75),
        new(31, 88, 91), new(56, 121, 119), new(94, 159, 141),
    ];
    private readonly Texture2D waterCaveGround;
    private readonly ushort[] waterCaveTiles = new ushort[Map.TileCount];
    private Map? waterCaveMap;
    private bool[] waterCaveRock = [];
    private readonly List<Point> waterCaveAlgae = new(), waterCaveDrips = new();

    internal static bool IsWaterCave(Map map) => map.Id is 44 or 45 or 46 or 50 or 56 or 62 or 68 or 74 or 90 or 96 or 102 or 108 or 114;
    private static bool WaterCaveWall(ushort tile) => tile is 0x25 or 0x5a;
    internal bool[] WaterCaveRock(Map map) { PrepareWaterCave(map); return waterCaveRock; }
    internal Point[] WaterCaveLights(Map map) { PrepareWaterCave(map); return waterCaveAlgae.ToArray(); }

    private void PrepareWaterCave(Map map)
    {
        if (!IsWaterCave(map)) return;
        var changed = !ReferenceEquals(waterCaveMap, map);
        for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
        {
            var i = x * Map.Height + y; var tile = map.Tile(new(x, y));
            changed |= waterCaveTiles[i] != tile; waterCaveTiles[i] = tile;
        }
        if (!changed) return;
        waterCaveMap = map; waterCaveAlgae.Clear(); waterCaveDrips.Clear(); PrepareWater(map);
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * 352; var oy = row * 320;
        var neighbors = new Map?[3, 3]; neighbors[1, 1] = map;
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = catalog.CreateMap(layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1);
            if (next is not null && IsWaterCave(next)) neighbors[dx + 1, dy + 1] = next;
        }
        ushort Tile(int x, int y)
        {
            if (Map.Contains(new(x, y))) return map.Tile(new(x, y));
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0; var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? next.Tile(new(x - dx * 11, y - dy * 10))
                : map.Tile(new(Math.Clamp(x, 0, 12), Math.Clamp(y, 0, 11)));
        }
        bool Wall(int x, int y) => WaterCaveWall(Tile(x, y));
        int[] ledge = [0, 1, 3, 2, 1, 0, 2, 1, 3, 1, 0, 1, 2, 3, 1, 0];
        var rock = waterCaveRock = new bool[WaterWidth * (WaterHeight + 16)];
        for (var y = 0; y < WaterHeight + 16; y++) for (var x = 0; x < WaterWidth; x++) rock[y * WaterWidth + x] = RockAt(x, y);
        bool RockAt(int x, int y)
        {
            var tx = x >> 5; var ty = y >> 5;
            if (!Wall(tx, ty)) return false;
            var edge = 32f;
            for (var cy = ty - 1; cy <= ty + 1; cy++) for (var cx = tx - 1; cx <= tx + 1; cx++)
            {
                if (Wall(cx, cy)) continue;
                var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 16);
                var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 16);
                edge = Math.Min(edge, MathF.Sqrt(dx * dx + dy * dy));
            }
            for (var sy = -1; sy <= 1; sy += 2) for (var sx = -1; sx <= 1; sx += 2)
            {
                if (Wall(tx + sx, ty) || Wall(tx, ty + sy)) continue;
                var dx = Math.Max(0, 8 - (sx < 0 ? (x & 31) + .5f : 31.5f - (x & 31)));
                var dy = Math.Max(0, 8 - (sy < 0 ? (y & 31) + .5f : 31.5f - (y & 31)));
                edge = Math.Min(edge, 8 - dx - dy);
            }
            return edge > 1 + ledge[((x + ox) / 7 + (y + oy) / 9 * 3) & 15];
        }
        bool Rock(int x, int y) => x >= 0 && x < WaterWidth && y >= 0 && y < WaterHeight + 16
            ? rock[y * WaterWidth + x] : RockAt(x, y);
        var data = new Color[WaterWidth * WaterHeight];
        for (var y = 0; y < WaterHeight; y++) for (var x = 0; x < WaterWidth; x++)
        {
            var i = y * WaterWidth + x; var wx = x + ox; var wy = y + oy;
            var grain = EarthSeed(wx, wy, 401 + layer); var shade = 2;
            if (!rock[i])
            {
                // Water-worn limestone, with broad silt deposits and shallow winding
                // erosion tracks. Quiet flat ground must read separately from the pools.
                var sediment = EarthNoise(wx / 69f, wy / 43f, 409 + layer);
                var erosion = EarthNoise(wx / 42f, wy / 97f, 419 + layer);
                shade = sediment > .61f ? 3 : 2;
                if (sediment > .73f) shade = grain % 5 == 0 ? 4 : 3;
                var line = (wy + (int)(erosion * 23) + (int)(Math.Sin(wx * .026) * 4)) % 31;
                if (erosion > .62f && sediment < .6f)
                {
                    if (line == 0) shade = 1;
                    else if (line == 1 && grain % 3 != 0) shade = 3;
                }
                // Sample neighboring rooms too: the wet sediment must not change
                // when the camera crosses a two-tile region overlap.
                var bankDistance = 14f;
                for (var cy = y / 32 - 1; cy <= y / 32 + 1; cy++) for (var cx = x / 32 - 1; cx <= x / 32 + 1; cx++)
                {
                    if (WaterTile(Tile(cx, cy)) == 0) continue;
                    var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 13);
                    var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 13);
                    bankDistance = Math.Min(bankDistance, MathF.Sqrt(dx * dx + dy * dy));
                }
                if (bankDistance < 7) shade = bankDistance < 3 ? 13 : 1;
                else if (bankDistance < 13 && grain % 7 == 0) shade = 12;
                if (grain % 317 == 0 && sediment > .5f) shade = 4;
                var nearest = 10;
                for (var dy = -3; dy <= 3; dy++) for (var dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy < nearest && Rock(x + dx, y + dy)) nearest = dx * dx + dy * dy;
                if (nearest <= 2) shade = 0;
                else if (nearest <= 8) shade = 1;
            }
            else
            {
                // Small eroded facets over a continuous rock mass. Damp faces are dark;
                // mineral ridges catch light, without drawing a bright outline around walls.
                var closest = float.MaxValue; var second = float.MaxValue; var facetY = 0; var facet = 0;
                for (var sy = wy / 11 - 1; sy <= wy / 11 + 1; sy++) for (var sx = wx / 15 - 1; sx <= wx / 15 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 421 + layer);
                    var dx = wx - (sx * 15 + 3 + seed % 10); var dy = wy - (sy * 11 + 2 + seed / 13 % 7);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; facetY = dy; facet = seed; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                var front = !Rock(x, y + 13 + ledge[(wx / 7) & 15]);
                shade = seam < .8f ? 6 : facetY < 0 ? 10 : 9;
                if (seam is >= .8f and < 1.55f && facetY < -1) shade = 11;
                if (front) shade = seam < .8f ? 0 : facetY < 0 ? 8 : 7;
                var damp = EarthNoise(wx / 19f, wy / 67f, 431 + layer);
                if (damp < .38f && seam > 1.5f) shade = front ? 6 : 8;
                // Sparse vertical mineral trails explain how water eroded the face.
                if (front && facet % 4 == 0 && (wx + wy / 8) % 13 == 0 && seam > 2) shade = 9;
                if (!Rock(x - 1, y) || !Rock(x + 1, y) || !Rock(x, y + 1)) shade = 0;
                else if (!Rock(x, y - 1)) shade = 8;
            }
            data[i] = WaterCavePalette[shade];
        }
        for (var y = 0; y < Map.Height - 1; y++) for (var x = 0; x < Map.Width; x++)
        {
            if (!Wall(x, y)) continue;
            var front = !Wall(x, y + 1);
            var side = !Wall(x - 1, y) ? -1 : !Wall(x + 1, y) ? 1 : 0;
            if (!front && side == 0) continue;
            var seed = EarthSeed(x + column * 11, y + row * 10, 433 + layer);
            // Colonies also cling to side faces in long flooded shafts without a
            // south-facing wall. Their light source stays on the visible rock.
            if (front ? seed % 3 == 0 : seed % 5 == 0)
            {
                var point = new Point(x * 32 + (front ? 16 : side < 0 ? 5 : 26), y * 32 + (front ? 22 : 16)); waterCaveAlgae.Add(point);
                for (var dx = -8; dx <= 8; dx++)
                {
                    var waviness = (int)(Math.Sin((dx + seed % 9) * .6) * (front ? 2 : 1));
                    var ax = point.X + (front ? dx : waviness); var ay = point.Y + (front ? waviness : dx);
                    Put(ax, ay, 14, true);
                    if ((dx + 9) % 3 != 0) Put(ax + (front ? 0 : -side), ay - (front ? 1 : 0), 15, true);
                    if ((dx + seed) % 5 == 0) Put(ax + (front ? 0 : -side * 2), ay - (front ? 2 : 0), 16, true);
                }
            }
            if (!front) continue;
            var floor = new MapPosition(x, y + 1); var tile = map.Tile(floor);
            if (tile is not (0x53 or 0x49 or 0x0d) || map.Events.Chests.Contains(floor)
                || map.Events.Depth.Any(d => d.HasPosition && d.Position == floor)
                || map.Events.Princess.HasPosition && map.Events.Princess.Position == floor
                || map.Events.Message.HasPosition && map.Events.Message.Position == floor) continue;
            if (seed % 3 == 1) waterCaveDrips.Add(new(x * 32 + 12, (y + 1) * 32 + 12));
            if (tile == 0x0d || seed % 4 != 0) continue;
            // Small limestone teeth stay at the rock foot, clear of the walking lane.
            var px = x * 32 + 5 + seed % 8; var pyBase = (y + 1) * 32 + 6;
            for (var dy = 0; dy < 7; dy++)
                for (var dx = -dy / 3; dx <= dy / 3; dx++)
                    Put(px + dx, pyBase - 6 + dy, dx < 0 ? 7 : dx == 0 ? 10 : 8);
            for (var dx = -4; dx <= 4; dx++) Put(px + dx, pyBase + 1, 0);
        }
        waterCaveGround.SetData(data);
        void Put(int x, int y, int shade, bool onRock = false)
        {
            if (x < 0 || x >= WaterWidth || y < 0 || y >= WaterHeight || rock[y * WaterWidth + x] != onRock) return;
            data[y * WaterWidth + x] = WaterCavePalette[shade];
        }
    }

    private void DrawWaterCaveAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsWaterCave(game.Map)) return;
        var t = Math.Floor(time * 8) / 8;
        for (var i = 0; i < waterCaveDrips.Count; i++)
        {
            var point = waterCaveDrips[i]; var phase = (t + point.X * .017 + point.Y * .011) % 5.7;
            if (!game.Visibility.Contains(new(point.X / 32, Math.Max(0, point.Y / 32 - 1)))) continue;
            if (phase < .7)
            {
                var y = point.Y - 24 + (int)(phase * phase / .49 * 24);
                Pixel(point.X, y, WaterCavePalette[22] * .7f, false);
                Pixel(point.X, y - 1, WaterCavePalette[21] * .3f, false);
            }
            else if (phase < 1.8)
                for (var step = 0; step < 20; step++)
                {
                    var angle = step * MathF.Tau / 20; var radius = (float)(phase - .7) * 8;
                    Pixel(point.X + (int)(MathF.Cos(angle) * radius), point.Y + (int)(MathF.Sin(angle) * radius * .3f),
                        WaterCavePalette[21] * (float)((1.8 - phase) * .42), false);
                }
        }
        var region = (game.Map.Id - 1) % 40; var ox = region % 6 * 352; var oy = region / 6 * 320;
        for (var y = 0; y < Map.Height; y++) for (var x = 0; x < Map.Width; x++)
        {
            if (!game.Visibility.Contains(new(x, y)) || WaterTile(game.Map.Tile(new(x, y))) == 0) continue;
            var seed = EarthSeed(x + ox / 32, y + oy / 32, 439);
            // Sparse submerged caustic fragments, never a bright mesh over every pool.
            for (var stroke = 0; stroke < 2; stroke++)
            {
                var px = x * 32 + 6 + (seed / 7 + stroke * 13) % 20;
                var py = y * 32 + 8 + (seed / 13 + stroke * 11) % 16;
                var opacity = .12f + .09f * (float)Math.Sin(t * 1.5 + seed % 19 + stroke);
                for (var dx = -4; dx <= 4; dx++)
                    Pixel(px + dx, py + (int)(Math.Sin(dx * .55 + t * .7 + stroke) * 2), WaterCavePalette[22] * opacity, true);
            }
            // Thin horizontal veils drift over open water, leaving actors and dry routes clear.
            if (seed % 5 != 0) continue;
            var phase = (t * .3 + seed % 11) % 10;
            var left = x * 32 + 2 + (int)(phase * 2); var top = y * 32 + 12;
            for (var dx = 0; dx < 19; dx++)
                Pixel(left + dx, top + (int)(Math.Sin(dx * .2 + phase) * 2),
                    WaterCavePalette[10] * (float)(.13 * Math.Sin(phase / 10 * Math.PI)), true);
        }
        void Pixel(int x, int y, Color color, bool onlyWater)
        {
            if (x < 0 || x >= WaterWidth || y < 0 || y >= WaterHeight || waterCaveRock[y * WaterWidth + x]
                || !game.Visibility.Contains(new(x / 32, y / 32)) || onlyWater && !WaterAt(x, y)) return;
            var tile = game.Map.Tile(new(x / 32, y / 32));
            if (tile is not (0x53 or 0x49 or 0x0d or 0x0c)) return;
            batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }

    internal float WaterCaveWetness(Map map, Vector2 position)
    {
        if (!IsWaterCave(map)) return 0;
        var closest = 5f;
        for (var y = 0; y < Map.Height; y++) for (var x = 0; x < Map.Width; x++)
            if (WaterTile(map.Tile(new(x, y))) != 0) closest = Math.Min(closest, Vector2.Distance(position, new(x, y)));
        return Math.Clamp(1 - closest / 5, 0, 1);
    }
}
