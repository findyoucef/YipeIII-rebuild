using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int SandWidth = Map.Width * 32, SandHeight = Map.Height * 32;
    private readonly ushort[] sandTiles = new ushort[Map.TileCount];
    private readonly Color[] sandPattern = new Color[64 * 64];
    private readonly Color[] sandPixels = new Color[SandWidth * SandHeight];
    private readonly float[] sandField = new float[SandWidth * SandHeight];
    private readonly float[] sandBlur = new float[SandWidth * SandHeight];
    private readonly bool[] sandClear = new bool[SandWidth * SandHeight];
    private readonly List<Rectangle> tumbleRuns = new();
    private Map? sandMap;
    private bool desertActive;

    private Texture2D CreateSand(GraphicsDevice device)
    {
        desert.GetData(sandPattern);
        return new Texture2D(device, SandWidth, SandHeight);
    }

    internal static bool FullSand(ushort tile) => tile is 0x2c or 0x2d or 0x2e or 0x2f or 0x36 or 0x42 or 0x57
        or >= 0x65 and <= 0x68;

    internal bool HasHeat => sandMap is { } hotMap && IsFire(hotMap) || sandMap is { Id: >= 117 and <= 120 }
        || sandMap is { Id: < 41 } && desertActive;

    internal float HeatAt(Vector2 position)
    {
        if (!HasHeat) return 0;
        if (IsFire(sandMap!)) return FireHeatAt(position);
        if (sandMap!.Id >= 117) return 1.15f;
        var x = Math.Clamp((int)position.X, 0, SandWidth - 1);
        var y = Math.Clamp((int)position.Y, 0, SandHeight - 1);
        // Reuse the continuous sand footprint so small patches cannot heat a whole region.
        // Water remains calm, and the field fades naturally into neighboring grass/rock.
        if (WaterAt(x, y)) return 0;
        return .75f * MathHelper.SmoothStep(0, 1, sandField[y * SandWidth + x]);
    }

    private void PrepareDesert(Map map)
    {
        var changed = !ReferenceEquals(sandMap, map);
        var hasSand = false;
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var tile = map.Tile(new(x, y));
                var index = y * Map.Width + x;
                changed |= sandTiles[index] != tile;
                sandTiles[index] = tile;
                hasSand |= FullSand(tile) || tile is >= 0x1b and <= 0x1e;
            }
        if (!changed) return;
        sandMap = map; desertActive = hasSand;
        tumbleRuns.Clear(); Array.Clear(sandClear);
        if (!hasSand) return;
        // Begin with the authored sand footprint, then round and weather its boundary
        // across the whole map. Sampling clamped map edges keeps continuations open.
        for (var y = 0; y < SandHeight; y++)
            for (var x = 0; x < SandWidth; x++)
            {
                var tile = sandTiles[y / 32 * Map.Width + x / 32];
                var px = x % 32; var py = y % 32;
                var inside = tile switch
                {
                    0x1b => px + py >= 31, 0x1c => px <= py,
                    0x1d => px >= py, 0x1e => px + py <= 31,
                    _ => FullSand(tile),
                };
                sandField[y * SandWidth + x] = inside ? 1 : 0;
            }
        const int radius = 12, span = radius * 2 + 1;
        for (var y = 0; y < SandHeight; y++)
        {
            var sum = 0f;
            for (var x = -radius; x <= radius; x++) sum += sandField[y * SandWidth + Math.Clamp(x, 0, SandWidth - 1)];
            for (var x = 0; x < SandWidth; x++)
            {
                sandBlur[y * SandWidth + x] = sum / span;
                sum += sandField[y * SandWidth + Math.Min(SandWidth - 1, x + radius + 1)]
                    - sandField[y * SandWidth + Math.Max(0, x - radius)];
            }
        }
        for (var x = 0; x < SandWidth; x++)
        {
            var sum = 0f;
            for (var y = -radius; y <= radius; y++) sum += sandBlur[Math.Clamp(y, 0, SandHeight - 1) * SandWidth + x];
            for (var y = 0; y < SandHeight; y++)
            {
                sandField[y * SandWidth + x] = sum / span;
                sum += sandBlur[Math.Min(SandHeight - 1, y + radius + 1) * SandWidth + x]
                    - sandBlur[Math.Max(0, y - radius) * SandWidth + x];
            }
        }
        Array.Clear(sandPixels);
        for (var y = 0; y < SandHeight; y++)
            for (var x = 0; x < SandWidth; x++)
            {
                var index = y * SandWidth + x;
                var tile = sandTiles[y / 32 * Map.Width + x / 32];
                if (WaterTile(tile) != 0 || tile is 3 or 9 or 0x39 or 0x45) continue;
                var gx = map.Id <= 36 ? Mod((map.Id - 1) % 6 * 352 + x, 2112) : x;
                var gy = map.Id <= 36 ? Mod((map.Id - 1) / 6 * 320 + y, 1920) : y;
                var weathering = map.Id <= 36
                    ? MathF.Sin(gx * MathF.Tau * 15 / 2112 + gy * MathF.Tau * 18 / 1920) * .10f
                        + MathF.Sin(gx * MathF.Tau * 57 / 2112 - gy * MathF.Tau * 34 / 1920) * .035f
                    : MathF.Sin(x * .045f + y * .061f) * .10f + MathF.Sin(x * .17f - y * .11f) * .035f;
                var coverage = Math.Clamp((sandField[index] - .5f + weathering) * 3.5f + .5f, 0, 1);
                if (sandField[index] == 0) coverage = 0;
                var grain = map.Id <= 36 ? OutdoorHash(gx, gy) % 23 / 23f : (x * 13 + y * 7 + x * y * 3) % 23 / 23f;
                var variant = tile == 0x2c ? 3 : (gx / 32 + gy / 32) % 2;
                var color = sandPattern[(variant / 2 * 32 + y % 32) * 64 + variant % 2 * 32 + x % 32];
                if (map.Id <= 36)
                {
                    var field = MeadowField(gx, gy);
                    // Slightly quieter sand between worn, wind-combed patches, using the existing sand art.
                    if (field > .57f + grain * .06f) color = Color.Lerp(color, RetroArt.Palette[18], .25f);
                    else if (field < .31f + grain * .05f) color = Color.Lerp(color, RetroArt.Palette[17], .10f);
                }
                // A broken dry-grass fringe replaces the ruler-straight contrast line.
                if (coverage > grain) sandPixels[index] = coverage < .65f && grain > coverage * .6f
                    ? RetroArt.Palette[(gx + gy * 3) % 3 == 0 ? 12 : 13] : color;
                sandClear[index] = coverage > .94f && tile is 2 or 0x46 or 0x2d or 0x42 or >= 0x1b and <= 0x1e;
            }
        sandLayer.SetData(sandPixels);
        foreach (var chest in map.Events.Chests) KeepClear(chest);
        foreach (var stairs in map.Events.Depth) KeepClear(stairs.Position);
        KeepClear(map.Events.Message.Position);
        if (map.Events.Princess is { } princess) KeepClear(princess.Position);
        void KeepClear(MapPosition position)
        {
            if (!Map.Contains(position)) return;
            for (var y = Math.Max(0, position.Y * 32 - 12); y < Math.Min(SandHeight, position.Y * 32 + 32); y++)
                for (var x = Math.Max(0, position.X * 32 - 8); x < Math.Min(SandWidth, position.X * 32 + 40); x++)
                    sandClear[y * SandWidth + x] = false;
        }
        // Only continuous, obstacle-free sand lanes can carry a rolling weed.
        for (var y = 48 + map.Id % 3 * 11; y < SandHeight - 16; y += 73)
        {
            var start = -1;
            for (var x = 8; x <= SandWidth - 8; x++)
            {
                var clear = x < SandWidth - 8;
                for (var dy = -12; dy <= 0 && clear; dy++)
                    for (var dx = -6; dx <= 6 && clear; dx++) clear = SandClearAt(x + dx, y + dy);
                if (clear && start < 0) start = x;
                if (clear || start < 0) continue;
                if (x - start >= 72) tumbleRuns.Add(new(start, y, x - start, 1));
                start = -1;
            }
        }
        tumbleRuns.Sort((a, b) => b.Width.CompareTo(a.Width));
        if (tumbleRuns.Count > 2) tumbleRuns.RemoveRange(2, tumbleRuns.Count - 2);
    }

    private bool SandClearAt(int x, int y) => x >= 0 && y >= 0 && x < SandWidth && y < SandHeight
        && sandClear[y * SandWidth + x];

    private void DrawDesertWind(SpriteBatch batch, Adventure game, double time)
    {
        if (!desertActive || game.Map.Id >= 41) return;
        var windTime = Math.Floor(time * 8) / 8;
        // Broad, faint puffs make the air move without veiling the scene or its actors.
        for (var i = 0; i < 5; i++)
        {
            var centerX = (int)((i * 113 + game.Map.Id * 47 + windTime * 9) % (SandWidth + 40)) - 20;
            var centerY = 24 + (i * 79 + game.Map.Id * 31) % (SandHeight - 48);
            centerY += (int)Math.Round(Math.Sin(windTime * .5 + i) * 5);
            var gust = (float)(.07 + .07 * (1 + Math.Sin(windTime * .7 + i * 1.9)));
            for (var dy = -5; dy <= 5; dy++)
                for (var dx = -20; dx <= 20; dx++)
                {
                    var density = 1 - dx * dx / 400f - dy * dy / 25f;
                    if (density <= 0) continue;
                    var x = centerX + dx; var y = centerY + dy;
                    if (!SandClearAt(x, y) || !game.Visibility.Contains(new(x / 32, y / 32))) continue;
                    batch.Draw(pixel, new Rectangle(x, y, 1, 1), RetroArt.Palette[6] * (density * gust));
                }
        }
        // Few low-opacity streaks, each clipped to open sand; actors draw above them.
        for (var i = 0; i < 32; i++)
        {
            var x = (int)((i * 97 + game.Map.Id * 31 + windTime * 13) % SandWidth);
            var y = (i * 53 + game.Map.Id * 17) % (SandHeight - 12) + 6;
            y += (int)Math.Round(Math.Sin(windTime * .8 + i) * 3);
            var opacity = (float)(.12 + .12 * (1 + Math.Sin(windTime * .6 + i * 2.4)));
            for (var j = 0; j < (i % 4 == 0 ? 14 : 3); j++)
            {
                var px = x - j;
                if (!SandClearAt(px, y) || !game.Visibility.Contains(new(px / 32, y / 32))) continue;
                batch.Draw(pixel, new Rectangle(px, y, 1, 1), RetroArt.Palette[18] * (opacity / (1 + j * .25f)));
                if (i % 4 == 0 && j is > 2 and < 10 && SandClearAt(px, y + 2))
                    batch.Draw(pixel, new Rectangle(px, y + 2, 1, 1), RetroArt.Palette[16] * (opacity * .25f));
            }
        }
        for (var i = 0; i < tumbleRuns.Count; i++)
        {
            var run = tumbleRuns[i];
            var travel = (float)((windTime * 16 + game.Map.Id * 19 + i * 91) % (run.Width + 128));
            if (travel >= run.Width) continue; // A quiet interval between gusts.
            var fade = Math.Clamp(Math.Min(travel, run.Width - travel) / 12, 0, 1);
            var x = run.X + (int)travel; var y = run.Y;
            if (!game.Visibility.Contains(new(x / 32, y / 32))) continue;
            batch.Draw(pixel, new Rectangle(x - 3, y, 7, 1), RetroArt.Palette[14] * (.25f * fade));
            y -= 5 + (int)Math.Round(Math.Abs(Math.Sin(travel * .20f)) * 2);
            var angle = (float)(Math.Floor(windTime * 6) % 16 * Math.PI / 8);
            // Small rotating twig silhouette, drawn at native pixels like the other ambient effects.
            for (var twig = 0; twig < 12; twig++)
            {
                var a = angle + twig * MathF.Tau / 12;
                var radius = twig % 3 == 0 ? 4 : 3;
                for (var r = twig % 2 == 0 ? 1 : radius; r <= radius; r++)
                {
                    var px = x + (int)MathF.Round(MathF.Cos(a) * r);
                    var py = y + (int)MathF.Round(MathF.Sin(a) * r);
                    batch.Draw(pixel, new Rectangle(px, py, 1, 1), RetroArt.Palette[py < y ? 17 : 16] * fade);
                }
            }
        }
    }
}
