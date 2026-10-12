using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int FireWidth = Map.Width * 32, FireHeight = Map.Height * 32;
    internal static readonly Color[] FirePalette =
    [
        new(23, 16, 19), new(35, 23, 25), new(44, 29, 29), new(50, 32, 31), new(57, 37, 33),
        new(46, 34, 37), new(64, 47, 49), new(85, 62, 59), new(112, 81, 70), new(156, 93, 51),
        new(74, 31, 29), new(127, 44, 24), new(194, 63, 19), new(235, 100, 23),
        new(255, 157, 40), new(255, 216, 87), new(255, 240, 170), new(117, 98, 47), new(80, 69, 53),
        new(114, 22, 13), new(168, 34, 15), new(215, 59, 20),
    ];
    private readonly Texture2D fireGround, fireLava;
    private readonly ushort[] fireTiles = new ushort[Map.TileCount];
    private Map? fireMap;
    private MapVisibility? fireVisibility;
    private bool[] fireStone = [], fireMolten = [];
    private Point[] fireVents = [];

    internal static bool IsFire(Map map) => map.Id is 67 or 71 or 72 or 73 or 95 or 100 or 101 or 107 or 111 or 112 or 113;
    private static bool FireWall(ushort tile) => tile is 0x25 or 0x5a;
    private static Point[] FireSources(int id) => id switch
    {
        67 => [new(144, 80), new(304, 80), new(16, 176), new(400, 240)],
        71 => [new(112, 208), new(176, 304), new(336, 16), new(400, 176)],
        72 => [new(112, 144), new(176, 208), new(240, 240), new(368, 208), new(304, 368)],
        73 => [new(208, 144), new(144, 272), new(400, 112), new(80, 368)],
        95 => [new(176, 112), new(112, 176), new(400, 272), new(48, 368)],
        100 => [new(304, 208), new(144, 272), new(16, 112), new(208, 16)],
        101 => [new(112, 176), new(208, 208), new(272, 272), new(368, 304), new(304, 80)],
        107 => [new(240, 80), new(176, 144), new(144, 240), new(240, 304), new(336, 208)],
        111 => [new(80, 112), new(176, 144), new(144, 240), new(400, 304), new(304, 16)],
        112 => [new(208, 160), new(208, 304), new(400, 208), new(80, 368)],
        113 => [new(304, 80), new(208, 272), new(400, 208), new(48, 16), new(304, 368)],
        _ => [],
    };

    private static FootstepSurface FireFootstep(Map map, MapPosition position)
    {
        var region = (map.Id - 1) % 40; var layer = (map.Id - 1) / 40;
        var x = position.X * 32 + 16 + region % 6 * 352;
        var y = position.Y * 32 + 25 + region / 6 * 320;
        return EarthNoise(x / 43f, y / 33f, 41 + layer) > .65f ? FootstepSurface.Sand : FootstepSurface.Stone;
    }

    internal bool[] FireStone(Map map) { PrepareFire(map); return fireStone; }
    internal Point[] FireLights(Map map) { PrepareFire(map); return fireVents; }

    private void PrepareFire(Map map)
    {
        if (!IsFire(map)) return;
        var changed = !ReferenceEquals(fireMap, map);
        for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
        {
            var i = x * Map.Height + y; var tile = map.Tile(new(x, y));
            changed |= fireTiles[i] != tile; fireTiles[i] = tile;
        }
        if (!changed) return;
        fireMap = map; fireVisibility = null;
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * 352; var oy = row * 320;
        var neighbors = new Map?[3, 3]; neighbors[1, 1] = map;
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = catalog.CreateMap(layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1);
            if (next is not null && IsFire(next)) neighbors[dx + 1, dy + 1] = next;
        }
        bool Wall(int x, int y)
        {
            if (Map.Contains(new(x, y))) return FireWall(map.Tile(new(x, y)));
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0; var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? FireWall(next.Tile(new(x - dx * 11, y - dy * 10)))
                : FireWall(map.Tile(new(Math.Clamp(x, 0, 12), Math.Clamp(y, 0, 11))));
        }
        // Include nearby authored openings from adjoining rooms so overlap rock
        // and molten recesses have the same world-space appearance on both sides.
        var pools = new List<(Point Center, float Width, float Height)>();
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (neighbors[dx + 1, dy + 1] is not { } next) continue;
            foreach (var source in FireSources(next.Id))
            {
                var p = new Point(source.X + dx * 352, source.Y + dy * 320);
                var tx = p.X >> 5; var ty = p.Y >> 5;
                if (p.X < -64 || p.X >= FireWidth + 64 || p.Y < -64 || p.Y >= FireHeight + 64 || !Wall(tx, ty)) continue;
                var horizontal = Wall(tx - 1, ty) && Wall(tx + 1, ty);
                var vertical = Wall(tx, ty - 1) && Wall(tx, ty + 1);
                var size = layer == 2 ? 1.15f : 1;
                pools.Add((p, (horizontal ? 43 : 9) * size, (vertical ? 41 : 9) * size));
            }
        }
        fireVents = pools.Select(p => p.Center).Where(p => p.X >= 0 && p.X < FireWidth && p.Y >= 0 && p.Y < FireHeight).Distinct().ToArray();
        int[] ledge = [0, 1, 2, 3, 1, 0, 2, 1, 3, 2, 1, 0, 1, 2, 0, 1];
        var stone = fireStone = new bool[FireWidth * (FireHeight + 16)];
        for (var y = 0; y < FireHeight + 16; y++) for (var x = 0; x < FireWidth; x++) stone[y * FireWidth + x] = StoneAt(x, y);
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
        bool Stone(int x, int y) => x >= 0 && x < FireWidth && y >= 0 && y < FireHeight + 16
            ? stone[y * FireWidth + x] : StoneAt(x, y);

        ReadOnlySpan<int> dither = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        var ground = new Color[FireWidth * FireHeight];
        var lava = new Color[FireWidth * FireHeight * 8];
        fireMolten = new bool[ground.Length];
        for (var y = 0; y < FireHeight; y++) for (var x = 0; x < FireWidth; x++)
        {
            var i = y * FireWidth + x; var wx = x + ox; var wy = y + oy;
            var grain = EarthSeed(wx / 2, wy / 2, 77 + layer);
            var shade = 2;
            var pool = 9f;
            foreach (var opening in pools)
            {
                var dx = (x - opening.Center.X) / opening.Width; var dy = (y - opening.Center.Y) / opening.Height;
                pool = Math.Min(pool, MathF.Sqrt(dx * dx + dy * dy));
            }
            // Irregular cooling lip; all exposed magma remains inside blocked rock.
            pool += (EarthNoise(wx / 9f, wy / 7f, 67) - .5f) * .24f;
            if (!stone[i])
            {
                // Flat cooling plates are broader and quieter than the wall
                // facets. Their seams and folded flow lines stay world-anchored.
                var closest = float.MaxValue; var second = float.MaxValue;
                var plateSeed = 0; var plateY = 0;
                for (var sy = wy / 18 - 1; sy <= wy / 18 + 1; sy++) for (var sx = wx / 24 - 1; sx <= wx / 24 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 137 + layer);
                    var dx = wx - (sx * 24 + 4 + seed % 17); var dy = wy - (sy * 18 + 3 + seed / 17 % 12);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; plateSeed = seed; plateY = dy; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                shade = plateSeed % 3 == 0 ? 3 : 2;
                if (seam < .65f) shade = 1;
                else if (seam < 1.5f && plateY < 0) shade = 3;
                if (plateSeed % 3 == 0 && seam > 2 && Math.Abs(plateY) < 7)
                {
                    var fold = (plateY + (int)(Math.Sin(wx * .24f) * 2) + 64) % 7;
                    if (fold == 0) shade = 1;
                    else if (fold == 1) shade = 3;
                }
                var field = EarthNoise(wx / 43f, wy / 33f, 41 + layer);
                var threshold = (dither[(wy & 3) * 4 + (wx & 3)] + .5f) / 16f;
                if (threshold < Math.Clamp((field - .58f) * 5, 0, .85f)) shade = grain % 7 == 0 ? 4 : 3;
                var grit = EarthSeed(wx, wy, 151 + layer);
                if (grit % 113 == 0) shade = 4;
                else if (grit % 71 == 0) shade = 1;
                // A few sealed ember veins catch the light between the crust
                // plates; the thicker unstable-floor warnings are drawn last.
                if (seam < .55f && field < .64f && EarthNoise(wx / 39f, wy / 31f, 157 + layer) > .67f) shade = 10;
                var nearest = 9;
                for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
                    if (dx * dx + dy * dy < nearest && Stone(x + dx, y + dy)) nearest = dx * dx + dy * dy;
                if (nearest <= 2) shade = 0;
                else if (nearest <= 8) shade = 1;
            }
            else
            {
                var closest = float.MaxValue; var second = float.MaxValue; var facetY = 0;
                for (var sy = wy / 11 - 1; sy <= wy / 11 + 1; sy++) for (var sx = wx / 13 - 1; sx <= wx / 13 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 93 + layer);
                    var dx = wx - (sx * 13 + 3 + seed % 8); var dy = wy - (sy * 11 + 2 + seed / 7 % 7);
                    var d = dx * dx + dy * dy;
                    if (d < closest) { second = closest; closest = d; facetY = dy; }
                    else if (d < second) second = d;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                shade = facetY < 2 ? 7 : 6;
                if (seam is >= 1.1f and < 2 && facetY < -1) shade = 8;
                if (!Stone(x, y + 12 + ledge[(wx / 9) & 15])) shade = 5;
                // Incandescent joints show heat inside the rock. Keep the outer
                // silhouette dark so the wall still has depth against the floor.
                if (seam < 1.1f)
                {
                    var heat = EarthNoise(wx / 23f, wy / 19f, 103 + layer);
                    shade = pool < 1.8f || heat > .67f ? 21 : heat > .4f ? 20 : 19;
                }
                if (!Stone(x, y + 2)) shade = 0;
                if (pool < 1.3f && shade is 7 or 8) shade = 9;
                if (pool < 1.08f && Stone(x - 3, y) && Stone(x + 3, y) && Stone(x, y - 3) && Stone(x, y + 3))
                {
                    shade = pool > .86f ? 0 : 11;
                    if (pool < .87f)
                    {
                        fireMolten[i] = true;
                        for (var frame = 0; frame < 8; frame++)
                        {
                            var phase = frame * MathF.Tau / 8;
                            var crust = EarthNoise(wx / 9f + MathF.Sin(phase) * .7f, wy / 7f + MathF.Cos(phase) * .9f, 81);
                            var current = MathF.Sin(wx * .18f + wy * .12f - phase);
                            var hot = pool > .7f ? 12 : current > .55f ? 15 : current > -.2f ? 14 : 13;
                            if (crust > .59f) hot = crust > .68f ? 1 : 10;
                            if (crust is > .565f and < .59f && pool < .65f) hot = 16;
                            var index = (frame / 4 * FireHeight + y) * (FireWidth * 4) + frame % 4 * FireWidth + x;
                            lava[index] = FirePalette[hot];
                        }
                    }
                }
                if (pool is > 1.1f and < 1.5f && grain % 29 == 0) shade = 17;
            }
            ground[i] = FirePalette[shade];
        }
        // Readable unstable crust retains the original tile's warning and action.
        for (var y = 0; y < Map.Height; y++) for (var x = 0; x < Map.Width; x++)
        {
            var tile = map.Tile(new(x, y));
            if (tile is 0x29 or 0x2a)
                for (var k = 4; k < 29; k++)
                {
                    var px = x * 32 + 16 + k / 4 % 3 - 1; var py = y * 32 + k;
                    Put(px, py, 0); Put(px + 1, py, 18);
                    if (k is 12 or 22) for (var arm = 1; arm < 6; arm++) Put(px - arm, py - arm / 2, 0);
                }
            if (tile is not (0x28 or 0x48) || !Wall(x, y - 1)) continue;
            // Small cooled shards and ash at wall feet, leaving the centre clear.
            for (var chip = 0; chip < 3; chip++)
            {
                var seed = EarthSeed(x * 32 + ox + chip * 7, y * 32 + oy, 53);
                var px = x * 32 + 4 + seed % 23; var py = y * 32 + 3 + seed / 23 % 6;
                for (var k = 0; k < 2 + seed % 3; k++) { Put(px + k, py + 1, 0); Put(px + k, py, k == 1 ? 7 : 5); }
            }
        }
        fireGround.SetData(ground); fireLava.SetData(lava);
        void Put(int x, int y, int shade)
        {
            if (x >= 0 && x < FireWidth && y >= 0 && y < FireHeight && !stone[y * FireWidth + x]) ground[y * FireWidth + x] = FirePalette[shade];
        }
    }

    private void DrawFireGround(SpriteBatch batch, Rectangle bounds, double time)
    {
        batch.Draw(fireGround, bounds, bounds, Color.White);
        var frame = (int)(Math.Floor(time * 3) % 8);
        batch.Draw(fireLava, bounds, new Rectangle(bounds.X + frame % 4 * FireWidth, bounds.Y + frame / 4 * FireHeight, 32, 32), Color.White);
    }

    private float FireHeatAt(Vector2 position)
    {
        if (fireMap is null || fireVisibility is null) return 0;
        var cell = new MapPosition((int)position.X / 32, (int)position.Y / 32);
        if (!Map.Contains(cell) || !fireVisibility.Contains(cell)) return 0;
        var amount = fireMap.Id > 80 ? .18f : .1f;
        foreach (var point in fireVents)
        {
            if (!fireVisibility.Contains(new(point.X / 32, point.Y / 32))) continue;
            var distance = Vector2.Distance(position, point.ToVector2());
            amount = Math.Max(amount, (fireMap.Id > 80 ? 1.3f : 1.05f) * MathHelper.SmoothStep(0, 1, Math.Clamp(1 - distance / 78, 0, 1)));
        }
        return amount;
    }

    internal float FireAmbienceAt(Adventure game)
    {
        PrepareFire(game.Map); fireVisibility = game.Visibility;
        return .55f + (game.Map.Id > 80 ? .2f : 0) + FireHeatAt(new Vector2(game.Location.Position.X * 32 + 16,
            game.Location.Position.Y * 32 + 16)) * .3f;
    }

    private void DrawFireAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsFire(game.Map)) return;
        fireVisibility = game.Visibility;
        var clock = Math.Floor(time * 8) / 8;
        foreach (var point in fireVents)
        {
            if (!game.Visibility.Contains(new(point.X / 32, point.Y / 32))) continue;
            for (var i = 0; i < (game.Map.Id > 80 ? 6 : 4); i++)
            {
                var phase = (clock * (6 + i % 3) + i * 13 + point.X * .13) % 44;
                var x = point.X + (int)(Math.Sin(phase * .15 + i) * 7) + i * 3 - 7;
                var y = point.Y - (int)phase;
                Pixel(x, y, phase < 18 ? 15 : phase < 32 ? 13 : 10, 1);
                if (phase < 12) Pixel(x, y + 1, 12, 1);
            }
            // A soft stepped smoke ribbon intermittently rises from each vent.
            var breath = (clock + point.X * .03 + point.Y * .01) % 7;
            if (breath < 3.5)
                for (var i = 0; i < 9; i++)
                {
                    var y = point.Y - (int)(breath * 9) - i;
                    var x = point.X + (int)(Math.Sin(breath + i * .2) * 5);
                    Pixel(x, y, 6, 3, .12f); Pixel(x - 1, y, 6, 1, .08f);
                }
            var bubble = (clock + point.X * .017) % 4;
            if (bubble < .65)
            {
                var radius = 1 + (int)(bubble * 6);
                for (var a = 0; a < 12; a++)
                {
                    var angle = a * MathF.Tau / 12; var x = point.X + (int)(MathF.Cos(angle) * radius * 1.7f);
                    var y = point.Y + (int)(MathF.Sin(angle) * radius);
                    if (x >= 0 && x < FireWidth && y >= 0 && y < FireHeight && fireMolten[y * FireWidth + x]) Pixel(x, y, 16, 1);
                }
            }
        }
        for (var i = 0; i < (game.Map.Id > 80 ? 20 : 12); i++)
        {
            var seed = EarthSeed(i, game.Map.Id, 33);
            var y = (int)((clock * (2 + i % 3) + seed) % FireHeight);
            var x = 12 + seed % (FireWidth - 24) + (int)(Math.Sin(clock * .4 + i) * 5);
            Pixel(x, y, 18, 1, .5f);
        }
        void Pixel(int x, int y, int shade, int width, float opacity = 1)
        {
            for (var dx = 0; dx < width; dx++)
                if (x + dx >= 0 && x + dx < FireWidth && y >= 0 && y < FireHeight && game.Visibility.Contains(new((x + dx) / 32, y / 32)))
                    batch.Draw(pixel, new Rectangle(x + dx, y, 1, 1), FirePalette[shade] * opacity);
        }
    }
}
