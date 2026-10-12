using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int WaterWidth = Map.Width * 32;
    private const int WaterHeight = Map.Height * 32;
    private readonly Color[] waterPattern = new Color[32 * 32];
    private readonly Color[] waterPixels = new Color[WaterWidth * WaterHeight];
    private readonly bool[] waterMask = new bool[WaterWidth * WaterHeight];
    private readonly float[] shoreDistance = new float[WaterWidth * WaterHeight];
    private readonly ushort[] waterTiles = new ushort[Map.TileCount];
    private Map? waterMap;

    private Texture2D CreateWater(GraphicsDevice device)
    {
        // Use the exact compiled water texture that gives Maynard its painted pixel detail.
        var source = new Color[outdoors.Width * outdoors.Height];
        outdoors.GetData(source);
        for (var y = 0; y < 32; y++)
            Array.Copy(source, (y + 32) * outdoors.Width, waterPattern, y * 32, 32);
        return new Texture2D(device, WaterWidth, WaterHeight);
    }

    private static ushort WaterTile(ushort tile) => tile switch
    {
        0x0b or 0x59 => 0x0e, 0x58 => 0x11,
        >= 0x0c and <= 0x15 or >= 0x4a and <= 0x51 => tile,
        _ => 0,
    };

    internal bool WaterAt(int x, int y) => x >= 0 && y >= 0 && x < WaterWidth && y < WaterHeight
        && waterMask[y * WaterWidth + x];

    internal bool HasPuddles => waterMap is { } map && IsEarth(map);

    private void PrepareWater(Map map)
    {
        var changed = !ReferenceEquals(waterMap, map);
        var puddles = IsEarth(map);
        var flooded = IsWaterCave(map);
        var region = (map.Id - 1) % 40;
        var ox = flooded ? region % 6 * 352 : 0; var oy = flooded ? region / 6 * 320 : 0;
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var index = y * Map.Width + x;
                // Puddle clipping also depends on the dry terrain underneath it.
                var tile = map.Tile(new(x, y));
                if (!puddles) tile = WaterTile(tile);
                changed |= waterTiles[index] != tile;
                waterTiles[index] = tile;
            }
        if (!changed) return;
        waterMap = map;
        Array.Clear(waterPixels);
        // One map-wide shoreline prevents seams and artificial banks between adjacent water tiles.
        for (var y = 0; y < WaterHeight; y++)
            for (var x = 0; x < WaterWidth; x++)
            {
                var tile = WaterTile(waterTiles[y / 32 * Map.Width + x / 32]);
                var px = x % 32 + .5f; var py = y % 32 + .5f;
                var ripple = MathF.Sin((x + ox) * .35f + (y + oy) * .19f) * 1.3f
                    + MathF.Sin((x + ox) * .13f - (y + oy) * .41f) * .6f;
                var radius = 7.5f + ripple;
                bool Bend(float dx, float dy) => MathF.Abs(MathF.Sqrt(dx * dx + dy * dy) - 16) < radius;
                waterMask[y * WaterWidth + x] = tile switch
                {
                    0x0c or 0x0d => true,
                    0x0e => MathF.Abs(px - 16) < radius,
                    0x11 => MathF.Abs(py - 16) < radius,
                    0x0f => Bend(32 - px, 32 - py), 0x10 => Bend(px, py),
                    0x14 => Bend(px, 32 - py), 0x15 => Bend(32 - px, py),
                    0x12 => MathF.Abs(px - 16) < radius + MathF.Max(0, py - 18),
                    0x13 => MathF.Abs(px - 16) < radius + MathF.Max(0, 14 - py),
                    0x4a => Vector2.Distance(new(px, py), new(32, 32)) < 30 + ripple,
                    0x4b => Vector2.Distance(new(px, py), new(0, 32)) < 30 + ripple,
                    0x4c => Vector2.Distance(new(px, py), new(32, 0)) < 30 + ripple,
                    0x4d => Vector2.Distance(new(px, py), Vector2.Zero) < 30 + ripple,
                    0x4e => px >= 3 + ripple, 0x4f => px < 29 + ripple,
                    0x50 => py >= 3 + ripple, 0x51 => py < 29 + ripple,
                    _ => false,
                };
            }

        if (puddles) AddEarthPuddles(map);
        ShoreDistance(toWater: false);
        if (flooded)
        {
            // Water nibbles at irregular limestone banks. World anchoring keeps
            // overlapping room edges aligned; tile centers keep their original material.
            for (var y = 0; y < WaterHeight; y++) for (var x = 0; x < WaterWidth; x++)
            {
                var i = y * WaterWidth + x;
                if (!waterMask[i] || waterTiles[y / 32 * Map.Width + x / 32] is not (0x0c or 0x0d)) continue;
                var inset = 1.5f + EarthNoise((x + ox) / 13f, (y + oy) / 17f, 449) * 4;
                if (shoreDistance[i] < inset) waterMask[i] = false;
            }
            ShoreDistance(toWater: false);
        }
        for (var y = 0; y < WaterHeight; y++)
            for (var x = 0; x < WaterWidth; x++)
            {
                var index = y * WaterWidth + x;
                if (!waterMask[index]) continue;
                var color = waterPattern[y % 32 * 32 + x % 32];
                var depth = shoreDistance[index];
                if (!flooded && waterTiles[y / 32 * Map.Width + x / 32] is 0x0c or 0x0d
                    && depth < 1.8f + MathF.Sin((x + ox) * .35f + (y + oy) * .19f) * .8f)
                {
                    waterMask[index] = false;
                    continue;
                }
                if (puddles)
                {
                    // Still cave water reflects brown rock, with broad quiet
                    // patches instead of the river's dense blue wave texture.
                    var stone = EarthNoise(x / 17f, y / 8f, map.Id + 31);
                    color = stone > .6f ? new Color(76, 67, 54) : new Color(59, 52, 45);
                    if (stone > .77f) color = new Color(91, 80, 62);
                    if (depth < 2) color = RetroArt.Palette[14];
                    if (depth < 2 && y > 0 && !waterMask[index - WaterWidth]
                        && (x / 4 + y / 2) % 3 == 0) color = new Color(145, 128, 97);
                }
                else if (flooded)
                {
                    var pool = EarthNoise((x + ox) / 53f, (y + oy) / 31f, 443);
                    color = WaterCavePalette[depth < 5 ? 19 : pool > .62f ? 18 : 17];
                    if (depth < 2) color = WaterCavePalette[20];
                    else if (depth < 4 && (x + ox + (y + oy) * 3) % 7 == 0) color = WaterCavePalette[21];
                }
                else if (depth <= 1) color = RetroArt.Palette[28];
                else if (depth < 3 && (x + y * 3) % 5 == 0) color = RetroArt.Palette[29];
                waterPixels[index] = color;
            }
        ShoreDistance(toWater: true);
        var surface = map.Id < 41;
        for (var y = 0; y < WaterHeight; y++)
            for (var x = 0; x < WaterWidth; x++)
            {
                var index = y * WaterWidth + x;
                if (waterMask[index]) continue;
                var distance = shoreDistance[index];
                if (distance > 3) continue;
                var color = surface
                    ? RetroArt.Palette[distance < 1.5f ? 16 : (x / 3 + y / 4) % 3 == 0 ? 10 : 9]
                    : RetroArt.Palette[0] * (distance < 1.5f ? .45f : .2f);
                if (puddles) color = RetroArt.Palette[14] * (distance < 1.5f ? .55f : .2f);
                else if (flooded) color = WaterCavePalette[13] * (distance < 1.5f ? .6f : .25f);
                else if (distance < 2 && (x * 13 + y * 7) % 37 == 0) color = RetroArt.Palette[surface ? 3 : 2];
                waterPixels[index] = color;
            }
        water.SetData(waterPixels);
    }

    private void ShoreDistance(bool toWater)
    {
        // Two chamfer passes find nearby banks in linear time, once per changed water layout.
        for (var i = 0; i < shoreDistance.Length; i++) shoreDistance[i] = waterMask[i] == toWater ? 0 : 1000;
        for (var y = 0; y < WaterHeight; y++)
            for (var x = 0; x < WaterWidth; x++)
            {
                var i = y * WaterWidth + x;
                if (x > 0) shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i - 1] + 1);
                if (y == 0) continue;
                shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i - WaterWidth] + 1);
                if (x > 0) shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i - WaterWidth - 1] + 1.4142f);
                if (x < WaterWidth - 1) shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i - WaterWidth + 1] + 1.4142f);
            }
        for (var y = WaterHeight - 1; y >= 0; y--)
            for (var x = WaterWidth - 1; x >= 0; x--)
            {
                var i = y * WaterWidth + x;
                if (x < WaterWidth - 1) shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i + 1] + 1);
                if (y == WaterHeight - 1) continue;
                shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i + WaterWidth] + 1);
                if (x > 0) shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i + WaterWidth - 1] + 1.4142f);
                if (x < WaterWidth - 1) shoreDistance[i] = Math.Min(shoreDistance[i], shoreDistance[i + WaterWidth + 1] + 1.4142f);
            }
    }

    private void DrawWater(SpriteBatch batch, Rectangle bounds, double time)
    {
        batch.Draw(water, bounds, bounds, Color.White);
        var tile = WaterTile(waterTiles[bounds.Y / 32 * Map.Width + bounds.X / 32]);
        if (tile == 0 && !HasPuddles) return;
        var puddles = HasPuddles;
        var flooded = waterMap is { } map && IsWaterCave(map);
        var tick = (int)(time * (puddles ? 3 : 6));
        var seed = bounds.X / 32 * 13 + bounds.Y / 32 * 7;
        for (var i = 0; i < 2; i++)
        {
            var travel = (seed + i * 19 + tick) % 32;
            var point = tile switch
            {
                0x0e or 0x12 or 0x13 => new Point(14 + (seed + i) % 4, travel),
                0x11 => new Point(travel, 14 + (seed + i) % 4),
                0x0f or 0x10 or 0x14 or 0x15 => BendPoint(tile, travel),
                _ => new Point(travel, 4 + (seed * 3 + i * 17) % 24),
            };
            var horizontal = tile != 0x11;
            var wave = new Rectangle(bounds.X + point.X, bounds.Y + point.Y, horizontal ? 4 : 1, horizontal ? 1 : 4);
            if (Fits(wave)) batch.Draw(pixel, wave, (flooded ? WaterCavePalette[21] : RetroArt.Palette[puddles ? 18 : 29])
                * (flooded ? .22f : puddles ? .25f : .6f));
            if (puddles || flooded) continue;
            if ((seed + i) % 3 != 0) continue;
            var foam = new Rectangle(wave.X + 4, wave.Y + 3, 2, 1);
            if (Fits(foam)) batch.Draw(pixel, foam, RetroArt.Palette[30] * .4f);
        }
        bool Fits(Rectangle area)
        {
            if (!bounds.Contains(area)) return false;
            for (var y = area.Top; y < area.Bottom; y++)
                for (var x = area.Left; x < area.Right; x++)
                    if (!WaterAt(x, y)) return false;
            return true;
        }
        static Point BendPoint(ushort tile, int travel)
        {
            var angle = travel / 32f * MathF.PI / 2;
            var x = (int)MathF.Round(16 * MathF.Cos(angle));
            var y = (int)MathF.Round(16 * MathF.Sin(angle));
            return new(tile is 0x0f or 0x14 ? 32 - x : x, tile is 0x0f or 0x15 ? 32 - y : y);
        }
    }
}
