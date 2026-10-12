using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    // Mineral earth and exposed slate stay quieter than the wall tops.
    internal static readonly Color[] BreedingSoilPalette =
        [new(32, 35, 43), new(45, 47, 54), new(52, 54, 60), new(67, 70, 77), new(59, 62, 68)];
    private readonly Texture2D breedingGround;
    private readonly ushort[] breedingTiles = new ushort[Map.TileCount];
    private Map? breedingMap;
    private bool[] breedingStone = [];
    private Point[] breedingFungi = [], breedingDrips = [];

    internal static bool IsBreeding(Map map) => map.Id is 59 or 60 or 65 or 66 or 105 or 106;
    private static bool BreedingWall(ushort tile) => tile is 0x23 or 0x44;

    internal bool[] BreedingStone(Map map)
    {
        PrepareBreeding(map);
        return breedingStone;
    }

    // Shared with the light pass, so each pool of light has a visible source.
    internal static Point[] BreedingFungi(int id) => id switch
    {
        59 => [new(103, 25), new(329, 246), new(209, 310)],
        60 => [new(258, 25), new(391, 163), new(168, 188)],
        65 => [new(194, 83), new(107, 25), new(265, 236)],
        66 => [new(23, 111), new(390, 193), new(256, 373)],
        105 => [new(113, 84), new(274, 25), new(301, 212)],
        106 => [new(100, 150), new(392, 184), new(206, 373)],
        _ => [],
    };

    internal static Point[] BreedingLamps(int id) => id switch
    {
        60 => [new(112, 178)], 65 => [new(329, 148)],
        66 => [new(362, 373)], 105 => [new(179, 212)],
        106 => [new(75, 275)], _ => [],
    };

    private void PrepareBreeding(Map map)
    {
        if (!IsBreeding(map)) return;
        var changed = !ReferenceEquals(breedingMap, map);
        for (var x = 0; x < Map.Width; x++)
            for (var y = 0; y < Map.Height; y++)
            {
                var i = x * Map.Height + y; var tile = map.Tile(new(x, y));
                changed |= breedingTiles[i] != tile; breedingTiles[i] = tile;
            }
        if (!changed) return;
        breedingMap = map;
        const int width = Map.Width * 32, height = Map.Height * 32;
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * (Map.Width - 2) * 32; var oy = row * (Map.Height - 2) * 32;
        var neighbors = new Map?[3, 3];
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                if (dx != 0 || dy != 0)
                {
                    var id = layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1;
                    var next = catalog.CreateMap(id);
                    if (next is not null && IsBreeding(next)) neighbors[dx + 1, dy + 1] = next;
                }
        bool Wall(int x, int y)
        {
            if (Map.Contains(new(x, y))) return BreedingWall(map.Tile(new(x, y)));
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0;
            var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? BreedingWall(next.Tile(new(x - dx * (Map.Width - 2), y - dy * (Map.Height - 2))))
                : BreedingWall(map.Tile(new(Math.Clamp(x, 0, Map.Width - 1), Math.Clamp(y, 0, Map.Height - 1))));
        }
        int[] ledge = [0, 1, 3, 3, 2, 0, 0, 1, 2, 4, 3, 1, 0, 0, 2, 1];
        // Contours and texture use world coordinates across the two-tile region overlaps.
        var stone = breedingStone = new bool[width * (height + 16)];
        for (var y = 0; y < height + 16; y++)
            for (var x = 0; x < width; x++)
                stone[y * width + x] = StoneAt(x, y);

        bool StoneAt(int x, int y)
        {
            var tx = x >> 5; var ty = y >> 5; var edge = 32f;
            if (!Wall(tx, ty)) return false;
            for (var cy = ty - 1; cy <= ty + 1; cy++)
                for (var cx = tx - 1; cx <= tx + 1; cx++)
                {
                    if (Wall(cx, cy)) continue;
                    var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 16);
                    var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 16);
                    edge = Math.Min(edge, MathF.Sqrt(dx * dx + dy * dy));
                }
            for (var sy = -1; sy <= 1; sy += 2)
                for (var sx = -1; sx <= 1; sx += 2)
                {
                    if (Wall(tx + sx, ty) || Wall(tx, ty + sy)) continue;
                    const float radius = 8;
                    var dx = Math.Max(0, radius - (sx < 0 ? (x & 31) + .5f : 31.5f - (x & 31)));
                    var dy = Math.Max(0, radius - (sy < 0 ? (y & 31) + .5f : 31.5f - (y & 31)));
                    edge = Math.Min(edge, radius - dx - dy);
                }
            var inset = 1 + ledge[((x + ox) / 7 + (y + oy) / 9 * 3) & 15];
            return edge > inset;
        }

        var data = new Color[width * height];
        ReadOnlySpan<int> dither = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x; var wx = x + ox; var wy = y + oy;
                var color = BreedingSoilPalette[2];
                if (!stone[i])
                {
                    // Broad slate beds with earth worn into their shallow joints.
                    // Interrupted seams leave larger stretches of intact ground.
                    var broad = EarthNoise(wx / 61f, wy / 47f, 37 + layer);
                    var dust = EarthNoise(wx / 23f, wy / 17f, 23 + layer);
                    var threshold = (dither[(wy & 3) * 4 + (wx & 3)] + .5f) / 16f;
                    var shade = dust < .5f ? 1 : 2;
                    if (broad + (threshold - .5f) * .06f > .39f)
                    {
                        var closest = float.MaxValue; var second = float.MaxValue;
                        var slabSeed = 0; var slabX = 0; var slabY = 0;
                        for (var sy = wy / 25 - 1; sy <= wy / 25 + 1; sy++)
                            for (var sx = wx / 36 - 1; sx <= wx / 36 + 1; sx++)
                            {
                                var seed = EarthSeed(sx, sy, 149 + layer);
                                var dx = wx - (sx * 36 + 5 + seed % 27);
                                var dy = wy - (sy * 25 + 4 + seed / 27 % 18);
                                var skew = dx + dy * .3f;
                                var distance = skew * skew + dy * dy * 1.4f;
                                if (distance < closest)
                                {
                                    second = closest; closest = distance;
                                    slabSeed = seed; slabX = dx; slabY = dy;
                                }
                                else if (distance < second) second = distance;
                            }
                        var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                        shade = 4;
                        if (seam < .65f && dust > .43f) shade = 1;
                        else if (seam < 1.2f && dust > .52f) shade = 2;
                        // Shallow cleavage marks break the flat faces without
                        // the bright perimeter or front face of a raised wall.
                        else if (seam > 3 && Math.Abs(slabX) < 7 && slabSeed % 5 == 0
                            && slabY + (slabX + 12) / 6 == 2) shade = 2;
                        if (seam > 2 && dust > .63f) shade = 2;
                    }
                    else
                    {
                        var seed = EarthSeed(wx / 19, wy / 13, 157 + layer);
                        var dx = wx % 19 - 3 - seed % 9;
                        var dy = wy % 13 - 3 - seed / 9 % 7;
                        if (seed % 3 == 0 && Math.Abs(dx) < 4 && dy + (dx + 4) / 4 == 0) shade = 1;
                    }
                    var grain = EarthSeed(wx, wy, 53 + layer);
                    if (grain % 131 == 0) shade = 1;
                    else if (dust > .52f && grain % 97 == 0) shade = 3;
                    color = BreedingSoilPalette[shade];
                    // A short, stepped contact shadow follows the actual chipped
                    // rock, including cut corners, rather than the square map cell.
                    var nearest = 9;
                    for (var dy = -2; dy <= 2; dy++)
                        for (var dx = -2; dx <= 2; dx++)
                        {
                            var distance = dx * dx + dy * dy;
                            if (distance >= nearest) continue;
                            var px = x + dx; var py = y + dy;
                            var rock = px >= 0 && px < width && py >= 0 && py < height + 16
                                ? stone[py * width + px] : StoneAt(px, py);
                            if (rock) nearest = distance;
                        }
                    if (nearest <= 2) color = BreedingSoilPalette[0];
                    else if (nearest <= 8) color = BreedingSoilPalette[1];
                }
                if (stone[i])
                {
                    var depth = 11 + ledge[(wx / 9) & 15];
                    var front = !stone[(y + depth) * width + x];
                    // Match Hell's 13x11 native-pixel stone scale and narrow joints.
                    // Anchor it in world space so adjoining cave regions agree.
                    var closest = float.MaxValue; var second = float.MaxValue;
                    var facetY = 0;
                    for (var sy = wy / 11 - 1; sy <= wy / 11 + 1; sy++)
                        for (var sx = wx / 13 - 1; sx <= wx / 13 + 1; sx++)
                        {
                            var seed = EarthSeed(sx, sy, 93 + layer);
                            var dx = wx - (sx * 13 + 3 + seed % 8);
                            var dy = wy - (sy * 11 + 2 + seed / 7 % 7);
                            var distance = dx * dx + dy * dy;
                            if (distance < closest)
                            {
                                second = closest; closest = distance; facetY = dy;
                            }
                            else if (distance < second) second = distance;
                        }
                    var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                    var shade = seam < 1.1f ? 1 : facetY < 2 ? 3 : 2;
                    if (seam is >= 1.1f and < 2 && facetY < -1) shade = 4;
                    if (front) shade = seam < 1.1f ? 1 : 2;
                    if (!stone[(y + 2) * width + x]) shade = 1;
                    color = RetroArt.Palette[shade];
                }
                data[i] = color;
            }

        // Loose chips at wall feet tie the rock to the earth. Keep central lanes quiet.
        for (var y = 1; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (!Wall(x, y) && Wall(x, y - 1))
                    for (var chip = 0; chip < 3; chip++)
                    {
                        var seed = EarthSeed(x * 32 + ox + chip * 7, y * 32 + oy, 63);
                        var px = x * 32 + 4 + seed % 23; var py = y * 32 + 3 + seed / 23 % 7;
                        var size = 1 + seed % 3;
                        Line(px - 1, py + 1, px + size + 1, py + 1, 14);
                        Line(px, py, px + size, py, 2);
                        if (size > 1) Line(px + 1, py - 1, px + size, py - 1, 3);
                    }

        // Details are authored per chamber, baked at native resolution and clipped
        // to ordinary floor. No new obstacles or event cells are introduced.
        switch (map.Id)
        {
            case 60: // Arrival: discarded shells and a worn approach to the existing stair.
                Nest(279, 73, 22, 2); Nest(343, 270, 20, 0);
                Bones(317, 197); Scales(244, 291); Scratches(387, 111);
                for (var i = 0; i < 7; i++)
                {
                    var x = 79 + i * 8; var y = 269 + i % 2 * 4;
                    Line(x, y, x + 2, y - 2, 14); Line(x + 4, y, x + 6, y - 2, 14);
                }
                break;
            case 59: // Communal hollows, with mixed shell sizes.
                Nest(88, 70, 24, 3); Nest(228, 75, 28, 2); Nest(277, 244, 24, 1);
                Nest(97, 184, 20, 0); Bones(217, 265); Scales(176, 75); Scratches(26, 172);
                break;
            case 65: // Existing prisoner alcove and guard bedding.
                Nest(95, 89, 25, 0); Nest(267, 63, 19, 1); Scales(148, 245);
                for (var i = 0; i < 36; i++)
                {
                    var seed = EarthSeed(i, 17, 65); var x = 326 + seed % 20; var y = 197 + seed / 20 % 12;
                    Line(x, y, x + 3 + i % 4, y + i % 5 - 2, i % 3 == 0 ? 17 : 38);
                }
                Scratches(271, 194); Scratches(393, 165); Bones(170, 306);
                break;
            case 66: // Feeding corner: a broad trampled hollow, bones and discarded shells.
                Nest(303, 103, 33, 0); Bones(292, 99); Bones(325, 120); Bones(269, 117);
                Nest(87, 225, 19, 1); Scales(342, 268); Scratches(391, 82);
                break;
            case 105: // Brood chamber above the existing locked passage.
                Nest(178, 63, 24, 3); Nest(303, 72, 31, 3); Nest(337, 132, 21, 2);
                Nest(274, 298, 26, 1); Scales(268, 109); Bones(146, 282); Scratches(111, 48);
                break;
            case 106: // Oversized bedding and claw marks around the large monsters.
                Nest(293, 73, 37, 1); Nest(286, 252, 34, 0);
                Bones(309, 260); Bones(267, 270); Scales(308, 115);
                Scratches(389, 82); Scratches(390, 241); Scratches(93, 272);
                break;
        }
        breedingDrips = map.Id switch
        {
            59 => [new(67, 124), new(288, 191)], 60 => [new(338, 60), new(270, 243)],
            65 => [new(159, 132), new(249, 287)], 66 => [new(96, 79), new(327, 208)],
            105 => [new(247, 144), new(79, 289)], _ => [new(203, 72), new(344, 308)],
        };
        foreach (var point in breedingDrips)
        {
            Oval(point.X, point.Y, 12, 4, 14);
            Oval(point.X, point.Y - 1, 10, 3, 1);
            Line(point.X - 5, point.Y - 3, point.X + 4, point.Y - 3, 2);
        }
        breedingFungi = BreedingFungi(map.Id);
        foreach (var point in breedingFungi)
            for (var cap = 0; cap < 3; cap++)
            {
                var x = point.X + cap * 5 - 5; var y = point.Y + cap % 2 * 3;
                Line(x, y - 2, x, y + 3, 39, wall: true);
                Line(x - 3, y - 2, x + 3, y - 2, 27, wall: true);
                Line(x - 2, y - 3, x + 2, y - 3, 29, wall: true);
                Line(x - 1, y - 4, x + 1, y - 4, 30, wall: true);
            }
        breedingGround.SetData(data);

        bool Floor(int x, int y)
        {
            var cell = new MapPosition(x / 32, y / 32);
            return map.Tile(cell) is 0x28 or 0x48 && !map.Events.Chests.Contains(cell)
                && !map.Events.Depth.Any(d => d.HasPosition && d.Position == cell)
                && (!map.Events.Message.HasPosition || map.Events.Message.Position != cell)
                && (!map.Events.Princess.HasPosition || map.Events.Princess.Position != cell);
        }
        void Put(int x, int y, int shade, bool wall = false)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            if (wall ? !stone[y * width + x] : !Floor(x, y)) return;
            data[y * width + x] = !wall && shade is 14 or 15
                ? BreedingSoilPalette[shade - 14] : RetroArt.Palette[shade];
        }
        void Line(int x, int y, int endX, int endY, int shade, bool wall = false)
        {
            var steps = Math.Max(Math.Abs(endX - x), Math.Abs(endY - y));
            for (var step = 0; step <= steps; step++)
            {
                var t = steps == 0 ? 0 : step / (float)steps;
                Put((int)MathF.Round(MathHelper.Lerp(x, endX, t)), (int)MathF.Round(MathHelper.Lerp(y, endY, t)), shade, wall);
            }
        }
        void Oval(int x, int y, int rx, int ry, int shade)
        {
            for (var dy = -ry; dy <= ry; dy++)
                for (var dx = -rx; dx <= rx; dx++)
                    if (dx * dx * ry * ry + dy * dy * rx * rx <= rx * rx * ry * ry) Put(x + dx, y + dy, shade);
        }
        void Nest(int x, int y, int radius, int eggs)
        {
            Oval(x, y + 3, radius + 2, radius / 2 + 1, 14);
            Oval(x, y, radius, radius / 2, 38);
            Oval(x, y - 1, radius - 5, radius / 2 - 4, 14);
            for (var straw = 0; straw < radius * 3; straw++)
            {
                var angle = straw * 2.39996f; var r = radius - straw % 5;
                var px = x + (int)(MathF.Cos(angle) * r); var py = y + (int)(MathF.Sin(angle) * r * .47f);
                Line(px, py, px + (straw % 7 - 3), py + straw % 3 - 1, straw % 4 == 0 ? 17 : 16);
            }
            for (var egg = 0; egg < eggs; egg++)
            {
                var ex = x - eggs * 4 + egg * 9 + 3; var ey = y - 2 + egg % 2 * 3;
                Oval(ex, ey + 1, 5, 3, 1); Oval(ex, ey - 3, 4, 6, 3);
                Oval(ex - 1, ey - 4, 3, 5, 5); Line(ex - 1, ey - 7, ex, ey - 7, 7);
                Put(ex + 1, ey - 4, 39); Put(ex, ey - 1, 39);
            }
            // A jagged half shell makes the empty nests readable too.
            Line(x + radius - 6, y + 9, x + radius, y + 9, 3);
            Line(x + radius - 5, y + 8, x + radius - 1, y + 8, 5);
            Put(x + radius - 5, y + 6, 5); Put(x + radius - 2, y + 7, 5);
        }
        void Bones(int x, int y)
        {
            for (var bone = 0; bone < 3; bone++)
            {
                var px = x + bone * 6 - 6; var py = y + bone % 2 * 5;
                Line(px, py + 2, px + 10, py - 2, 14);
                Line(px, py + 1, px + 9, py - 3, 3);
                Line(px + 1, py, px + 8, py - 3, 5);
                Put(px, py - 1, 5); Put(px + 9, py - 4, 5);
            }
        }
        void Scales(int x, int y)
        {
            for (var scale = 0; scale < 8; scale++)
            {
                var px = x + scale * 3; var py = y + (int)(Math.Sin(scale * .8) * 3);
                Line(px, py, px + 2, py + 2, 1); Line(px, py - 1, px + 2, py + 1, scale % 3 == 0 ? 39 : 2);
            }
        }
        void Scratches(int x, int y)
        {
            for (var claw = 0; claw < 3; claw++)
            {
                Line(x + claw * 4, y, x + claw * 4 - 3, y + 12, 1, wall: true);
                Line(x + claw * 4 + 1, y + 1, x + claw * 4 - 2, y + 12, 3, wall: true);
            }
        }
    }

    private void DrawBreedingAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsBreeding(game.Map)) return;
        var clock = Math.Floor(time * 8) / 8;
        for (var i = 0; i < breedingDrips.Length; i++)
        {
            var point = breedingDrips[i]; var phase = (clock + i * 2.1) % 5.3;
            if (game.Map.Tile(new(point.X / 32, point.Y / 32)) is not (0x28 or 0x48)) continue;
            if (phase < .75)
            {
                var y = point.Y - 22 + (int)(phase / .75 * 22);
                Pixel(point.X, y, 26); Pixel(point.X, y - 1, 2);
            }
            else if (phase < 1.75)
            {
                var radius = (int)((phase - .75) * 9);
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * MathF.Tau / 16;
                    Pixel(point.X + (int)(MathF.Cos(angle) * radius), point.Y - 1 + (int)(MathF.Sin(angle) * radius * .3f), 2);
                }
            }
        }
        for (var i = 0; i < breedingFungi.Length; i++)
        {
            var point = breedingFungi[i]; var phase = (clock + i * 2.7) % 8;
            if (!BreedingWall(game.Map.Tile(new(point.X / 32, point.Y / 32)))) continue;
            if (phase < 3)
                Pixel(point.X + (int)(Math.Sin(clock + i) * 5), point.Y - 6 - (int)(phase * 4), 39);
        }
        for (var i = 0; i < 6; i++)
        {
            var phase = (clock + i * 2.3) % 11;
            if (phase > 1.4) continue;
            var x = 48 + EarthSeed(i, 17, game.Map.Id) % 320;
            var y = 48 + EarthSeed(i, 29, game.Map.Id) % 256 + (int)(phase * 8);
            if (!BreedingWall(game.Map.Tile(new(x / 32, y / 32)))) Pixel(x, y, 16);
        }
        void Pixel(int x, int y, int shade)
        {
            if (x >= 0 && y >= 0 && x < Map.Width * 32 && y < Map.Height * 32
                && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), shade == 16 ? BreedingSoilPalette[3] : RetroArt.Palette[shade]);
        }
    }
}
