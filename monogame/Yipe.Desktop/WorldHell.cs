using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private const int HellWidth = Map.Width * 32, HellHeight = Map.Height * 32;
    private readonly ushort[] hellTiles = new ushort[Map.TileCount];
    private readonly bool[] hellMolten = new bool[HellWidth * HellHeight];
    private readonly Vector2[] hellOrigins = new Vector2[8];
    private readonly Texture2D hellProps, hellGround, hellLava, hellGlow;
    private Map? hellMap;

    private static bool IsHell(Map map) => map.Id is >= 117 and <= 120;
    private static bool HellWall(Map map, int x, int y) => Map.Contains(new(x, y)) && map.Tile(new(x, y)) == 0x24;
    private static int HellSeed(int x, int y, int id) => (x * 37 + y * 73 + x * y * 11 + id * 17) % 997;

    private Texture2D LoadHell(GraphicsDevice device, string content)
    {
        var atlas = RetroArt.Load(device, content, "World/hell.png", 4, 2, 48);
        var data = new Color[atlas.Width * atlas.Height]; atlas.GetData(data);
        for (var frame = 0; frame < 8; frame++)
        {
            var source = Cell(frame, 4, 48);
            var left = 48; var right = 0; var bottom = 0;
            for (var y = 0; y < 48; y++)
                for (var x = 0; x < 48; x++)
                    if (data[(source.Y + y) * atlas.Width + source.X + x].A != 0)
                    { left = Math.Min(left, x); right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1); }
            if (bottom == 0) throw new InvalidDataException($"Hell sprite {frame} is empty.");
            hellOrigins[frame] = new((left + right) / 2, bottom);
        }
        return atlas;
    }

    private void PrepareHell(Map map)
    {
        if (!IsHell(map)) return;
        var changed = !ReferenceEquals(hellMap, map);
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var tile = map.Tile(new(x, y)); var index = y * Map.Width + x;
                changed |= hellTiles[index] != tile; hellTiles[index] = tile;
            }
        if (!changed) return;
        hellMap = map;
        var ground = new Color[HellWidth * HellHeight];
        var lava = new Color[HellWidth * HellHeight * 8];
        var glow = new Color[ground.Length];
        var distance = new float[ground.Length];
        var paving = new bool[ground.Length];
        // Keep the approved ember-red/ash balance, with a few deliberate shades
        // instead of hundreds of blended colors between rock, soot and fire.
        Color[] shades =
        [
            new(40, 22, 29), new(56, 26, 33), new(72, 34, 39), new(90, 41, 46),
            new(113, 49, 49), new(143, 59, 55), new(174, 70, 58), new(205, 90, 64),
            RetroArt.Palette[23], RetroArt.Palette[6],
            new(55, 47, 54), new(78, 73, 80), new(94, 89, 95), new(116, 108, 111),
            new(31, 21, 28), new(65, 43, 49), new(98, 65, 65), new(139, 95, 82),
        ];
        for (var y = 0; y < HellHeight; y++)
            for (var x = 0; x < HellWidth; x++)
            {
                var i = y * HellWidth + x;
                var tx = x / 32; var ty = y / 32;
                var wall = HellWall(map, tx, ty);
                // Distance to the opposite terrain joins neighboring wall cells and
                // gives both sides the same weathered soot fringe.
                var edge = 32f;
                for (var cy = ty - 1; cy <= ty + 1; cy++)
                    for (var cx = tx - 1; cx <= tx + 1; cx++)
                    {
                        if (!Map.Contains(new(cx, cy)) || HellWall(map, cx, cy) == wall) continue;
                        var dx = Math.Max(0, Math.Abs(x + .5f - (cx * 32 + 16)) - 16);
                        var dy = Math.Max(0, Math.Abs(y + .5f - (cy * 32 + 16)) - 16);
                        edge = Math.Min(edge, MathF.Sqrt(dx * dx + dy * dy));
                    }
                // Round only exposed outside corners, never the joins between cells.
                if (wall)
                {
                    var radius = 11 + HellSeed(tx, ty, map.Id) % 4;
                    for (var sy = -1; sy <= 1; sy += 2)
                        for (var sx = -1; sx <= 1; sx += 2)
                        {
                            if (!Map.Contains(new(tx + sx, ty)) || !Map.Contains(new(tx, ty + sy))
                                || HellWall(map, tx + sx, ty) || HellWall(map, tx, ty + sy)) continue;
                            var dx = Math.Max(0, radius - (sx < 0 ? x % 32 + .5f : 31.5f - x % 32));
                            var dy = Math.Max(0, radius - (sy < 0 ? y % 32 + .5f : 31.5f - y % 32));
                            edge = Math.Min(edge, radius - MathF.Sqrt(dx * dx + dy * dy));
                        }
                }
                var weathering = 2 * MathF.Sin(x * .21f + y * .13f) + MathF.Sin(y * .43f - x * .17f);
                var scorch = MathHelper.SmoothStep(0, 1, Math.Clamp(((wall ? edge : -edge) + weathering + 9) / 23, 0, 1));
                var gx = x / 2; var gy = y / 2;
                var grain = (gx * 13 + gy * 19 + gx * gy * 3) % 101;
                // Deep ember reds keep the floor warm without losing its midtones.
                var color = new Color(72, 36, 39);
                var nearest = float.MaxValue; var second = float.MaxValue;
                var stoneX = x + 2.5f * MathF.Sin(y * .09f) + MathF.Sin(y * .18f + x * .04f);
                var stoneY = y + 2 * MathF.Sin(x * .075f) + MathF.Sin(x * .16f - y * .03f);
                for (var cy = y / 40 - 1; cy <= y / 40 + 1; cy++)
                    for (var cx = x / 40 - 1; cx <= x / 40 + 1; cx++)
                    {
                        var seed = HellSeed(cx + 7, cy + 7, map.Id);
                        var dx = stoneX - (cx * 40 + 10 + seed % 21);
                        var dy = stoneY - (cy * 40 + 10 + seed / 7 % 21);
                        var d = dx * dx + dy * dy;
                        if (d < nearest) { second = nearest; nearest = d; }
                        else if (d < second) second = d;
                    }
                var crack = MathF.Sqrt(second) - MathF.Sqrt(nearest);
                if (grain < 2) color = new Color(84, 47, 48);
                else if (grain == 6) color = new Color(77, 37, 40);
                color = Color.Lerp(color, new Color(46, 22, 29), scorch * .45f);
                // Small angular chips join the rock bases to the surrounding ash.
                var chip = HellSeed(x / 4, y / 3, map.Id);
                if (scorch > .08f && scorch < .92f && chip % 7 == 0 && (x + chip) % 4 < 3 && (y + chip) % 3 < 2)
                    color = Color.Lerp(color, new Color(68, 29, 33), .65f);
                // Hairline heat below the stone, with a narrow soft halo. These are
                // floor details; molten hazards remain confined to fire-wall cells.
                var seep = Math.Clamp(.5f + .3f * MathF.Sin(x * .028f + y * .019f)
                    + .25f * MathF.Sin(y * .047f - x * .021f), .15f, 1);
                if (crack < .8f)
                    color = Color.Lerp(new Color(78, 28, 27), RetroArt.Palette[22], seep * .7f * (1 - crack / 1.1f));
                glow[i] = RetroArt.Palette[23] * (.09f * seep * MathF.Exp(-crack * crack / 12));
                // Flat, irregular flagstones have narrow joints and worn shoulders,
                // without the repeating courses and bevels of a brick wall.
                var pathWidth = 17 + (int)(2 * MathF.Sin(y * .09f));
                paving[i] = map.Id == 117 && !wall && y < 352 && Math.Abs(x - 208) < pathWidth;
                if (paving[i])
                {
                    var closest = float.MaxValue; var next = float.MaxValue; var stoneSeed = 0;
                    for (var cy = y / 12 - 1; cy <= y / 12 + 1; cy++)
                        for (var cx = x / 15 - 1; cx <= x / 15 + 1; cx++)
                        {
                            var seed = HellSeed(cx + 31, cy + 47, map.Id + 9);
                            var dx = x + MathF.Sin(y * .35f) - (cx * 15 + 3 + seed % 10);
                            var dy = y + MathF.Sin(x * .27f) - (cy * 12 + 2 + seed / 11 % 8);
                            var d = dx * dx + dy * dy;
                            if (d < closest) { next = closest; closest = d; stoneSeed = seed; }
                            else if (d < next) next = d;
                        }
                    var joint = MathF.Sqrt(next) - MathF.Sqrt(closest) < .8f;
                    var shade = 73 + stoneSeed % 12 + (grain < 3 ? 6 : 0);
                    var flagstone = joint ? new Color(64, 54, 59) : new Color(shade + 10, shade + 5, shade + 7);
                    var shoulder = Math.Clamp((pathWidth - Math.Abs(x - 208)) / 3f, 0, 1);
                    color = Color.Lerp(color, flagstone, shoulder);
                    glow[i] = Color.Transparent;
                }
                // Large angular basalt facets replace the finely mottled atlas.
                // Keep the connected, rounded wall footprint and its exposed face.
                var bank = Math.Max(.75f, 3 + weathering * .65f + .6f * MathF.Sin(x * .065f + y * .11f));
                var rock = wall && edge > bank;
                var front = !HellWall(map, tx, ty + 1) && 31 - y % 32 < 7 + weathering;
                var seam = float.MaxValue;
                var facetSeed = 0;
                if (rock)
                {
                    var closest = float.MaxValue; var next = float.MaxValue;
                    var facetY = 0f;
                    for (var cy = y / 11 - 1; cy <= y / 11 + 1; cy++)
                        for (var cx = x / 13 - 1; cx <= x / 13 + 1; cx++)
                        {
                            var seed = HellSeed(cx + 17, cy + 29, map.Id);
                            var dx = x - (cx * 13 + 3 + seed % 8);
                            var dy = y - (cy * 11 + 2 + seed / 7 % 7);
                            var d = dx * dx + dy * dy;
                            if (d < closest) { next = closest; closest = d; facetSeed = seed; facetY = dy; }
                            else if (d < next) next = d;
                        }
                    seam = MathF.Sqrt(next) - MathF.Sqrt(closest);
                    color = seam < 1.1f ? new(31, 21, 28)
                        : facetY < -1 ? new(98, 65, 65) : new(65, 43, 49);
                    if (seam is >= 1.1f and < 2 && facetY < -1) color = new(139, 95, 82);
                    if (front) color = seam < 1.1f ? new(31, 21, 28) : new(56, 26, 33);
                    else if (edge < bank + 1.2f) color = new(31, 21, 28);
                    glow[i] = Color.Transparent;
                }
                ground[i] = Shade(color);
                // The recessed arch ends at y=62; row 2 belongs to the fire wall.
                var foundation = map.Id == 117 && tx is >= 5 and <= 7 && ty <= 1;
                // Selected joints glow; the remaining dark seams outline solid rock.
                var molten = rock && !foundation && seam < .8f && facetSeed % 4 == 0;
                hellMolten[i] = molten;
                distance[i] = molten ? 0 : 1000;
                if (!molten) continue;
                for (var frame = 0; frame < 8; frame++)
                {
                    var phase = frame * MathF.Tau / 8;
                    var flow = MathF.Sin(x * .12f - y * .07f - phase);
                    var hot = Color.Lerp(new Color(205, 62, 39), RetroArt.Palette[23], .45f + flow * .35f);
                    lava[(frame / 4 * HellHeight + y) * HellWidth * 4 + frame % 4 * HellWidth + x] = Shade(Color.Lerp(color, hot, front ? .45f : .8f));
                }
            }
        // The distance field still joins adjacent walls; restrained light bands
        // replace the continuous airbrushed falloff without obscuring the path.
        for (var y = 0; y < HellHeight; y++)
            for (var x = 0; x < HellWidth; x++)
            {
                var i = y * HellWidth + x;
                if (x > 0) distance[i] = Math.Min(distance[i], distance[i - 1] + 1);
                if (y > 0) distance[i] = Math.Min(distance[i], distance[i - HellWidth] + 1);
                if (x > 0 && y > 0) distance[i] = Math.Min(distance[i], distance[i - HellWidth - 1] + 1.4142f);
                if (x + 1 < HellWidth && y > 0) distance[i] = Math.Min(distance[i], distance[i - HellWidth + 1] + 1.4142f);
            }
        for (var y = HellHeight - 1; y >= 0; y--)
            for (var x = HellWidth - 1; x >= 0; x--)
            {
                var i = y * HellWidth + x;
                if (x + 1 < HellWidth) distance[i] = Math.Min(distance[i], distance[i + 1] + 1);
                if (y + 1 < HellHeight) distance[i] = Math.Min(distance[i], distance[i + HellWidth] + 1);
                if (x > 0 && y + 1 < HellHeight) distance[i] = Math.Min(distance[i], distance[i + HellWidth - 1] + 1.4142f);
                if (x + 1 < HellWidth && y + 1 < HellHeight) distance[i] = Math.Min(distance[i], distance[i + HellWidth + 1] + 1.4142f);
                var strength = Math.Max(0, 1 - distance[i] / 20);
                var light = Math.Max(strength * strength * .16f, glow[i].A / 255f);
                light = MathF.Round((paving[i] ? light * .18f : light) * 32) / 32;
                glow[i] = new Color(255, 64, 36) * light;
            }
        hellGround.SetData(ground); hellLava.SetData(lava); hellGlow.SetData(glow);

        Color Shade(Color color)
        {
            var closest = shades[0]; var best = int.MaxValue;
            foreach (var candidate in shades)
            {
                var dr = color.R - candidate.R; var dg = color.G - candidate.G; var db = color.B - candidate.B;
                var difference = dr * dr + dg * dg + db * db;
                if (difference >= best) continue;
                best = difference; closest = candidate;
            }
            // Enrich the existing warm ramp without changing its facet boundaries.
            // Neutral ash and the entrance flagstones retain their readable contrast.
            if (closest.R - closest.G <= 12) return closest;
            return new Color(Math.Min(255, closest.R * 6 / 5), closest.G * 19 / 20, closest.B * 4 / 5);
        }
    }

    private void DrawHellGround(SpriteBatch batch, Rectangle bounds, double time)
    {
        var frame = (int)(time * 4 % 8);
        var source = new Rectangle(bounds.X + frame % 4 * HellWidth, bounds.Y + frame / 4 * HellHeight, 32, 32);
        batch.Draw(hellGround, bounds, bounds, Color.White);
        batch.Draw(hellLava, bounds, source, Color.White);
        var t = (float)(Math.Floor(time * 8) / 8);
        var light = .82f + .10f * MathF.Sin(t * 3.7f) + .08f * MathF.Sin(t * 6.1f);
        batch.Draw(hellGlow, bounds, bounds, Color.White * light);
    }

    private void DrawHellSprite(SpriteBatch batch, int index, Vector2 feet, float warmth = 0)
    {
        for (var ring = 0; ring < 3; ring++)
        {
            var radius = 5 - ring;
            for (var y = -radius + 1; y < radius; y++)
            {
                var width = (int)((19 - ring * 5) * MathF.Sqrt(1 - y * y / (float)(radius * radius)));
                batch.Draw(pixel, new Rectangle((int)feet.X - width, (int)feet.Y + y - 1, width * 2 + 1, 1), new Color(10, 9, 14) * .12f);
            }
        }
        var tint = Color.Lerp(Color.White, new Color(255, 174, 128), warmth);
        batch.Draw(hellProps, feet, Cell(index, 4, 48), tint, 0, hellOrigins[index], 1, SpriteEffects.None, 0);
    }

    private void DrawHellScenery(SpriteBatch batch, Map map, MapPosition cell, double time)
    {
        var tile = map.Tile(cell); var seed = HellSeed(cell.X, cell.Y, map.Id);
        var feet = new Vector2(cell.X * 32 + 16, cell.Y * 32 + 31);
        var warmth = .12f + .04f * MathF.Sin((float)time * 4 + seed);
        if (tile == 0x24)
        {
            feet += new Vector2(seed % 7 - 3, -5 + seed % 4);
            var foundation = map.Id == 117 && cell.X is >= 5 and <= 7 && cell.Y <= 1;
            var chain = map.Id == 117 && cell.X is 4 or 8 && cell.Y == 10;
            var brazier = map.Id == 117 && cell.X is 4 or 8 && cell.Y == 6;
            if (chain || brazier)
                DrawHellSprite(batch, chain ? 6 : 7, feet, warmth);
            // Low broken masonry stays inside the wall cell, clear of the
            // monsters standing immediately north of these two ruins.
            if (map.Id == 117 && cell.X is 4 or 8 && cell.Y == 8)
                for (var chip = 0; chip < 5; chip++)
                {
                    var stone = HellSeed(cell.X * 5 + chip, cell.Y, map.Id);
                    var x = cell.X * 32 + 3 + chip * 5 + stone % 2;
                    var y = cell.Y * 32 + 13 + stone / 11 % 6;
                    var width = 4 + stone % 4; var height = 3 + stone / 7 % 3;
                    batch.Draw(pixel, new Rectangle(x, y + height, width + 1, 1), new Color(10, 9, 14) * .35f);
                    batch.Draw(pixel, new Rectangle(x, y + 1, width, height), new Color(49, 30, 36));
                    batch.Draw(pixel, new Rectangle(x + 1, y, width - 2, height - 1), new Color(80, 49, 49));
                    batch.Draw(pixel, new Rectangle(x + 1, y, width - 3, 1),
                        Color.Lerp(new Color(105, 66, 61), new Color(153, 81, 53), warmth));
                }
            // Every original fire-wall cell retains a visible hot hazard, even under rock.
            var flameFeet = HellFlameFeet(map, cell);
            DrawHellFlame(batch, flameFeet, seed, time, foundation ? 5 : 6 + seed % 3);
            if (seed % 4 == 0 && !foundation) DrawHellFlame(batch, flameFeet + new Vector2(8, 1), seed + 9, time, 5);
        }
        else if (tile is 0x3e or 0x3f or 0x40)
            DrawHellSprite(batch, 5, feet, tile == 0x40 ? .3f : warmth);
        // Recess the arch into the north map edge, with its jambs on authored walls.
        if (map.Id == 117 && cell == new MapPosition(6, 1))
            DrawHellGate(batch);
    }

    private void DrawHellGate(SpriteBatch batch)
    {
        // Draw masonry at world-pixel resolution instead of doubling a small
        // painted sprite. The arch keeps the same recessed entrance and footing.
        var outline = new Color(24, 19, 26);
        for (var side = -1; side <= 1; side += 2)
            for (var row = 0; row < 6; row++)
            {
                var inset = row == 0 ? 4 : 0;
                var left = side < 0 ? 174 + inset : 228;
                var top = row * 10;
                var width = 14 - inset;
                batch.Draw(pixel, new Rectangle(left - 1, top, width + 2, 11), outline);
                batch.Draw(pixel, new Rectangle(left, top + 1, width, 8), new Color(77, 48, 54));
                batch.Draw(pixel, new Rectangle(left + 1, top + 1, width - 2, 2), new Color(151, 104, 84));
                batch.Draw(pixel, new Rectangle(left + 1, top + 3, 2, 5), new Color(111, 73, 66));
                batch.Draw(pixel, new Rectangle(left + width - 3, top + 4, 3, 5), new Color(45, 28, 36));
                // A few deliberate chips; broad faces stay free of speckle.
                if ((row + side + 7) % 3 == 0)
                    batch.Draw(pixel, new Rectangle(left + 5, top + 1, 2, 2), outline);
            }
        // The curved crown continues above the map's north edge.
        for (var side = -1; side <= 1; side += 2)
            for (var step = 0; step < 3; step++)
            {
                var left = side < 0 ? 186 + step * 4 : 222 - step * 4;
                var top = 6 - step * 4;
                batch.Draw(pixel, new Rectangle(left, top, 9, 10), outline);
                batch.Draw(pixel, new Rectangle(left + 1, top + 1, 7, 7), new Color(111, 73, 66));
                batch.Draw(pixel, new Rectangle(left + 1, top + 1, 6, 1), new Color(170, 119, 91));
            }
        batch.Draw(pixel, new Rectangle(171, 59, 19, 3), outline);
        batch.Draw(pixel, new Rectangle(226, 59, 19, 3), outline);
    }

    internal static Vector2 HellFlameFeet(Map map, MapPosition cell)
    {
        var seed = HellSeed(cell.X, cell.Y, map.Id);
        var brazier = map.Id == 117 && cell.X is 4 or 8 && cell.Y == 6;
        return new(cell.X * 32 + 17 + seed % 7 - seed % 9, cell.Y * 32 + 26 + seed % 4 - (brazier ? 12 : 2));
    }

    private void DrawHellDoorCrown(SpriteBatch batch, Map map, MapPosition cell, double time)
    {
        var tile = map.Tile(cell);
        if (tile is not (0x3e or 0x3f or 0x40)) return;
        // Restore only the sprite slice above its owning tile. Transparent pixels
        // leave the fog intact, and the door's base keeps its normal depth order.
        var source = Cell(5, 4, 48);
        source.X += (int)hellOrigins[5].X - 16;
        source.Width = 32;
        source.Height = (int)hellOrigins[5].Y - 31;
        var warmth = tile == 0x40 ? .3f : .12f + .04f * MathF.Sin((float)time * 4 + HellSeed(cell.X, cell.Y, map.Id));
        batch.Draw(hellProps, new Vector2(cell.X * 32, cell.Y * 32 - source.Height), source,
            Color.Lerp(Color.White, new Color(255, 174, 128), warmth));
    }

    private void DrawHellFlame(SpriteBatch batch, Vector2 feet, int seed, double time, int size)
    {
        var phase = (float)(Math.Floor(time * 8) / 8) * 7 + seed * .73f;
        var height = size + (int)MathF.Round(2 * MathF.Sin(phase));
        for (var y = 0; y < height; y++)
        {
            var fraction = y / (float)height;
            var width = Math.Max(1, (int)((1 - fraction) * (size * .30f)));
            var sway = (int)MathF.Round(MathF.Sin(phase + fraction * 4) * fraction * 2);
            var x = (int)feet.X + sway; var top = (int)feet.Y - y;
            batch.Draw(pixel, new Rectangle(x - width, top, width * 2 + 1, 1), RetroArt.Palette[21]);
            if (width > 1) batch.Draw(pixel, new Rectangle(x - width + 1, top, width * 2 - 1, 1), RetroArt.Palette[23]);
            if (fraction < .55f) batch.Draw(pixel, new Rectangle(x, top, Math.Max(1, width), 1), RetroArt.Palette[6]);
        }
    }

    private void DrawHellAtmosphere(SpriteBatch batch, Adventure game, double time)
    {
        if (!IsHell(game.Map)) return;
        var t = Math.Floor(time * 12) / 12;
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var cell = new MapPosition(x, y);
                if (!game.Visibility.Contains(cell) || !HellWall(game.Map, x, y)) continue;
                var seed = HellSeed(x, y, game.Map.Id);
                // Bubbles appear only in actual molten pixels, never on the traversable floor.
                var bx = x * 32 + 7 + seed % 18; var by = y * 32 + 7 + seed / 3 % 18;
                var bubble = (t + seed * .13) % 4.5;
                if (bubble < .65 && hellMolten[by * HellWidth + bx])
                {
                    var radius = 1 + (int)(bubble * 4);
                    for (var dy = -radius; dy <= radius; dy++)
                        for (var dx = -radius; dx <= radius; dx++)
                            if (Math.Abs(dx * dx + dy * dy - radius * radius) <= radius
                                && hellMolten[(by + dy) * HellWidth + bx + dx])
                                batch.Draw(pixel, new Rectangle(bx + dx, by + dy, 1, 1), RetroArt.Palette[6] * (float)(1 - bubble / .65));
                }
                if (seed % 3 != 0) continue;
                var rise = (float)((t * 9 + seed) % 29);
                var px = x * 32 + 16 + (int)(Math.Sin(t * 1.7 + seed) * 4);
                var py = y * 32 + 26 - (int)rise;
                if (game.Visibility.Contains(new(px / 32, py / 32)))
                    batch.Draw(pixel, new Rectangle(px, py, 1, seed % 2 + 1), RetroArt.Palette[35] * (1 - rise / 29));
                if (seed % 9 != 0) continue;
                var smoke = (float)((t * 5 + seed) % 40);
                var sx = x * 32 + 16 + (int)(Math.Sin(t + seed) * 3 + smoke * .12f);
                var sy = y * 32 + 16 - (int)smoke;
                var r = 3 + (int)(smoke / 10);
                for (var dy = -r; dy <= r; dy++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var cx = dx / 2 * 2; var cy = dy / 2 * 2;
                        var density = 1 - (cx * cx + cy * cy) / (float)(r * r);
                        var wx = sx + dx; var wy = sy + dy;
                        if (density <= 0 || wx < 0 || wy < 0 || !game.Visibility.Contains(new(wx / 32, wy / 32))) continue;
                        var fade = MathF.Ceiling((1 - smoke / 40) * 4) / 4;
                        batch.Draw(pixel, new Rectangle(wx, wy, 1, 1), RetroArt.Palette[3] * ((density > .55f ? .08f : .04f) * fade));
                    }
            }
        for (var i = 0; i < 20; i++)
        {
            var x = (int)((i * 97 + t * 5) % HellWidth);
            var y = (int)((i * 71 + t * 3) % HellHeight);
            if (game.Visibility.Contains(new(x / 32, y / 32)))
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), RetroArt.Palette[4] * .22f);
        }
    }
}
