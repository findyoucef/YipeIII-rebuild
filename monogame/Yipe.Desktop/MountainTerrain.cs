using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Connected native-pixel rock masses and cliff faces, independent of collision.</summary>
internal sealed class MountainTerrain : IDisposable
{
    private const int Width = Map.Width * 32, Height = Map.Height * 32;
    private readonly MapCatalog catalog;
    private readonly Color[] rock = new Color[32 * 32];
    // Local to the authored peaks: cool shaded slopes and warm sunlit ridges.
    // Other terrain and cave atlases retain their established colors.
    internal static readonly Color[] StonePalette =
    [new(50, 60, 74), RetroArt.Palette[2], new(76, 88, 103), new(90, 101, 115),
        RetroArt.Palette[3], new(123, 132, 140), new(141, 147, 149), new(160, 161, 155),
        new(179, 177, 165), new(198, 190, 173), new(215, 204, 185)];
    private readonly Color[] peaks = new Color[256 * 128];
    private readonly Rectangle[] peakBounds = new Rectangle[8];
    private readonly Color[] snowPeaks = new Color[256 * 256];
    private readonly Rectangle[] snowPeakBounds = new Rectangle[16];
    private readonly Color[] sand = new Color[64 * 64];
    private readonly ushort[] tiles = new ushort[Map.TileCount];
    private int mapId = -1;
    internal Texture2D Texture { get; }
    internal Texture2D Scree { get; }

    internal MountainTerrain(GraphicsDevice device, Texture2D terrain, MapCatalog catalog, Texture2D desert, string content)
    {
        this.catalog = catalog;
        terrain.GetData(0, new Rectangle(32, 32, 32, 32), rock, 0, rock.Length);
        desert.GetData(sand);
        foreach (var (name, rows, palette, pixels, bounds) in new[]
        {
            ("World/mountain-peaks.png", 2, StonePalette, peaks, peakBounds),
            ("World/mountain-peaks-snow.png", 4, StonePalette.Concat(RetroArt.SnowPalette).ToArray(), snowPeaks, snowPeakBounds),
        })
        {
            using var sheet = RetroArt.Load(device, content, name, 4, rows, 64, palette);
            sheet.GetData(pixels);
            for (var frame = 0; frame < bounds.Length; frame++)
            {
                // The source's magnified cell edges can contain disconnected pixels
                // from the adjacent row. Retain the actual rock body, not that fringe.
                var connected = new bool[64 * 64];
                var body = new Queue<Point>();
                body.Enqueue(new(32, 48));
                while (body.TryDequeue(out var point))
                {
                    if (point.X < 0 || point.X >= 64 || point.Y < 0 || point.Y >= 64) continue;
                    var local = point.Y * 64 + point.X;
                    var index = (frame / 4 * 64 + point.Y) * 256 + frame % 4 * 64 + point.X;
                    if (connected[local] || pixels[index].A == 0) continue;
                    connected[local] = true;
                    body.Enqueue(new(point.X - 1, point.Y)); body.Enqueue(new(point.X + 1, point.Y));
                    body.Enqueue(new(point.X, point.Y - 1)); body.Enqueue(new(point.X, point.Y + 1));
                }
                var left = 64; var top = 64; var right = 0; var bottom = 0;
                for (var y = 0; y < 64; y++)
                    for (var x = 0; x < 64; x++)
                        if (connected[y * 64 + x])
                        {
                            left = Math.Min(left, x); top = Math.Min(top, y);
                            right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                        }
                        else pixels[(frame / 4 * 64 + y) * 256 + frame % 4 * 64 + x] = Color.Transparent;
                if (left >= right || top >= bottom) throw new InvalidDataException($"{name}: mountain peak {frame} is empty.");
                bounds[frame] = new(frame % 4 * 64 + left, frame / 4 * 64 + top, right - left, bottom - top);
            }
        }
        Texture = new Texture2D(device, Width, Height);
        Scree = new Texture2D(device, Width, Height);
    }

    internal static bool IsMountain(ushort tile) => tile is 3 or 0x39 or 0x45;

    internal static Color SnowSoil(int x, int y)
    {
        var grain = Seed(x / 3, y / 2) % 17;
        return StonePalette[grain == 0 ? 5 : grain < 4 ? 3 : 4];
    }

    internal void Prepare(int id, Func<MapPosition, ushort> readTile)
    {
        var changed = mapId != id;
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var tile = readTile(new(x, y));
                var index = y * Map.Width + x;
                changed |= tiles[index] != tile; tiles[index] = tile;
            }
        if (!changed) return;
        mapId = id;
        var data = new Color[Width * Height];
        var scree = new Color[data.Length];
        if (id is < 1 or > 36) { Texture.SetData(data); Scree.SetData(scree); return; }

        var column = (id - 1) % 6; var row = (id - 1) / 6;
        var neighbors = new Map?[3, 3];
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                if (dx != 0 || dy != 0)
                {
                    var neighborId = (row + dy + 6) % 6 * 6 + (column + dx + 6) % 6 + 1;
                    neighbors[dx + 1, dy + 1] = catalog.CreateMap(neighborId)
                        ?? throw new InvalidDataException($"Missing surface mountain neighbor {neighborId}.");
                }

        // Adjacent regions overlap by two cells. Read beyond the current map from
        // the next region, and anchor texture/ledge variation in that shared space.
        ushort Tile(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Map.Width && y < Map.Height) return tiles[y * Map.Width + x];
            var dx = x < 0 ? -1 : x >= Map.Width ? 1 : 0;
            var dy = y < 0 ? -1 : y >= Map.Height ? 1 : 0;
            return neighbors[dx + 1, dy + 1]!.Tile(new(x - dx * (Map.Width - 2), y - dy * (Map.Height - 2)));
        }

        // Include the full two-cell transition radius of offscreen peak anchors
        // so neighboring regions choose the same complete winter sprite.
        var snowCells = new List<Rectangle>();
        for (var y = -3; y < Map.Height + 3; y++)
            for (var x = -3; x < Map.Width + 3; x++)
                if (WorldRenderer.SnowTile(Tile(x, y))) snowCells.Add(new(x * 32, y * 32, 32, 32));

        bool Joins(int x, int y, int tx, int ty)
        {
            // Maynard's castle and cave sit against the northern backdrop. Carry
            // those ridges above the view instead of cutting a rim of distant ground.
            if (id == TownMap.Id && ty == 0 && y < 0) return true;
            var tile = Tile(x, y);
            return IsMountain(tile) || tile switch
            {
                0x65 => x == tx - 1 && y == ty, 0x66 => x == tx && y == ty - 1,
                0x67 => x == tx + 1 && y == ty, 0x68 => x == tx && y == ty + 1,
                _ => false,
            };
        }

        (float Edge, ushort Ground) Contour(int x, int y)
        {
            var tx = (int)MathF.Floor(x / 32f); var ty = (int)MathF.Floor(y / 32f);
            if (!IsMountain(Tile(tx, ty))) return (-32, 0);
            var px = x - tx * 32 + .5f; var py = y - ty * 32 + .5f;
            var distance = 32f;
            ushort ground = 0;
            for (var cy = ty - 1; cy <= ty + 1; cy++)
                for (var cx = tx - 1; cx <= tx + 1; cx++)
                {
                    if (Joins(cx, cy, tx, ty)) continue;
                    var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 16);
                    var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 16);
                    var edge = MathF.Sqrt(dx * dx + dy * dy);
                    if (edge >= distance) continue;
                    distance = edge; ground = Tile(cx, cy);
                }
            for (var sy = -1; sy <= 1; sy += 2)
                for (var sx = -1; sx <= 1; sx += 2)
                {
                    if (Joins(tx + sx, ty, tx, ty) || Joins(tx, ty + sy, tx, ty)) continue;
                    // A bend in a connected ridge uses nearly the whole cell.
                    // Thin tips keep a smaller radius so no blocked cell vanishes.
                    var radius = Joins(tx - sx, ty, tx, ty) && Joins(tx, ty - sy, tx, ty) ? 29 : 17;
                    var dx = Math.Max(0, radius - (sx < 0 ? px : 32 - px));
                    var dy = Math.Max(0, radius - (sy < 0 ? py : 32 - py));
                    distance = Math.Min(distance, radius - Math.Max(dx, dy) - Math.Min(dx, dy) * .5f);
                }
            return (distance, ground);
        }

        // Compose overlapping authored peaks back to front. Neighboring regions
        // use the same world-cell seed and include the same offscreen contributors.
        // Only this cached art layer changes; the original terrain remains the mask.
        var slopes = new Color[Width * Height];
        for (var ty = -1; ty <= Map.Height; ty++)
            for (var tx = -1; tx <= Map.Width; tx++)
            {
                if (!IsMountain(Tile(tx, ty))) continue;
                var seed = Seed((column * (Map.Width - 2) + tx + 66) % 66,
                    (row * (Map.Height - 2) + ty + 60) % 60);
                var north = Joins(tx, ty - 1, tx, ty);
                var frame = (int)(seed % 8);
                var source = peakBounds[frame];
                var pixels = peaks;
                var distanceSquared = 64 * 64;
                foreach (var cell in snowCells)
                {
                    var x = tx * 32 + 16; var y = ty * 32 + 16;
                    var dx = Math.Max(0, Math.Max(cell.Left - x, x - cell.Right + 1));
                    var dy = Math.Max(0, Math.Max(cell.Top - y, y - cell.Bottom + 1));
                    distanceSquared = Math.Min(distanceSquared, dx * dx + dy * dy);
                }
                // Whole authored sprites: light dusting outside the basin, deep
                // wind-shaped snow nearer its floor. No runtime snow recoloring.
                if (distanceSquared < 64 * 64)
                {
                    source = snowPeakBounds[frame + (distanceSquared < 24 * 24 ? 8 : 0)];
                    pixels = snowPeaks;
                }
                var width = 40 + (int)(seed >> 4 & 15);
                var height = north ? 40 + (int)(seed >> 8 & 15) : 26 + (int)(seed >> 8 & 3);
                if (id == TownMap.Id && ty == 0) { width += 16; height += 24; }
                var left = tx * 32 + 16 - width / 2 + (int)(seed >> 12 & 7) - 3;
                var top = ty * 32 + (north ? 32 - height : 1);
                for (var py = 0; py < height; py++)
                    for (var px = 0; px < width; px++)
                    {
                        var x = left + px; var y = top + py;
                        if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                        var color = pixels[(source.Y + py * source.Height / height) * 256
                            + source.X + px * source.Width / width];
                        if (color.A == 0) continue;
                        slopes[y * Width + x] = color;
                    }
            }

        ReadOnlySpan<int> ledge = [2, 2, 3, 3, 4, 4, 3, 2, 2, 1, 1, 2, 3, 3, 2, 2];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var tx = x / 32; var ty = y / 32;
                if (!IsMountain(tiles[ty * Map.Width + tx])) continue;
                var wx = (column * (Map.Width - 2) * 32 + x) % (6 * (Map.Width - 2) * 32);
                var wy = (row * (Map.Height - 2) * 32 + y) % (6 * (Map.Height - 2) * 32);
                var (edge, neighbor) = Contour(x, y);
                var dry = WorldRenderer.FullSand(neighbor) || neighbor is >= 0x1b and <= 0x1e;
                var snowFoot = WorldRenderer.SnowTile(neighbor);
                var grass = neighbor is 2 or 0x46 or 0x12 or 0x13 or >= 0x31 and <= 0x35 or 0x56;
                var inset = 1 + ledge[(wx / 16 + wy / 16 * 3) & 15] / 2;
                if (snowFoot)
                    inset += 2 + (int)MathF.Round(MathF.Sin(wx * .071f + wy * .09f) * 2);
                var index = y * Width + x;
                var ground = sand[(wy & 31) * 64 + (wx & 31)];
                var patch = (wx / 5 * 7 + wy / 4 * 3) % 11;
                var solidCore = x % 32 is >= 12 and < 20 && y % 32 is >= 12 and < 20;
                if (edge < inset && !(snowFoot && solidCore))
                {
                    // Exposed desert corners reveal sand, not the meadow below it.
                    if (dry) data[index] = ground;
                    else if (snowFoot) scree[index] = SnowSoil(wx, wy);
                    if (edge > 0 && patch == 0 && wx % 5 is 1 or 2 && wy % 4 >= 2)
                        data[index] = RetroArt.Palette[wy % 2 == 0 ? 3 : 2];
                    continue;
                }

                var foot = edge - inset;
                var color = slopes[index];
                // Exposed summits break the upper outline. Between connected peaks,
                // a quiet stone backing keeps blocked centers visibly solid.
                var meetsFoothill = x % 32 < 4 && Tile(tx - 1, ty) == 0x65
                    || x % 32 >= 28 && Tile(tx + 1, ty) == 0x67;
                if (color.A == 0 && !meetsFoothill && !Joins(tx, ty - 1, tx, ty) && y % 32 < 12)
                {
                    if (dry) data[index] = ground;
                    else if (snowFoot) scree[index] = SnowSoil(wx, wy);
                    continue;
                }
                if (color.A == 0)
                    color = StonePalette[rock[(wy & 31) * 32 + (wx & 31)] == RetroArt.Palette[2] ? 1 : 2];
                var shade = Array.IndexOf(StonePalette, color);
                // A few one-tone chips break up the long slopes without replacing
                // their authored forms with the old repeated pebble texture.
                var grain = rock[(wy & 31) * 32 + (wx & 31)];
                if (shade > 3 && grain == RetroArt.Palette[2] && Seed(wx / 7, wy / 5) % 5 == 0) shade--;
                if (shade >= 0) color = StonePalette[foot < 3 ? Math.Min(shade, 3) : shade];

                // Grass and sand overlap small parts of the rock foot, rather than
                // tracing its perimeter. The solid middle and cave mouth stay clear.
                if (tiles[ty * Map.Width + tx] != 0x39 && (grass || dry) && edge < 7)
                {
                    var reach = patch < 3 ? 3 : patch < 6 ? 1 : 0;
                    if (foot < reach)
                        color = dry ? ground : RetroArt.Palette[wx % 5 == 1 && wy % 4 > 0 ? 10 : 11];
                    else if (grass && patch < 3 && foot < reach + 2 && color != RetroArt.Palette[3])
                        color = RetroArt.Palette[10];
                }
                data[index] = color;
            }
        Texture.SetData(data);
        Scree.SetData(scree);
    }

    private static uint Seed(int x, int y)
    {
        var bits = unchecked((uint)(x * 374761393 + y * 668265263 + 127));
        bits = unchecked((bits ^ bits >> 13) * 1274126177);
        return bits ^ bits >> 16;
    }

    public void Dispose() { Texture.Dispose(); Scree.Dispose(); }
}
