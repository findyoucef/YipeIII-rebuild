using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int EarthWidth = Map.Width * 32, EarthHeight = Map.Height * 32;
    private readonly Texture2D earthGround;
    private readonly ushort[] earthTiles = new ushort[Map.TileCount];
    private readonly List<Point> earthFungi = new(), earthDrips = new();
    private Map? earthMap;

    private static bool IsEarth(Map map) => map.Id is >= 41 and < 117 && map.Environment == 1;
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
        earthMap = map; earthFungi.Clear(); earthDrips.Clear();
        var data = new Color[EarthWidth * EarthHeight];
        var stone = new bool[data.Length];
        var deep = map.Id > 80;
        var light = map.Id switch
        {
            57 => new Vector2(48, 48), 63 => new(300, 225), 69 => new(144, 144),
            75 => new(112, 185), 103 => new(285, 35), 109 => new(245, 190), _ => new(110, 240),
        };
        ReadOnlySpan<int> dither = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        for (var y = 0; y < EarthHeight; y++)
            for (var x = 0; x < EarthWidth; x++)
            {
                var index = y * EarthWidth + x; var tx = x / 32; var ty = y / 32;
                var wall = EarthWall(map, tx, ty);
                var edge = 32f;
                for (var cy = ty - 1; cy <= ty + 1; cy++)
                    for (var cx = tx - 1; cx <= tx + 1; cx++)
                    {
                        if (EarthWall(map, cx, cy) == wall) continue;
                        var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 16);
                        var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 16);
                        edge = Math.Min(edge, MathF.Sqrt(dx * dx + dy * dy));
                    }
                if (wall)
                    for (var sy = -1; sy <= 1; sy += 2)
                        for (var sx = -1; sx <= 1; sx += 2)
                        {
                            if (EarthWall(map, tx + sx, ty) || EarthWall(map, tx, ty + sy)) continue;
                            const float radius = 9;
                            var dx = Math.Max(0, radius - (sx < 0 ? x % 32 + .5f : 31.5f - x % 32));
                            var dy = Math.Max(0, radius - (sy < 0 ? y % 32 + .5f : 31.5f - y % 32));
                            edge = Math.Min(edge, radius - MathF.Sqrt(dx * dx + dy * dy));
                        }
                var broad = EarthNoise(x / 91f, y / 83f, map.Id);
                var grain = EarthSeed(x, y, map.Id);
                var dust = EarthNoise(x / 34f, y / 39f, map.Id + 17);
                var damp = Math.Clamp((EarthNoise(x / 69f, y / 57f, map.Id + 41) - .58f) * 3, 0, 1);
                if (map.Id != 109) damp *= .35f;
                // Solid palette steps and small pixel clusters keep the floor earthy
                // without the blended brown gradients that flattened the cave.
                var threshold = (dither[(y & 3) * 4 + (x & 3)] + .5f) / 16f;
                var floor = broad + (threshold - .5f) * .10f < (deep ? .48f : .36f) ? 15 : 38;
                if (damp > (map.Id == 109 ? .28f : .32f) + threshold * .12f) floor = damp > .72f ? 8 : 9;
                var cluster = EarthSeed(x / 3, y / 2, map.Id + 71);
                if (cluster % 19 == 0 && dust > .43f && x % 3 != 2) floor = 16;
                if (grain % 113 == 0) floor = 14;
                if (edge < 2.5f) floor = 14;
                else if (edge < 5 && threshold < .5f) floor = 15;
                var bank = 2.5f + 1.1f * MathF.Sin(x * .14f + y * .09f) + .6f * MathF.Sin(y * .31f - x * .17f);
                stone[index] = wall && edge > bank;
                var shade = floor;
                if (stone[index])
                {
                    // Broken rock facets use a brighter ramp than the walkable earth.
                    // Sediment crosses tiles, but no longer shades them like smooth wood.
                    var strata = y + 8 * MathF.Sin(x * .021f) + 3 * MathF.Sin(x * .067f + map.Id);
                    var facet = EarthNoise((x / 2 * 2) / 14f, (y / 2 * 2) / 12f, map.Id + 19);
                    shade = facet < .36f ? 16 : 17;
                    if (facet > .75f) shade = 18;
                    if (map.Id == 63 && MathF.Sin(strata * .17f) > .82f) shade = 16;
                    var fissure = EarthNoise(x / 27f, y / 23f, map.Id + 5);
                    if (fissure is > .485f and < .505f) shade = 15;
                    var front = !EarthWall(map, tx, ty + 1) && 32 - y % 32 < 9 + bank;
                    if (front) shade = facet < .4f ? 15 : 16;
                    if (edge < bank + 1.2f) shade = 14;
                    else if (!front && edge < bank + 2.4f) shade = 18;
                    if (!front && grain % 71 == 0) shade = 18;
                }
                var distance = Vector2.DistanceSquared(new(x, y), light);
                var illumination = MathF.Exp(-distance / (map.Id == 57 ? 11000 : 4300));
                // Local light promotes a palette step with sparse dithering, not a wash.
                if (threshold < illumination * .22f && edge > 5)
                    shade = shade switch { 15 => 38, 16 => 17, _ => shade };
                data[index] = RetroArt.Palette[shade];
            }

        // Paint details into the cached terrain. They cannot rise over actors or
        // spill into a neighboring fog tile, and never select by a secret tile ID.
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var seed = EarthSeed(x, y, map.Id);
                if (EarthWall(map, x, y))
                {
                    if (seed % (map.Id == 57 ? 3 : 7) == 0)
                        Root(x * 32 + 8 + seed % 15, y * 32 + 2, 16 + seed % 22, seed);
                    if (!EarthWall(map, x, y + 1) && seed % 6 == 0 && earthFungi.Count < 5)
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
                    if (earthDrips.Count < (map.Id == 109 ? 5 : 2) && seed % 11 == 0 && EarthWall(map, x, y - 1))
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

    private void AddEarthPuddles(Map map)
    {
        // Small hollows in the earth, with an uneven basin, a shallow finger and
        // a bite of dry ground. The existing floor remains freely walkable.
        (MapPosition Cell, Point Radius)[] pools = map.Id switch
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
        for (var i = 0; i < earthFungi.Count; i++)
        {
            var point = earthFungi[i];
            var breath = .75f + .25f * MathF.Sin((float)t * .65f + i * 2);
            for (var dy = -9; dy <= 9; dy++)
                for (var dx = -11; dx <= 11; dx++)
                {
                    var falloff = 1 - dx * dx / 121f - dy * dy / 81f;
                    if (falloff > 0) Pixel(point.X + dx, point.Y + dy, new Color(129, 183, 135) * (falloff * falloff * breath * .13f));
                }
        }
        for (var i = 0; i < earthDrips.Count; i++)
        {
            var point = earthDrips[i]; var phase = (t + i * 1.37) % 4.7;
            if (phase < .65)
            {
                var y = point.Y - 17 + (int)(phase / .65 * 17);
                Pixel(point.X, y, new Color(143, 169, 145) * .55f);
                Pixel(point.X, y - 1, new Color(143, 169, 145) * .25f);
            }
            else if (phase < 1.65)
            {
                var radius = (float)(phase - .65) * 8;
                for (var step = 0; step < 20; step++)
                {
                    var angle = step * MathF.Tau / 20;
                    Pixel(point.X + (int)(MathF.Cos(angle) * radius), point.Y + (int)(MathF.Sin(angle) * radius * .35f),
                        new Color(148, 166, 137) * (float)((1.65 - phase) * .28));
                }
            }
        }
        for (var mote = 0; mote < 12; mote++)
        {
            var seed = EarthSeed(mote, 23, game.Map.Id);
            var phase = (t + seed % 79) % 9;
            if (phase > 3) continue;
            var x = 8 + seed % (EarthWidth - 16) + (int)(Math.Sin(t + mote) * 2);
            var y = 12 + seed / 17 % (EarthHeight - 48) + (int)(phase * 7);
            Pixel(x, y, new Color(187, 158, 109) * (float)(Math.Sin(phase / 3 * Math.PI) * .35));
        }
        void Pixel(int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < EarthWidth && y < EarthHeight && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }
}
