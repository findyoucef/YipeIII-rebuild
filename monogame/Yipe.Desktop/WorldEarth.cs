using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int EarthWidth = Map.Width * 32, EarthHeight = Map.Height * 32;
    private readonly Texture2D earthGround;
    private readonly ushort[] earthTiles = new ushort[Map.TileCount];
    private readonly List<Point> earthFungi = new(), earthLamps = new(), earthDrips = new();
    private bool[] earthStone = [];
    internal static readonly Color[] EarthSoilPalette =
    [
        new(36, 31, 25), new(49, 41, 31), new(61, 49, 36), new(65, 52, 38),
        new(74, 60, 43), new(84, 70, 50), new(52, 47, 34), new(62, 57, 40),
    ];
    private Map? earthMap;

    internal static bool IsEarth(Map map) => map.Id is 57 or 63 or 69 or 75 or 103 or 109 or 115;
    internal bool[] EarthStone(Map map) { PrepareEarth(map); return earthStone; }
    internal Point[] EarthFungi(Map map) { PrepareEarth(map); return earthFungi.ToArray(); }
    internal Point[] EarthLamps(Map map) { PrepareEarth(map); return earthLamps.ToArray(); }
    // Secret walls have precisely the same silhouette and decoration as ordinary rock.
    private static bool EarthWall(Map map, int x, int y) =>
        map.Tile(new(Math.Clamp(x, 0, Map.Width - 1), Math.Clamp(y, 0, Map.Height - 1))) is 0x22 or 0x43;

    private static int EarthSeed(int x, int y, int seed)
    {
        var bits = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 144269));
        bits = unchecked((bits ^ bits >> 13) * 1274126177);
        return (int)((bits ^ bits >> 16) & 0x7fffffff);
    }

    private static float EarthNoise(float x, float y, int seed)
    {
        var ix = (int)MathF.Floor(x); var iy = (int)MathF.Floor(y);
        var fx = x - ix; var fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float Sample(int dx, int dy) => EarthSeed(ix + dx, iy + dy, seed) % 1024 / 1023f;
        return MathHelper.Lerp(MathHelper.Lerp(Sample(0, 0), Sample(1, 0), fx),
            MathHelper.Lerp(Sample(0, 1), Sample(1, 1), fx), fy);
    }

    private void PrepareEarth(Map map)
    {
        if (!IsEarth(map)) return;
        var changed = !ReferenceEquals(earthMap, map);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var tile = map.Tile(new(x, y)); var index = y * Map.Width + x;
                changed |= earthTiles[index] != tile; earthTiles[index] = tile;
            }
        if (!changed) return;
        earthMap = map; earthFungi.Clear(); earthLamps.Clear(); earthDrips.Clear();
        PrepareWater(map);
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * 352; var oy = row * 320;
        var neighbors = new Map?[3, 3]; neighbors[1, 1] = map;
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = catalog.CreateMap(layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1);
            if (next is not null && IsEarth(next)) neighbors[dx + 1, dy + 1] = next;
        }
        bool Wall(int x, int y)
        {
            if (Map.Contains(new(x, y))) return EarthWall(map, x, y);
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0; var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? EarthWall(next, x - dx * 11, y - dy * 10) : EarthWall(map, x, y);
        }
        int[] ledge = [0, 1, 2, 1, 3, 2, 0, 1, 2, 1, 0, 2, 1, 3, 1, 0];
        var stone = earthStone = new bool[EarthWidth * (EarthHeight + 16)];
        for (var y = 0; y < EarthHeight + 16; y++) for (var x = 0; x < EarthWidth; x++)
            stone[y * EarthWidth + x] = StoneAt(x, y);
        bool StoneAt(int x, int y)
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
        bool Stone(int x, int y) => x >= 0 && x < EarthWidth && y >= 0 && y < EarthHeight + 16
            ? stone[y * EarthWidth + x] : StoneAt(x, y);
        var data = new Color[EarthWidth * EarthHeight];
        ReadOnlySpan<int> dither = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        for (var y = 0; y < EarthHeight; y++) for (var x = 0; x < EarthWidth; x++)
        {
            var index = y * EarthWidth + x; var wx = x + ox; var wy = y + oy;
            var grain = EarthSeed(wx, wy, 211 + layer);
            if (!stone[index])
            {
                // Compacted soil: broad deposits, shallow embedded stones and
                // sparse grains. No outlined paving or web of slate fractures.
                var field = EarthNoise(wx / 67f, wy / 49f, 193 + layer);
                var threshold = (dither[(wy & 3) * 4 + (wx & 3)] + .5f) / 16f;
                var shade = field + (threshold - .5f) * .05f > .52f ? 3 : 2;
                var wash = EarthNoise(wx / 29f, wy / 13f, 197 + layer);
                if (wash > .68f && field > .42f) shade = 3;
                var bed = EarthNoise(wx / 13f, wy / 9f, 199 + layer);
                if (bed > .71f)
                {
                    shade = 4;
                    if (bed > .8f) shade = 5;
                    // Only a short down-slope seam gives stones shallow relief.
                    if (bed < .73f && EarthNoise(wx / 13f, (wy - 1) / 9f, 199 + layer) > bed) shade = 2;
                }
                // Deposited damp silt carries across the deep E5 overlaps.
                var dampDistance = Vector2.DistanceSquared(new(wx, wy), new(4 * 352 + 245, 4 * 320 + 190));
                var wet = layer == 2 ? Math.Max(0, 1 - dampDistance / 105000f) : 0;
                if (wet > .2f && wash > .6f + (1 - wet) * .2f) shade = wash > .76f ? 7 : 6;
                // Silt follows the existing irregular puddle shorelines.
                if (shoreDistance[index] < 7 + wash * 3) shade = threshold < .7f ? 6 : 2;
                if (EarthSeed(wx / 3, wy / 2, 239 + layer) % 31 == 0 && wx % 3 != 2) shade = 4;
                if (grain % 151 == 0) shade = shade is 6 or 7 ? 7 : 4;
                else if (grain % 223 == 0) shade = 1;
                var nearest = 10;
                for (var dy = -3; dy <= 3; dy++) for (var dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy < nearest && Stone(x + dx, y + dy)) nearest = dx * dx + dy * dy;
                if (nearest <= 2) shade = 0;
                else if (nearest <= 5) shade = 1;
                data[index] = EarthSoilPalette[shade];
            }
            else
            {
                // Small sedimentary facets, interrupted joints and chipped
                // ledges retain the warm stone without a continuous bright rim.
                var closest = float.MaxValue; var second = float.MaxValue; var facetY = 0;
                for (var sy = wy / 10 - 1; sy <= wy / 10 + 1; sy++) for (var sx = wx / 15 - 1; sx <= wx / 15 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 223 + layer);
                    var dx = wx - (sx * 15 + 3 + seed % 9); var dy = wy - (sy * 10 + 2 + seed / 11 % 6);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; facetY = dy; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                var shade = facetY < 2 ? 17 : 16;
                if (seam < .65f) shade = 15;
                else if (seam < 1.5f && facetY < -1) shade = 18;
                var strata = (wy + (int)(3 * MathF.Sin(wx * .029f)) + 64) % 17;
                if (strata == 0 && grain % 5 != 0) shade = 16;
                var front = !Stone(x, y + 10 + ledge[(wx / 9) & 15]);
                if (front) shade = seam < .65f ? 14 : facetY < 1 ? 16 : 15;
                if (!Stone(x - 1, y) || !Stone(x + 1, y) || !Stone(x, y + 1)) shade = 14;
                else if (!Stone(x, y - 1)) shade = 16;
                if (!front && seam > 2 && grain % 113 == 0) shade = 18;
                data[index] = RetroArt.Palette[shade];
            }
        }

        // Paint details into the cached terrain. They cannot rise over actors or
        // spill into a neighboring fog tile, and never select by a secret tile ID.
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var seed = EarthSeed(x + column * 11, y + row * 10, 229 + layer);
                if (EarthWall(map, x, y))
                {
                    if (seed % (map.Id == 57 ? 3 : 7) == 0)
                        Root(x * 32 + 8 + seed % 15, y * 32 + 2, 16 + seed % 22, seed);
                    if (!Wall(x, y + 1) && y + 1 < Map.Height && seed % 7 == 0)
                        earthLamps.Add(new(x * 32 + 16, y * 32 + 20));
                    if (!Wall(x, y + 1) && seed % 7 != 0 && seed % 4 == 0)
                    {
                        var point = new Point(x * 32 + 12 + seed % 8, y * 32 + 24);
                        earthFungi.Add(point);
                        for (var cap = 0; cap < 3; cap++)
                        {
                            var px = point.X + cap * 4 - 4; var py = point.Y + cap % 2 * 2;
                            Line(px, py, px, py + 4, new Color(138, 144, 106), wallOnly: true);
                            Line(px - 2, py, px + 2, py, new Color(99, 145, 116), wallOnly: true);
                            Line(px - 1, py - 1, px + 1, py - 1, new Color(145, 172, 125), wallOnly: true);
                        }
                    }
                }
                else
                {
                    // Loose stones sit at the wall foot, never in the lane's center.
                    if (EarthWall(map, x, y - 1) && seed % 3 == 0)
                        for (var chip = 0; chip < 3; chip++)
                        {
                            var scatter = EarthSeed(x * 3 + chip, y, map.Id);
                            var px = x * 32 + 3 + scatter % 25; var py = y * 32 + 2 + scatter / 17 % 7;
                            var width = 1 + scatter % 3;
                            Line(px - 1, py + 1, px + width + 1, py + 1, new Color(54, 43, 33));
                            Line(px, py, px + width, py, new Color(133, 109, 76));
                            if (width == 3) Line(px + 1, py - 1, px + 2, py - 1, new Color(148, 122, 83));
                        }
                    if (earthDrips.Count < (map.Id == 109 ? 3 : 1) && seed % 7 == 0 && Wall(x, y - 1))
                    {
                        var point = new Point(x * 32 + 16, y * 32 + 12);
                        earthDrips.Add(point);
                        for (var dy = -3; dy <= 3; dy++)
                            for (var dx = -9; dx <= 9; dx++)
                            {
                                var oval = dx * dx / 81f + dy * dy / 9f;
                                if (oval < 1) Put(point.X + dx, point.Y + dy,
                                    Color.Lerp(data[(point.Y + dy) * EarthWidth + point.X + dx], new Color(68, 87, 76), (1 - oval) * .65f));
                            }
                    }
                }
            }

        // Authored landmarks give each room an identity without adding obstacles.
        if (map.Id == 57)
        {
            Root(173, 2, 113, 17); Root(23, 1, 88, 31); Root(301, 2, 81, 53);
        }
        if (map.Id == 69)
        {
            Fossil(146, 144, 11); Fossil(176, 272, 8);
        }
        if (map.Id is 75 or 115)
        {
            var start = map.Id == 75 ? new Point(42, 170) : new Point(63, 169);
            for (var block = 0; block < 6; block++)
            {
                var px = start.X + block * 18; var py = start.Y + (block % 3 - 1) * 2;
                for (var dy = 0; dy < 12; dy++)
                    for (var dx = 0; dx < 16; dx++)
                        if (EarthSeed(dx, dy, block + map.Id) % 13 != 0)
                            Put(px + dx, py + dy, dy == 0 ? new Color(165, 139, 102) : new Color(119, 108, 84), true);
                Line(px + 2, py + 11, px + 13, py + 11, new Color(70, 62, 47), true);
            }
            if (map.Id == 115) Fossil(207, 111, 9);
        }
        if (map.Id == 103)
        {
            foreach (var (origin, length) in new[] { (new Point(175, 17), 114), (new Point(13, 170), 93), (new Point(310, 176), 83) })
                for (var step = 0; step < length; step++)
                {
                    var x = origin.X + step;
                    var y = origin.Y + (int)(6 * MathF.Sin(step * .049f + origin.X) + 2 * MathF.Sin(step * .27f));
                    Put(x, y + 1, new Color(47, 69, 54), true);
                    Put(x, y, new Color(102, 142, 112), true);
                    if (step % 23 == 7)
                    {
                        var direction = step % 2 == 0 ? 1 : -1;
                        Line(x, y, x + 8, y + direction * 5, new Color(99, 126, 90), true);
                        Line(x + 8, y + direction * 5, x + 13, y + direction * 6, new Color(129, 152, 108), true);
                    }
                    if (step % 29 < 3)
                    {
                        Put(x, y - 1, new Color(149, 166, 124), true);
                        Put(x + 1, y - 2, new Color(172, 181, 140), true);
                    }
                }
        }
        foreach (var (cell, _) in EarthPools(map.Id))
            if (WaterAt(cell.X * 32 + 16, cell.Y * 32 + 32))
                earthDrips.Add(new(cell.X * 32 + 16, cell.Y * 32 + 32));
        earthGround.SetData(data);

        void Put(int x, int y, Color color, bool wallOnly = false)
        {
            if (x < 0 || y < 0 || x >= EarthWidth || y >= EarthHeight) return;
            var index = y * EarthWidth + x;
            if (wallOnly && !stone[index]) return;
            // Detail colors follow the same palette contract as the sprite atlases.
            var bestDistance = int.MaxValue;
            foreach (var candidate in RetroArt.Palette)
            {
                var dr = color.R - candidate.R; var dg = color.G - candidate.G; var db = color.B - candidate.B;
                var distance = dr * dr + dg * dg + db * db;
                if (distance >= bestDistance) continue;
                bestDistance = distance; data[index] = candidate;
                if (distance == 0) break;
            }
        }
        void Line(int x, int y, int endX, int endY, Color color, bool wallOnly = false)
        {
            var steps = Math.Max(Math.Abs(endX - x), Math.Abs(endY - y));
            for (var step = 0; step <= steps; step++)
            {
                var t = steps == 0 ? 0 : step / (float)steps;
                Put((int)MathF.Round(MathHelper.Lerp(x, endX, t)), (int)MathF.Round(MathHelper.Lerp(y, endY, t)), color, wallOnly);
            }
        }
        void Root(int startX, int startY, int length, int seed)
        {
            var previousX = startX; var previousY = startY;
            for (var step = 1; step < length; step++)
            {
                var x = startX + (int)(5 * MathF.Sin(step * .065f + seed) + step * .035f);
                var y = startY + step;
                var width = step < length / 3 ? 5 : step < length * 2 / 3 ? 3 : 1;
                for (var dx = -1; dx < width + 1; dx++) Line(previousX + dx, previousY + 1, x + dx, y + 1, new Color(46, 39, 27), true);
                for (var dx = 0; dx < width; dx++) Line(previousX + dx, previousY, x + dx, y, new Color(101, 81, 45), true);
                if (width > 1) Line(previousX + 1, previousY, x + 1, y, new Color(157, 129, 75), true);
                if (step % 19 == 7)
                {
                    var direction = step % 2 == 0 ? 1 : -1;
                    Line(x, y, x + direction * 11, y + 8, new Color(46, 39, 27), true);
                    Line(x, y, x + direction * 9, y + 7, new Color(137, 112, 64), true);
                    Line(x + direction * 9, y + 7, x + direction * 11, y + 14, new Color(100, 82, 46), true);
                }
                previousX = x; previousY = y;
            }
        }
        void Fossil(int x, int y, int radius)
        {
            for (var step = 0; step < 120; step++)
            {
                var angle = step * .14f; var r = radius * (1 - step / 130f);
                var px = x + (int)(MathF.Cos(angle) * r); var py = y + (int)(MathF.Sin(angle) * r * .8f);
                Put(px, py + 1, new Color(67, 53, 38), true);
                Put(px, py, new Color(178, 153, 112), true);
                if (step % 9 == 0) Line(px, py, x + (int)(MathF.Cos(angle) * (r + 2)), y + (int)(MathF.Sin(angle) * (r + 2) * .8f), new Color(147, 125, 90), true);
            }
        }
    }

    private static (MapPosition Cell, Point Radius)[] EarthPools(int id) => id switch
    {
        57 => [(new(3, 4), new(24, 16)), (new(8, 8), new(21, 14))],
        63 => [(new(5, 5), new(23, 15)), (new(9, 3), new(18, 13))],
        69 => [(new(8, 6), new(21, 16)), (new(4, 2), new(24, 14))],
        75 => [(new(7, 6), new(20, 14)), (new(8, 2), new(22, 15))],
        103 => [(new(6, 5), new(24, 16)), (new(8, 8), new(20, 14))],
        109 => [(new(7, 5), new(21, 15)), (new(7, 1), new(23, 14))],
        115 => [(new(4, 8), new(23, 16)), (new(8, 2), new(19, 13))],
        _ => [],
    };

    private void AddEarthPuddles(Map map)
    {
        // Small hollows in the earth, with an uneven basin, a shallow finger and
        // a bite of dry ground. The existing floor remains freely walkable.
        var pools = EarthPools(map.Id);
        foreach (var (cell, radius) in pools)
        {
            var center = new Point(cell.X * 32 + 16, cell.Y * 32 + 32);
            var seed = EarthSeed(cell.X, cell.Y, map.Id);
            for (var y = Math.Max(0, center.Y - radius.Y - 2); y <= Math.Min(EarthHeight - 1, center.Y + radius.Y + 2); y++)
                for (var x = Math.Max(0, center.X - radius.X - 4); x <= Math.Min(EarthWidth - 1, center.X + radius.X + 4); x++)
                {
                    // Secret floor has the same appearance as ordinary floor.
                    if (map.Tile(new(x / 32, y / 32)) is not (0x27 or 0x47)) continue;
                    var dx = (x - center.X) / (float)radius.X * (seed % 2 == 0 ? 1 : -1);
                    var dy = (y - center.Y) / (float)radius.Y;
                    var basinX = dx + .18f + dy * .24f;
                    var basin = basinX * basinX / .78f + dy * dy;
                    var finger = (dx - .65f) * (dx - .65f) / .22f + (dy - .25f) * (dy - .25f) / .28f;
                    var bite = (dx - .55f) * (dx - .55f) / .18f + (dy + .65f) * (dy + .65f) / .26f;
                    var rim = .84f + .16f * MathF.Sin(x * .39f + y * .23f + seed % 17)
                        + .1f * MathF.Sin(x * .21f - y * .48f);
                    if (Math.Min(basin, finger) < rim && bite > 1) waterMask[y * WaterWidth + x] = true;
                }
        }
    }

    private void DrawEarthAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsEarth(game.Map)) return;
        var t = Math.Floor(time * 8) / 8;
        // A few bright spores orbit visible fungi. Illumination itself comes
        // from PixelLighting's contour mask, not an unoccluded painted halo.
        foreach (var point in earthFungi)
            for (var mote = 0; mote < 3; mote++)
            {
                var phase = (t + mote * 1.4 + point.X * .03) % 5;
                var x = point.X + (int)(Math.Sin(phase * 1.4 + mote) * 9);
                var y = point.Y - (int)(phase * 3);
                Pixel(x, y, new Color(151, 181, 119) * (float)(Math.Sin(phase / 5 * Math.PI) * .45));
            }
        for (var i = 0; i < earthDrips.Count; i++)
        {
            var point = earthDrips[i]; var phase = (t + i * 1.37) % (game.Map.Id > 80 ? 3.7 : 4.7);
            if (phase < .65)
            {
                var y = point.Y - 17 + (int)(phase / .65 * 17);
                Pixel(point.X, y, new Color(158, 177, 143) * .8f);
                Pixel(point.X, y - 1, new Color(158, 177, 143) * .4f);
            }
            else if (phase < 1.65)
            {
                var radius = (float)(phase - .65) * 8;
                for (var step = 0; step < 20; step++)
                {
                    var angle = step * MathF.Tau / 20;
                    Pixel(point.X + (int)(MathF.Cos(angle) * radius), point.Y + (int)(MathF.Sin(angle) * radius * .35f),
                        new Color(148, 166, 137) * (float)((1.65 - phase) * .48));
                }
            }
        }
        foreach (var point in earthLamps)
            for (var mote = 0; mote < 5; mote++)
            {
                var phase = (t + mote * 1.73 + point.X * .017) % 8;
                var x = point.X - 16 + mote * 8 + (int)(Math.Sin(t + mote) * 2);
                var y = point.Y + 5 + (int)(phase * 3);
                if (x >= 0 && x < EarthWidth && y >= 0 && y < EarthHeight && !earthStone[y * EarthWidth + x])
                    Pixel(x, y, EarthSoilPalette[5] * (float)(Math.Sin(phase / 8 * Math.PI) * .7));
            }
        // Occasional grains shed at damp wall feet, disappearing on landing.
        for (var i = 0; i < earthDrips.Count; i++)
        {
            var point = earthDrips[i]; var phase = (t + i * 3.17) % 12;
            if (phase > 1) continue;
            for (var grain = 0; grain < 3; grain++)
                Pixel(point.X + 7 + grain * 2, point.Y - 20 + (int)(phase * phase * 20) - grain,
                    EarthSoilPalette[5] * (float)((1 - phase) * .7));
        }
        void Pixel(int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < EarthWidth && y < EarthHeight && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }
}
