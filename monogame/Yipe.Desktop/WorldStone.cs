using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int StoneWidth = Map.Width * 32, StoneHeight = Map.Height * 32;
    internal static readonly Color[] StonePalette =
    [
        new(25, 29, 33), new(38, 44, 48), new(49, 55, 59), new(55, 61, 64),
        new(63, 69, 72), new(73, 79, 80), new(48, 58, 65), new(71, 83, 90),
        new(96, 108, 113), new(115, 127, 129), new(137, 145, 144), new(159, 164, 155),
        new(86, 112, 111), new(43, 51, 52),
    ];
    private readonly Texture2D stoneGround;
    private readonly ushort[] stoneTiles = new ushort[Map.TileCount];
    private Map? stoneMap;
    private bool[] stoneRock = [];
    private readonly List<Point> stoneLamps = new(), stoneDrips = new();

    internal static bool IsStone(Map map) => map.Id is 51 or 52 or 58 or 64 or 70 or 76 or 104 or 110 or 116;
    private static bool StoneWall(ushort tile) => tile is 0x23 or 0x44;
    internal bool[] StoneRock(Map map) { PrepareStone(map); return stoneRock; }
    internal Point[] StoneLamps(Map map) { PrepareStone(map); return stoneLamps.ToArray(); }

    private void PrepareStone(Map map)
    {
        if (!IsStone(map)) return;
        var changed = !ReferenceEquals(stoneMap, map);
        for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
        {
            var i = x * Map.Height + y; var tile = map.Tile(new(x, y));
            changed |= stoneTiles[i] != tile; stoneTiles[i] = tile;
        }
        if (!changed) return;
        stoneMap = map; stoneLamps.Clear(); stoneDrips.Clear(); PrepareWater(map);
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * 352; var oy = row * 320;
        var neighbors = new Map?[3, 3]; neighbors[1, 1] = map;
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = catalog.CreateMap(layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1);
            if (next is not null && IsStone(next)) neighbors[dx + 1, dy + 1] = next;
        }
        bool Wall(int x, int y)
        {
            if (Map.Contains(new(x, y))) return StoneWall(map.Tile(new(x, y)));
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0; var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? StoneWall(next.Tile(new(x - dx * 11, y - dy * 10)))
                : StoneWall(map.Tile(new(Math.Clamp(x, 0, 12), Math.Clamp(y, 0, 11))));
        }
        int[] ledge = [0, 1, 3, 2, 1, 0, 2, 1, 3, 1, 0, 1, 2, 3, 1, 0];
        var rock = stoneRock = new bool[StoneWidth * (StoneHeight + 16)];
        for (var y = 0; y < StoneHeight + 16; y++) for (var x = 0; x < StoneWidth; x++) rock[y * StoneWidth + x] = RockAt(x, y);
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
        bool Rock(int x, int y) => x >= 0 && x < StoneWidth && y >= 0 && y < StoneHeight + 16
            ? rock[y * StoneWidth + x] : RockAt(x, y);
        var data = new Color[StoneWidth * StoneHeight];
        for (var y = 0; y < StoneHeight; y++) for (var x = 0; x < StoneWidth; x++)
        {
            var i = y * StoneWidth + x; var wx = x + ox; var wy = y + oy;
            var grain = EarthSeed(wx, wy, 263 + layer);
            var shade = 2;
            if (!rock[i])
            {
                // Worn bedrock shelves: broad, nearly level surfaces, with only
                // selected shallow joints. Keep the walking lane quiet.
                var closest = float.MaxValue; var second = float.MaxValue; var plate = 0; var plateY = 0;
                for (var sy = wy / 31 - 1; sy <= wy / 31 + 1; sy++) for (var sx = wx / 43 - 1; sx <= wx / 43 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 271 + layer);
                    var dx = wx - (sx * 43 + 7 + seed % 29); var dy = wy - (sy * 31 + 5 + seed / 17 % 21);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; plate = seed; plateY = dy; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                var sediment = EarthNoise(wx / 53f, wy / 39f, 277 + layer);
                shade = sediment > .58f ? 3 : 2;
                if (seam < .6f && plate % 4 == 0 && plateY > 0 && sediment < .64f) shade = 1;
                else if (seam < 1.4f && plate % 4 == 0 && plateY is > 0 and < 8) shade = 3;
                if (plate % 7 == 0 && Math.Abs(plateY) < 8 && seam > 3)
                {
                    var scour = (plateY + (int)(Math.Sin(wx * .16f) * 2) + 64) % 9;
                    if (scour == 0) shade = 1;
                    else if (scour == 1) shade = 3;
                }
                // Mineral sand settles into hollows; nearby water leaves silt.
                if (sediment > .68f && grain % 7 == 0) shade = 4;
                if (shoreDistance[i] < 9) shade = grain % 5 == 0 ? 2 : 13;
                if (grain % 193 == 0) shade = 4;
                else if (grain % 271 == 0) shade = 1;
                var nearest = 9;
                for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
                    if (dx * dx + dy * dy < nearest && Rock(x + dx, y + dy)) nearest = dx * dx + dy * dy;
                if (nearest <= 2) shade = 0;
                else if (nearest <= 8) shade = 1;
            }
            else
            {
                // Dense crystalline stone, with finer facets than the floor and
                // a dark front face following the actual visible silhouette.
                var closest = float.MaxValue; var second = float.MaxValue; var facetY = 0;
                for (var sy = wy / 11 - 1; sy <= wy / 11 + 1; sy++) for (var sx = wx / 13 - 1; sx <= wx / 13 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 281 + layer);
                    var dx = wx - (sx * 13 + 3 + seed % 8); var dy = wy - (sy * 11 + 2 + seed / 7 % 7);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; facetY = dy; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                shade = facetY < 2 ? 9 : 8;
                if (seam < .8f) shade = 6;
                else if (seam < 1.7f && facetY < -1) shade = 10;
                var front = !Rock(x, y + 11 + ledge[(wx / 9) & 15]);
                if (front) shade = seam < .8f ? 1 : facetY < 2 ? 7 : 6;
                // Sparse mineral seams catch light; they are not emissive.
                var vein = EarthNoise(wx / 43f, wy / 29f, 283 + layer);
                if (!front && vein is > .496f and < .505f && EarthNoise(wx / 83f, wy / 61f, 293) > .58f)
                    shade = grain % 4 == 0 ? 12 : 11;
                if (!Rock(x - 1, y) || !Rock(x + 1, y) || !Rock(x, y + 1)) shade = 0;
                else if (!Rock(x, y - 1)) shade = 7;
                if (!front && grain % 139 == 0) shade = 10;
            }
            data[i] = StonePalette[shade];
        }
        for (var y = 0; y < Map.Height - 1; y++) for (var x = 0; x < Map.Width; x++)
        {
            if (!Wall(x, y) || Wall(x, y + 1)) continue;
            var seed = EarthSeed(x + column * 11, y + row * 10, 307 + layer);
            if (seed % 6 == 0 || map.Id == 104 && x == 3 && y == 3 || map.Id == 58 && x == 7 && y == 3)
                stoneLamps.Add(new(x * 32 + 16, y * 32 + 20));
            var floor = new MapPosition(x, y + 1);
            if (map.Tile(floor) is not (0x27 or 0x47)) continue;
            // Talus stays at the wall foot, outside routes and event footprints.
            if (map.Events.Chests.Contains(floor) || map.Events.Depth.Any(d => d.HasPosition && d.Position == floor)
                || map.Events.Princess.HasPosition && map.Events.Princess.Position == floor) continue;
            for (var chip = 0; chip < 3; chip++)
            {
                var px = x * 32 + 4 + (seed + chip * 7) % 23; var py = (y + 1) * 32 + 3 + chip % 2 * 3;
                var width = 1 + (seed + chip) % 3;
                for (var dx = -1; dx <= width; dx++) Put(px + dx, py + 1, 0);
                for (var dx = 0; dx < width; dx++) Put(px + dx, py, chip == 1 ? 5 : 4);
                if (width > 1) Put(px + 1, py - 1, 8);
            }
            if (seed % 5 == 0)
            {
                var point = new Point(x * 32 + 16, (y + 1) * 32 + 11); stoneDrips.Add(point);
                for (var dy = -2; dy <= 2; dy++) for (var dx = -7; dx <= 7; dx++)
                    if (dx * dx / 49f + dy * dy / 4f < 1) Put(point.X + dx, point.Y + dy, 13);
            }
        }
        stoneGround.SetData(data);
        void Put(int x, int y, int shade)
        {
            if (x < 0 || x >= StoneWidth || y < 0 || y >= StoneHeight || rock[y * StoneWidth + x]) return;
            data[y * StoneWidth + x] = StonePalette[shade];
        }
    }

    private void DrawStoneAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsStone(game.Map)) return;
        var t = Math.Floor(time * 8) / 8;
        foreach (var point in stoneLamps)
            for (var mote = 0; mote < 5; mote++)
            {
                var phase = (t + mote * 1.71 + point.X * .03) % 8;
                var x = point.X - 14 + mote * 7 + (int)(Math.Sin(t + mote) * 2);
                var y = point.Y + 7 + (int)(phase * 3);
                Pixel(x, y, StonePalette[10] * (float)(Math.Sin(phase / 8 * Math.PI) * .35));
            }
        for (var i = 0; i < stoneDrips.Count; i++)
        {
            var point = stoneDrips[i]; var phase = (t + i * 1.47) % 5.3;
            if (phase < .7)
            {
                var y = point.Y - 22 + (int)(phase * phase / .49 * 22);
                Pixel(point.X, y, StonePalette[10] * .65f); Pixel(point.X, y - 1, StonePalette[9] * .25f);
            }
            else if (phase < 1.7)
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * MathF.Tau / 16; var radius = (float)(phase - .7) * 7;
                    Pixel(point.X + (int)(MathF.Cos(angle) * radius), point.Y + (int)(MathF.Sin(angle) * radius * .35f),
                        StonePalette[9] * (float)((1.7 - phase) * .4));
                }
            var grit = (t + i * 3.31) % 14;
            if (grit < 1)
                for (var chip = 0; chip < 3; chip++)
                    Pixel(point.X - 7 + chip * 3, point.Y - 21 + (int)(grit * grit * 21) - chip,
                        StonePalette[5] * (float)((1 - grit) * .7));
        }
        void Pixel(int x, int y, Color color)
        {
            if (x >= 0 && x < StoneWidth && y >= 0 && y < StoneHeight && !stoneRock[y * StoneWidth + x]
                && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }
}
