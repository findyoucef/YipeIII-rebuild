using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int IceWidth = Map.Width * 32, IceHeight = Map.Height * 32;
    internal static readonly Color[] IcePalette =
    [
        new(14, 24, 35), new(24, 40, 55), new(37, 57, 76), new(43, 65, 83),
        new(49, 72, 91), new(55, 80, 98), new(66, 92, 108), new(82, 108, 121),
        new(37, 65, 89), new(49, 84, 111), new(65, 105, 132), new(86, 132, 158),
        new(112, 158, 180), new(144, 187, 202), new(177, 209, 218), new(202, 227, 233),
        new(59, 78, 95), new(79, 99, 112), new(107, 130, 143),
        new(61, 111, 141), new(106, 176, 198), new(176, 219, 228),
    ];
    private readonly Texture2D iceGround;
    private readonly ushort[] iceTiles = new ushort[Map.TileCount];
    private Map? iceMap;
    private bool[] iceStone = [];
    private byte[] icePolish = [];
    private readonly List<Point> iceCrystals = new(), iceFalls = new();

    internal static bool IsIce(Map map) => map.Id is 41 or 42 or 47 or 53 or 81 or 82 or 87 or 88 or 93 or 94 or 99;
    private static bool IceWall(ushort tile) => tile is 0x22 or 0x43 or 0x23 or 0x44;
    internal bool[] IceStone(Map map) { PrepareIce(map); return iceStone; }
    internal Point[] IceLights(Map map) { PrepareIce(map); return iceCrystals.ToArray(); }

    private void PrepareIce(Map map)
    {
        if (!IsIce(map)) return;
        var changed = !ReferenceEquals(iceMap, map);
        for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
        {
            var i = x * Map.Height + y; var tile = map.Tile(new(x, y));
            changed |= iceTiles[i] != tile; iceTiles[i] = tile;
        }
        if (!changed) return;
        iceMap = map; iceCrystals.Clear(); iceFalls.Clear();
        var layer = (map.Id - 1) / 40; var region = map.Id - layer * 40 - 1;
        var column = region % 6; var row = region / 6;
        var ox = column * 352; var oy = row * 320;
        var neighbors = new Map?[3, 3]; neighbors[1, 1] = map;
        for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = catalog.CreateMap(layer * 40 + (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1);
            if (next is not null && IsIce(next)) neighbors[dx + 1, dy + 1] = next;
        }
        bool Wall(int x, int y)
        {
            if (Map.Contains(new(x, y))) return IceWall(map.Tile(new(x, y)));
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0; var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1] is { } next
                ? IceWall(next.Tile(new(x - dx * 11, y - dy * 10)))
                : IceWall(map.Tile(new(Math.Clamp(x, 0, 12), Math.Clamp(y, 0, 11))));
        }
        int[] ledge = [0, 1, 2, 1, 3, 2, 1, 0, 1, 3, 2, 1, 2, 0, 1, 2];
        var rock = iceStone = new bool[IceWidth * (IceHeight + 16)];
        for (var y = 0; y < IceHeight + 16; y++) for (var x = 0; x < IceWidth; x++) rock[y * IceWidth + x] = RockAt(x, y);
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
        bool Rock(int x, int y) => x >= 0 && x < IceWidth && y >= 0 && y < IceHeight + 16
            ? rock[y * IceWidth + x] : RockAt(x, y);
        var data = new Color[IceWidth * IceHeight];
        icePolish = new byte[data.Length];
        for (var y = 0; y < IceHeight; y++) for (var x = 0; x < IceWidth; x++)
        {
            var i = y * IceWidth + x; var wx = x + ox; var wy = y + oy;
            var grain = EarthSeed(wx, wy, 353 + layer); var shade = 3;
            if (!rock[i])
            {
                // Broad frozen sheets, with occasional submerged fractures and scuffs.
                // Most of each plate is quiet; the walking floor is never crushed gravel.
                var closest = float.MaxValue; var second = float.MaxValue; var plate = 0; var plateY = 0;
                for (var sy = wy / 41 - 1; sy <= wy / 41 + 1; sy++) for (var sx = wx / 58 - 1; sx <= wx / 58 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 359 + layer);
                    var dx = wx - (sx * 58 + 8 + seed % 40); var dy = wy - (sy * 41 + 6 + seed / 17 % 28);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; plate = seed; plateY = dy; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                var frost = EarthNoise(wx / 67f, wy / 47f, 367 + layer);
                shade = frost > .58f ? 4 : 3;
                if (layer == 2 && frost < .42f) shade = 2;
                if (seam < .55f && plate % 3 == 0 && frost < .63f) shade = 2;
                else if (seam < 1.25f && plate % 3 == 0 && plateY < 0 && frost < .63f) shade = 5;
                if (plate % 5 == 0 && Math.Abs(plateY) < 9 && seam > 4)
                {
                    var scour = (plateY + wx / 5 % 2 + 96) % 11;
                    if (scour == 0) shade = 5;
                }
                // Packed frost rests in broad sheltered pockets, never square tile borders.
                if (frost > .68f) shade = 5;
                if (frost > .73f && grain % 5 == 0) shade = 6;
                if (grain % 419 == 0) shade = 5;
                var nearest = 26;
                for (var dy = -5; dy <= 5; dy++) for (var dx = -5; dx <= 5; dx++)
                    if (dx * dx + dy * dy < nearest && Rock(x + dx, y + dy)) nearest = dx * dx + dy * dy;
                if (nearest <= 2) shade = 0;
                else if (nearest <= 8) shade = 1;
                else if (nearest < 25 && frost > .52f && grain % 4 != 0) shade = 6;
                // Clear ice holds a quiet mirror; frost, scuffs and fractures roughen it.
                var tile = map.Tile(new(x / 32, y / 32));
                if (nearest > 8 && (tile is 0x53 or 0x49 || IceWall(tile)))
                    icePolish[i] = (byte)(shade >= 5 ? 1 : frost > .58f ? 2 : seam < 1.25f && plate % 3 == 0 ? 1 : 3);
            }
            else
            {
                // Narrow ice facets sit over a darker frozen rock mass. Vertical faces
                // and a few trapped streaks give the wall depth without a bright perimeter.
                var closest = float.MaxValue; var second = float.MaxValue; var facetY = 0; var facet = 0;
                for (var sy = wy / 15 - 1; sy <= wy / 15 + 1; sy++) for (var sx = wx / 13 - 1; sx <= wx / 13 + 1; sx++)
                {
                    var seed = EarthSeed(sx, sy, 373 + layer);
                    var dx = wx - (sx * 13 + 3 + seed % 8); var dy = wy - (sy * 15 + 3 + seed / 7 % 10);
                    var distance = dx * dx + dy * dy;
                    if (distance < closest) { second = closest; closest = distance; facetY = dy; facet = seed; }
                    else if (distance < second) second = distance;
                }
                var seam = MathF.Sqrt(second) - MathF.Sqrt(closest);
                shade = facetY < 0 ? 12 : 11;
                if (seam < .75f) shade = 9;
                else if (seam < 1.6f && facetY < -1) shade = 13;
                var front = !Rock(x, y + 12 + ledge[(wx / 9) & 15]);
                if (front) shade = seam < .75f ? 8 : facetY < 1 ? 10 : 9;
                var mineral = EarthNoise(wx / 39f, wy / 31f, 379 + layer);
                if (mineral < .29f || map.Tile(new(x / 32, y / 32)) is 0x23 or 0x44)
                    shade = front ? 16 : seam < .75f ? 16 : facetY < 0 ? 18 : 17;
                else if (facet % 4 == 0 && (wx + wy / 3) % 11 == 0 && seam > 2 && facetY < 3)
                    shade = front ? 11 : 14;
                if (!Rock(x - 1, y) || !Rock(x + 1, y) || !Rock(x, y + 1)) shade = 0;
                else if (!Rock(x, y - 1)) shade = 10;
            }
            data[i] = IcePalette[shade];
        }
        for (var y = 0; y < Map.Height - 1; y++) for (var x = 0; x < Map.Width; x++)
        {
            if (!Wall(x, y) || Wall(x, y + 1)) continue;
            var seed = EarthSeed(x + column * 11, y + row * 10, 383 + layer);
            if (seed % 4 == 0)
            {
                var point = new Point(x * 32 + 16, y * 32 + 21); iceCrystals.Add(point);
                // Three small, faceted crystals embedded in the visible face.
                for (var shard = 0; shard < 3; shard++)
                {
                    var px = point.X + (shard - 1) * 5; var height = shard == 1 ? 12 : 7;
                    for (var dy = 0; dy < height; dy++)
                    {
                        var half = dy < 3 ? dy / 2 : dy > height - 3 ? 1 : 2;
                        for (var dx = -half; dx <= half; dx++)
                            Put(px + dx, point.Y - height + dy + shard % 2 * 2, dx < 0 ? 19 : dx == 0 ? 21 : 20, true);
                    }
                }
            }
            var floor = new MapPosition(x, y + 1);
            if (map.Tile(floor) is not (0x53 or 0x49) || map.Events.Chests.Contains(floor)
                || map.Events.Depth.Any(d => d.HasPosition && d.Position == floor)
                || map.Events.Princess.HasPosition && map.Events.Princess.Position == floor) continue;
            if (seed % 3 == 0)
            {
                iceFalls.Add(new(x * 32 + 10, (y + 1) * 32 + 9));
                // Icicles hang from the face, leaving the walkable lane clear.
                for (var shard = 0; shard < 3; shard++)
                {
                    var px = x * 32 + 6 + shard * 8; var length = 4 + (seed / 7 + shard * 3) % 6;
                    for (var dy = 0; dy < length; dy++)
                    {
                        Put(px, y * 32 + 25 + dy, 12);
                        if (dy < length / 2) Put(px - 1, y * 32 + 25 + dy, 9);
                    }
                    Put(px + 1, (y + 1) * 32 + 7, 6); Put(px + 2, (y + 1) * 32 + 7, 5);
                }
            }
        }
        iceGround.SetData(data);
        void Put(int x, int y, int shade, bool onRock = false)
        {
            if (x < 0 || x >= IceWidth || y < 0 || y >= IceHeight) return;
            if (onRock && !rock[y * IceWidth + x]) return;
            data[y * IceWidth + x] = IcePalette[shade];
            icePolish[y * IceWidth + x] = 0;
        }
    }

    internal void DrawIceReflection(SpriteBatch batch, Adventure game, Texture2D texture, Rectangle source,
        Point feet, int scale = 1, bool facingLeft = false, int lift = 0)
    {
        if (!IsIce(game.Map)) return;
        var height = Math.Max(1, source.Height * scale * 2 / 3);
        var left = feet.X - source.Width * scale / 2;
        for (var column = 0; column < source.Width * scale; column++)
        {
            var x = left + column;
            if (x < 0 || x >= IceWidth) continue;
            var sx = source.X + (facingLeft ? source.Width - 1 - column / scale : column / scale);
            for (var row = 0; row < height + lift * scale; row++)
            {
                var y = feet.Y + 1 + row;
                if (y < 0 || y >= IceHeight || !game.Visibility.Contains(new(x / 32, y / 32))) break;
                var polish = icePolish[y * IceWidth + x];
                // A reflection cannot resume on the far side of an intervening wall or door.
                if (polish == 0) break;
                var reflectedRow = row - lift * scale;
                if (reflectedRow < 0) continue;
                var sy = source.Bottom - 1 - reflectedRow * source.Height / height;
                var tint = new Color(160, 204, 232) * (.38f * (1 - reflectedRow / (height + 3f)));
                batch.Draw(texture, new Rectangle(x, y, 1, 1), new Rectangle(sx, sy, 1, 1), tint * (polish / 3f));
            }
        }
    }

    private void DrawIceMonsterReflections(SpriteBatch batch, Adventure game, PlayerMotion hero, double time)
    {
        if (!IsIce(game.Map)) return;
        // The whole reflection pass precedes scenery and actors, preserving their depth order.
        foreach (var placement in game.ActivePlacements)
            if (game.Visibility.Contains(placement.Position) && HasMonsterAnimation(placement.TypeId))
                DrawMonsterActor(batch, placement.TypeId, new(placement.Position.X, placement.Position.Y),
                    placement.OccupiesTwoByTwoTiles ? 2 : 1, time, hero.VisualPosition.X < placement.Position.X,
                    reflection: game);
        for (var row = 0; row < Map.Height; row++) DrawRoamingActor(batch, game, row, time, reflection: true);
    }

    private void DrawIceAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsIce(game.Map)) return;
        var t = Math.Floor(time * 8) / 8;
        foreach (var point in iceCrystals)
        {
            if (!game.Visibility.Contains(new(point.X / 32, point.Y / 32))) continue;
            var phase = (t + point.X * .03 + point.Y * .07) % 6.7;
            if (phase < .5)
            {
                Pixel(point.X, point.Y - 9, IcePalette[15] * .75f, true);
                if (phase < .25) Pixel(point.X + 1, point.Y - 9, IcePalette[21] * .4f, true);
            }
        }
        for (var i = 0; i < iceFalls.Count; i++)
        {
            var point = iceFalls[i]; var phase = (t + i * 1.73) % 7.9;
            if (!game.Visibility.Contains(new(point.X / 32, point.Y / 32 - 1))) continue;
            if (phase < 1.2)
            {
                var y = point.Y - 17 + (int)(phase * phase / 1.44 * 17);
                Pixel(point.X, y, IcePalette[13] * (float)(.55 - phase * .25));
            }
            for (var mote = 0; mote < 3; mote++)
            {
                var age = (t + i * 1.31 + mote * 2.27) % 9;
                var x = point.X - 16 + (int)(age * 4) + mote * 3;
                var y = point.Y + 4 + mote * 6 + (int)(Math.Sin(age * .8 + i) * 2);
                Pixel(x, y, IcePalette[7] * (float)(Math.Sin(age / 9 * Math.PI) * .32));
            }
        }
        void Pixel(int x, int y, Color color, bool onRock = false)
        {
            if (x >= 0 && x < IceWidth && y >= 0 && y < IceHeight && iceStone[y * IceWidth + x] == onRock
                && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }
}
