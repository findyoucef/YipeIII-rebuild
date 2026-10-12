using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int SnowWidth = Map.Width * 32, SnowHeight = Map.Height * 32;
    private readonly ushort[] snowTiles = new ushort[Map.TileCount];
    private readonly Color[] snowPixels = new Color[SnowWidth * SnowHeight];
    private readonly float[] snowField = new float[SnowWidth * SnowHeight];
    private readonly float[] snowBlur = new float[SnowWidth * SnowHeight];
    private readonly bool[] snowCold = new bool[SnowWidth * SnowHeight];
    private readonly List<SnowPrint> snowPrints = new(16);
    private readonly record struct SnowPrint(Point Feet, int Facing, double Born);
    private Map? snowMap, snowPrintMap;
    private bool snowActive;

    internal static bool SnowTile(ushort tile) => tile is 0x1f or 0x20 or 0x21 or 0x69;
    internal bool SnowAt(int x, int y) => snowActive && x >= 0 && y >= 0 && x < SnowWidth && y < SnowHeight
        && snowPixels[y * SnowWidth + x].A != 0;
    internal bool SnowColdAt(int x, int y) => snowActive && x >= 0 && y >= 0 && x < SnowWidth && y < SnowHeight
        && snowCold[y * SnowWidth + x];

    private void PrepareSnow(Map map)
    {
        var changed = !ReferenceEquals(snowMap, map);
        var active = false;
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var tile = map.Tile(new(x, y));
                // Secret gold remains indistinguishable from ordinary snow, even after searching.
                if (tile == 0x69) tile = 0x1f;
                var i = y * Map.Width + x;
                changed |= snowTiles[i] != tile; snowTiles[i] = tile;
                active |= SnowTile(tile);
            }
        if (!changed) return;
        snowMap = map; snowActive = map.Id < 41 && active;
        Array.Clear(snowPixels); Array.Clear(snowCold);
        if (!snowActive) { snowGround.SetData(snowPixels); return; }

        for (var y = 0; y < SnowHeight; y++)
            for (var x = 0; x < SnowWidth; x++)
            {
                var tile = snowTiles[y / 32 * Map.Width + x / 32];
                snowField[y * SnowWidth + x] = tile == 0x20 ? .65f : SnowTile(tile) ? 1 : 0;
            }
        // Compile one connected footprint, rather than repeating a square snowy tile.
        const int radius = 12, span = radius * 2 + 1;
        for (var y = 0; y < SnowHeight; y++)
        {
            var sum = 0f;
            for (var x = -radius; x <= radius; x++) sum += snowField[y * SnowWidth + Math.Clamp(x, 0, SnowWidth - 1)];
            for (var x = 0; x < SnowWidth; x++)
            {
                snowBlur[y * SnowWidth + x] = sum / span;
                sum += snowField[y * SnowWidth + Math.Min(SnowWidth - 1, x + radius + 1)]
                    - snowField[y * SnowWidth + Math.Max(0, x - radius)];
            }
        }
        for (var x = 0; x < SnowWidth; x++)
        {
            var sum = 0f;
            for (var y = -radius; y <= radius; y++) sum += snowBlur[Math.Clamp(y, 0, SnowHeight - 1) * SnowWidth + x];
            for (var y = 0; y < SnowHeight; y++)
            {
                snowField[y * SnowWidth + x] = sum / span;
                sum += snowBlur[Math.Min(SnowHeight - 1, y + radius + 1) * SnowWidth + x]
                    - snowBlur[Math.Max(0, y - radius) * SnowWidth + x];
            }
        }
        for (var y = 0; y < SnowHeight; y++)
            for (var x = 0; x < SnowWidth; x++)
            {
                var i = y * SnowWidth + x;
                var tile = snowTiles[y / 32 * Map.Width + x / 32];
                var contour = snowField[i] - .36f + MathF.Sin(x * .061f + y * .025f) * .025f
                    + MathF.Sin(y * .10f - x * .043f) * .014f;
                if (contour > 0 && WaterTile(tile) == 0 && (RetroArt.PathConnections(tile) == 0
                    || MountainTerrain.IsMountain(tile)))
                {
                    // Small stepped drifts keep the same pixel scale as the meadow
                    // and rock clusters. Each patch has its own offset and length.
                    var patchX = x / 27; var patchY = y / 19;
                    var variation = (patchX * 17 + patchY * 31 + patchX * patchY * 7) % 23;
                    var px = x % 27 - 4 - variation % 8;
                    var py = y % 19 - 5 - variation % 7;
                    var length = 7 + variation % 9;
                    var drift = px >= 0 && px < length && py >= 0 && py < 3
                        && px > (py == 0 ? 3 : 0) && px < length - (py == 2 ? 3 : 0);
                    var rim = contour < .045f + (x / 3 * 3 + y / 2 * 5) % 7 * .004f;
                    snowPixels[i] = RetroArt.SnowPalette[rim ? 2 : drift ? 1 : 0];
                }
                snowCold[i] = snowPixels[i].A > 0 || MountainTerrain.IsMountain(tile) && NearSnow(x, y);
            }
        snowGround.SetData(snowPixels);

        bool NearSnow(int x, int y)
        {
            var tx = x / 32; var ty = y / 32;
            for (var cy = Math.Max(0, ty - 2); cy <= Math.Min(Map.Height - 1, ty + 2); cy++)
                for (var cx = Math.Max(0, tx - 2); cx <= Math.Min(Map.Width - 1, tx + 2); cx++)
                {
                    if (!SnowTile(snowTiles[cy * Map.Width + cx])) continue;
                    var dx = Math.Max(0, Math.Abs(x - (cx * 32 + 16)) - 16);
                    var dy = Math.Max(0, Math.Abs(y - (cy * 32 + 16)) - 16);
                    if (dx * dx + dy * dy <= 48 * 48) return true;
                }
            return false;
        }
    }

    private void DrawSnowScenery(SpriteBatch batch, Map map, MapPosition cell, double time)
    {
        var tile = map.Tile(cell);
        if (tile == 0x21)
        {
            // Stagger full pines and saplings instead of lining up equal crowns.
            // Each trunk stays inside its blocked cell, including in the area map.
            var variation = (cell.X * 3 + cell.Y * 5) % 7;
            var (size, offsetX, offsetY, index) = variation switch
            {
                3 => (44, 5, -12, 0),
                6 => (32, -1, -2, 1),
                5 => (40, 5, -7, 1),
                4 => (38, -5, -7, 0),
                _ => (44, 3, -3, 1),
            };
            var left = cell.X * 32 + 16 + offsetX - size / 2;
            var top = cell.Y * 32 + 31 + offsetY - 44 * size / 48;
            var flip = variation % 2 == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            var breeze = (int)MathF.Round(MathF.Sin((float)(Math.Floor(time * 5) / 5) * 1.2f + cell.X) * .8f);
            for (var row = 0; row < 48; row += 4)
            {
                var y = row * size / 48;
                batch.Draw(snowProps, new Rectangle(left + (row < 28 ? breeze : 0), top + y,
                    size, (row + 4) * size / 48 - y), new Rectangle(index * 48, row, 48, 4),
                    Color.White, 0, Vector2.Zero, flip, 0);
            }
        }
        else if (tile == 0x39)
            batch.Draw(snowProps, new Vector2(cell.X * 32 - 8, cell.Y * 32 - 13), Cell(2, 2, 48), Color.White);
        else if (tile is 0x1f or 0x69 && (cell.X * 7 + cell.Y * 11) % 29 == 2
            && !map.Events.Chests.Contains(cell) && cell != map.Events.Message.Position)
            batch.Draw(snowProps, new Vector2(cell.X * 32 - 8, cell.Y * 32 - 13), Cell(3, 2, 48), Color.White);
    }

    public void SnowStep(Map map, PlayerMotion hero, double time)
    {
        PrepareSnow(map);
        var feet = new Point((int)MathF.Round(hero.VisualPosition.X * 32 + 16), (int)MathF.Round(hero.VisualPosition.Y * 32 + 25));
        if (!SnowAt(feet.X, feet.Y)) return;
        if (!ReferenceEquals(snowPrintMap, map)) { snowPrints.Clear(); snowPrintMap = map; }
        snowPrints.RemoveAll(print => time - print.Born >= 4);
        if (snowPrints.Count == 16) snowPrints.RemoveAt(0);
        snowPrints.Add(new(feet, hero.FacingRow, time));
    }

    public void ClearSnowTracks() { snowPrints.Clear(); snowPrintMap = null; }

    private void DrawSnowTracks(SpriteBatch batch, Adventure game, double time)
    {
        if (!ReferenceEquals(snowPrintMap, game.Map)) return;
        foreach (var print in snowPrints)
        {
            var age = time - print.Born;
            if (age is < 0 or >= 4) continue;
            var alpha = .34f * (float)(1 - age / 4);
            var horizontal = print.Facing is 1 or 2;
            for (var boot = -1; boot <= 1; boot += 2)
            {
                var center = print.Feet + (horizontal ? new Point(boot, boot * 3) : new Point(boot * 3, boot));
                for (var y = -2; y <= 2; y++)
                    for (var x = -1; x <= 1; x++)
                    {
                        if (Math.Abs(y) == 2 && x != 0) continue;
                        var px = center.X + (horizontal ? y : x); var py = center.Y + (horizontal ? x : y);
                        Mark(px, py, RetroArt.Palette[3] * alpha);
                    }
            }
            if (age >= .35) continue;
            for (var mote = 0; mote < 4; mote++)
            {
                var spread = 2 + (int)(age * 17);
                var x = print.Feet.X + (mote % 2 == 0 ? -spread : spread);
                var y = print.Feet.Y - 1 - mote / 2 - (int)(Math.Sin(age / .35 * Math.PI) * 3);
                Mark(x, y, RetroArt.Palette[5] * ((float)(1 - age / .35) * .65f));
            }
        }
        void Mark(int x, int y, Color color)
        {
            if (SnowAt(x, y) && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
        }
    }

    private void DrawSnowWind(SpriteBatch batch, Adventure game, double time)
    {
        if (!snowActive) return;
        var clock = Math.Floor(time * 8) / 8;
        for (var gust = 0; gust < 4; gust++)
        {
            var x = (int)((clock * 14 + gust * 109) % (SnowWidth + 40)) - 20;
            var y = 25 + (gust * 83 + game.Map.Id * 17) % (SnowHeight - 50);
            y += (int)Math.Round(Math.Sin(clock * .7 + gust) * 5);
            var alpha = (float)(.08 + .045 * Math.Sin(clock * .6 + gust));
            for (var trail = 0; trail < 18; trail++)
            {
                var px = x - trail; var py = y + (trail / 6 % 2);
                if (SnowAt(px, py) && game.Visibility.Contains(new(px / 32, py / 32)))
                    batch.Draw(pixel, new Rectangle(px, py, 1, 1), RetroArt.Palette[5] * (alpha * (1 - trail / 18f)));
            }
        }
    }

    internal void DrawSnowfall(SpriteBatch batch, Adventure game, double time, float stormStrength)
    {
        PrepareSnow(game.Map);
        if (!snowActive) return;
        // Steady snowfall fills the basin; passing weather adds heavier flurries.
        var strength = .65f + Math.Max(0, stormStrength) * .65f;
        var flakeCount = strength * 312;
        var clock = Math.Floor(time * 12) / 12;
        for (var flake = 0; flake < flakeCount; flake++)
        {
            var x = (int)((flake * 97 + clock * 8 + Math.Sin(clock * .7 + flake) * 9) % SnowWidth);
            if (x < 0) x += SnowWidth;
            var y = (int)((flake * 53 + clock * (12 + flake % 7)) % SnowHeight);
            var alpha = (.34f + flake % 3 * .09f) * Math.Clamp(flakeCount - flake, 0, 1);
            Flake(x, y, alpha);
            if (flake % 4 == 0) Flake(x + 1, y, alpha * .7f);
        }
        void Flake(int x, int y, float alpha)
        {
            if (SnowColdAt(x, y) && game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), RetroArt.Palette[5] * alpha);
        }
    }
}
