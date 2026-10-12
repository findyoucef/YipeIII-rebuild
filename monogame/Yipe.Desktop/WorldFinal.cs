using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int FinalWidth = Map.Width * 32, FinalHeight = Map.Height * 32;
    internal static readonly Color[] FinalPalette =
    [
        new(19, 18, 26), new(32, 28, 36), new(43, 37, 44), new(47, 40, 47),
        new(57, 49, 53), new(68, 57, 61), new(42, 38, 50), new(58, 50, 64),
        new(76, 64, 79), new(90, 77, 92), new(105, 91, 103), new(125, 108, 114),
        new(73, 31, 43), new(112, 35, 52), new(173, 49, 67), new(223, 89, 99),
        new(251, 161, 148),
    ];
    private readonly Texture2D finalGround;
    private readonly ushort[] finalTiles = new ushort[Map.TileCount];
    private Map? finalMap;
    private bool[] finalRock = [];
    private readonly List<Point> finalCrystals = new(), finalGrit = new();

    internal static bool IsFinal(Map map) => map.Id is 43 or 48 or 49 or 54 or 55 or 61 or 83 or 84 or 85 or 86 or 89 or 91 or 92 or 97 or 98;
    private static bool FinalWall(ushort tile) => tile is 0x22 or 0x43;
    internal bool[] FinalRock(Map map) { PrepareFinal(map); return finalRock; }
    internal Point[] FinalLights(Map map) { PrepareFinal(map); return finalCrystals.ToArray(); }

    private void PrepareFinal(Map map)
    {
        if (!IsFinal(map)) return;
        var changed = !ReferenceEquals(finalMap, map);
        for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
        {
            var i = x * Map.Height + y; var tile = map.Tile(new(x, y));
            changed |= finalTiles[i] != tile; finalTiles[i] = tile;
        }
        if (!changed) return;
        finalMap = map; finalCrystals.Clear(); finalGrit.Clear(); PrepareWater(map);
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * 352; var oy = row * 320;
        var neighbors = new Map?[3, 3]; neighbors[1, 1] = map;
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = catalog.CreateMap(layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1);
            if (next is not null && IsFinal(next)) neighbors[dx + 1, dy + 1] = next;
        }
        bool Wall(int x, int y)
        {
            if (Map.Contains(new(x, y))) return FinalWall(map.Tile(new(x, y)));
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0; var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? FinalWall(next.Tile(new(x - dx * 11, y - dy * 10)))
                : FinalWall(map.Tile(new(Math.Clamp(x, 0, 12), Math.Clamp(y, 0, 11))));
        }
        int[] ledge = [0, 1, 3, 2, 1, 0, 2, 1, 3, 1, 0, 1, 2, 3, 1, 0];
        var rock = finalRock = new bool[FinalWidth * (FinalHeight + 16)];
        for (var y = 0; y < FinalHeight + 16; y++) for (var x = 0; x < FinalWidth; x++) rock[y * FinalWidth + x] = RockAt(x, y);
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
        bool Rock(int x, int y) => x >= 0 && x < FinalWidth && y >= 0 && y < FinalHeight + 16
            ? rock[y * FinalWidth + x] : RockAt(x, y);
        var data = new Color[FinalWidth * FinalHeight];
        for (var y = 0; y < FinalHeight; y++) for (var x = 0; x < FinalWidth; x++)
        {
            var i = y * FinalWidth + x; var wx = x + ox; var wy = y + oy;
            var grain = EarthSeed(wx, wy, 401 + layer); var shade = 2;
            if (!rock[i])
            {
                // Ancient bedrock worn almost level. Broad slabs and occasional
                // shallow scars read as floor, beneath the wall's small facets.
                var closest = float.MaxValue; var second = float.MaxValue; var plate = 0; var plateY = 0;
                for (var sy = wy / 39 - 1; sy <= wy / 39 + 1; sy++) for (var sx = wx / 55 - 1; sx <= wx / 55 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 409 + layer);
                    var dx = wx - (sx * 55 + 9 + seed % 37); var dy = wy - (sy * 39 + 7 + seed / 17 % 25);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; plate = seed; plateY = dy; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                var wear = EarthNoise(wx / 67f, wy / 43f, 419 + layer);
                shade = plate % 3 == 0 ? 3 : 2;
                if (wear > .65f) shade = 3;
                if (seam < .55f && plate % 3 == 0 && plateY > -5) shade = 1;
                else if (seam < 1.25f && plate % 3 == 0 && plateY is > 0 and < 12) shade = 4;
                // Sparse abrasion parallel to the bedding, broken by old wear.
                var scar = (wy + (int)(Math.Sin(wx * .08) * 2)) % 23;
                if (plate % 5 == 0 && seam > 5 && wear is > .46f and < .6f)
                {
                    if (scar == 0) shade = 1;
                    else if (scar == 1) shade = 3;
                }
                if (grain % 233 == 0) shade = 4;
                else if (grain % 317 == 0) shade = 1;
                var nearest = 9;
                for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
                    if (dx * dx + dy * dy < nearest && Rock(x + dx, y + dy)) nearest = dx * dx + dy * dy;
                if (nearest <= 2) shade = 0;
                else if (nearest <= 8) shade = 1;
            }
            else
            {
                var closest = float.MaxValue; var second = float.MaxValue; var facetY = 0; var facet = 0;
                for (var sy = wy / 11 - 1; sy <= wy / 11 + 1; sy++) for (var sx = wx / 13 - 1; sx <= wx / 13 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 421 + layer);
                    var dx = wx - (sx * 13 + 3 + seed % 8); var dy = wy - (sy * 11 + 2 + seed / 7 % 7);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; facetY = dy; facet = seed; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                shade = facetY < 2 ? 9 : 8;
                if (seam < .85f) shade = 6;
                else if (seam < 1.7f && facetY < -1) shade = 10;
                var front = !Rock(x, y + 12 + ledge[(wx / 9) & 15]);
                if (front) shade = seam < .85f ? 1 : facetY < 2 ? 7 : 6;
                // Short sedimentary laminations, never a continuous bright rim.
                if (!front && facet % 4 == 0 && facetY == 3 && seam > 2) shade = 7;
                if (!Rock(x - 1, y) || !Rock(x + 1, y) || !Rock(x, y + 1)) shade = 0;
                else if (!Rock(x, y - 1)) shade = 7;
                if (!front && grain % 181 == 0) shade = 10;
            }
            data[i] = FinalPalette[shade];
        }
        for (var y = 0; y < Map.Height; y++) for (var x = 0; x < Map.Width; x++)
        {
            if (!Wall(x, y)) continue;
            var front = !Wall(x, y + 1);
            var left = !Wall(x - 1, y); var right = !Wall(x + 1, y);
            if (!front && !left && !right) continue;
            var seed = EarthSeed(x + column * 11, y + row * 10, 431 + layer);
            if (seed % 4 == 0)
            {
                var point = new Point(x * 32 + (front ? 16 : left ? 8 : 24), y * 32 + 20); finalCrystals.Add(point);
                // Small angular garnet clusters embedded in the rock. Their
                // visible cores occupy precisely the contour-light source.
                for (var shard = 0; shard < 3; shard++)
                {
                    var cx = point.X + (shard - 1) * 5; var top = point.Y - (shard == 1 ? 9 : 5);
                    for (var py = top; py <= point.Y + 3; py++)
                    {
                        var half = Math.Min(2, Math.Min(py - top, point.Y + 4 - py));
                        for (var px = -half; px <= half; px++)
                            Put(cx + px, py, px < 0 ? 15 : px == 0 ? 14 : 13, true);
                    }
                    Put(cx, top + 1, 16, true);
                }
            }
            if (!front || y + 1 >= Map.Height) continue;
            var floor = new MapPosition(x, y + 1);
            if (map.Tile(floor) is not (0x28 or 0x48)) continue;
            if (map.Events.Chests.Contains(floor) || map.Events.Depth.Any(d => d.HasPosition && d.Position == floor)
                || map.Events.Princess.HasPosition && map.Events.Princess.Position == floor) continue;
            finalGrit.Add(new(x * 32 + 16, (y + 1) * 32 + 8));
            for (var chip = 0; chip < 3; chip++)
            {
                var px = x * 32 + 4 + (seed + chip * 7) % 23; var py = (y + 1) * 32 + 3 + chip % 2 * 3;
                var width = 1 + (seed + chip) % 3;
                for (var dx = -1; dx <= width; dx++) Put(px + dx, py + 1, 0);
                for (var dx = 0; dx < width; dx++) Put(px + dx, py, chip == 1 ? 5 : 4);
                if (width > 1) Put(px + 1, py - 1, 8);
            }
        }
        finalGround.SetData(data);
        void Put(int x, int y, int shade, bool wall = false)
        {
            if (x < 0 || x >= FinalWidth || y < 0 || y >= FinalHeight || rock[y * FinalWidth + x] != wall) return;
            data[y * FinalWidth + x] = FinalPalette[shade];
        }
    }

    private void DrawFinalAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsFinal(game.Map)) return;
        var t = Math.Floor(time * 8) / 8;
        // A handful of pale ash flecks across the whole room, with irregular drift.
        for (var i = 0; i < 42; i++)
        {
            var seed = EarthSeed(i, game.Map.Id, 439);
            var x = (int)((seed % FinalWidth + t * (1 + i % 3)) % FinalWidth);
            var y = (int)((seed / 17 % FinalHeight + t * (2 + i % 4)) % FinalHeight);
            Pixel(x + (int)(Math.Sin(t * .7 + i) * 3), y, FinalPalette[11] * (.2f + i % 3 * .055f));
        }
        for (var i = 0; i < finalGrit.Count; i++)
        {
            var point = finalGrit[i]; var phase = (t + i * 2.73) % 17;
            if (phase < 1.2)
                for (var chip = 0; chip < 3; chip++)
                    Pixel(point.X - 6 + chip * 4, point.Y - 22 + (int)(phase * phase / 1.44 * 22) - chip,
                        FinalPalette[8] * (float)((1 - phase / 1.2) * .5));
        }
        // Thin, broken ground haze: native pixels, kept clear of hazards/events.
        for (var band = 0; band < 5; band++)
        {
            var seed = EarthSeed(band, game.Map.Id, 443);
            var cx = (int)((seed % FinalWidth + t * 2) % (FinalWidth + 60)) - 30;
            var cy = 22 + seed / 13 % (FinalHeight - 44);
            for (var dx = -20; dx <= 20; dx++)
            {
                var y = cy + (int)(Math.Sin(dx * .08 + t * .17 + band) * 2);
                if ((dx + band + 24) % 3 == 0) continue;
                Pixel(cx + dx, y, FinalPalette[10] * ((1 - Math.Abs(dx) / 21f) * .11f));
            }
        }
        void Pixel(int x, int y, Color color)
        {
            if (x < 0 || x >= FinalWidth || y < 0 || y >= FinalHeight || finalRock[y * FinalWidth + x]) return;
            var cell = new MapPosition(x / 32, y / 32);
            if (game.Visibility.Contains(cell) && game.Map.Tile(cell) is 0x28 or 0x48 or 2 or 0x1f or 0x20)
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }
}
