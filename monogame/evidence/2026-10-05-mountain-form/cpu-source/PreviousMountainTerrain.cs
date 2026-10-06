using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Connected surface rock, compiled from the existing native mountain artwork.</summary>
internal sealed class PreviousMountainTerrain : IDisposable
{
    private const int Width = Map.Width * 32, Height = Map.Height * 32;
    private readonly MapCatalog catalog;
    private readonly Color[] rock = new Color[32 * 32];
    private readonly Color[] sand = new Color[64 * 64];
    private readonly ushort[] tiles = new ushort[Map.TileCount];
    private int mapId = -1;
    internal Texture2D Texture { get; }

    internal PreviousMountainTerrain(GraphicsDevice device, Texture2D terrain, MapCatalog catalog, Texture2D desert)
    {
        this.catalog = catalog;
        terrain.GetData(0, new Rectangle(32, 32, 32, 32), rock, 0, rock.Length);
        desert.GetData(sand);
        Texture = new Texture2D(device, Width, Height);
    }

    internal static bool IsMountain(ushort tile) => tile is 3 or 0x39 or 0x45;

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
        if (id is < 1 or > 36) { Texture.SetData(data); return; }

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
        bool Joins(int x, int y, int tx, int ty)
        {
            var tile = Tile(x, y);
            return IsMountain(tile) || tile switch
            {
                0x65 => x == tx - 1 && y == ty, 0x66 => x == tx && y == ty - 1,
                0x67 => x == tx + 1 && y == ty, 0x68 => x == tx && y == ty + 1,
                _ => false,
            };
        }

        ReadOnlySpan<int> ledge = [2, 2, 3, 3, 4, 4, 3, 2, 2, 1, 1, 2, 3, 3, 2, 2];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var tx = x / 32; var ty = y / 32;
                if (!IsMountain(tiles[ty * Map.Width + tx])) continue;
                var wx = (column * (Map.Width - 2) * 32 + x) % (6 * (Map.Width - 2) * 32);
                var wy = (row * (Map.Height - 2) * 32 + y) % (6 * (Map.Height - 2) * 32);
                var edge = 32f; var dry = false; var grass = false; var front = false;
                for (var cy = ty - 1; cy <= ty + 1; cy++)
                    for (var cx = tx - 1; cx <= tx + 1; cx++)
                    {
                        if (Joins(cx, cy, tx, ty)) continue;
                        var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 16);
                        var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 16);
                        var distance = MathF.Sqrt(dx * dx + dy * dy);
                        if (distance >= edge) continue;
                        edge = distance;
                        var neighbor = Tile(cx, cy);
                        dry = WorldRenderer.FullSand(neighbor) || neighbor is >= 0x1b and <= 0x1e;
                        grass = neighbor is 2 or 0x46 or 0x12 or 0x13 or 0x21 or >= 0x31 and <= 0x35 or 0x56;
                        front = cy > ty;
                    }
                for (var sy = -1; sy <= 1; sy += 2)
                    for (var sx = -1; sx <= 1; sx += 2)
                    {
                        if (Joins(tx + sx, ty, tx, ty) || Joins(tx, ty + sy, tx, ty)) continue;
                        const int radius = 12;
                        var dx = Math.Max(0, radius - (sx < 0 ? x % 32 + .5f : 31.5f - x % 32));
                        var dy = Math.Max(0, radius - (sy < 0 ? y % 32 + .5f : 31.5f - y % 32));
                        edge = Math.Min(edge, radius - MathF.Sqrt(dx * dx + dy * dy));
                    }
                var inset = ledge[(wx / 8 + wy / 8 * 3) & 15];
                var index = y * Width + x;
                var ground = sand[(wy & 31) * 64 + (wx & 31)];
                var patch = (wx / 5 * 7 + wy / 4 * 3) % 11;
                if (edge < inset)
                {
                    // Exposed desert corners reveal sand, not the meadow below it.
                    if (dry) data[index] = ground;
                    if (edge > 0 && patch == 0 && wx % 5 is 1 or 2 && wy % 4 >= 2)
                        data[index] = RetroArt.Palette[wy % 2 == 0 ? 3 : 2];
                    continue;
                }

                var color = rock[(wy & 31) * 32 + (wx & 31)];
                var foot = edge - inset;
                if (color == RetroArt.Palette[4] && foot < 5) color = RetroArt.Palette[3];
                if (front && foot < 5 && color == RetroArt.Palette[3]) color = RetroArt.Palette[39];

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
    }

    public void Dispose() => Texture.Dispose();
}
