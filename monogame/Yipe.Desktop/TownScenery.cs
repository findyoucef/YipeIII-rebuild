using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownRenderer
{
    private const int WorldWidth = TownMap.Width * 32;
    private const int WorldHeight = TownMap.Height * 32;
    private readonly Texture2D meadow;
    private readonly Texture2D details;
    private readonly Texture2D river;
    private readonly Texture2D paths;
    private readonly Texture2D castShadows;
    private readonly Texture2D buildingShade;
    private readonly Texture2D windowGlow;
    private readonly bool[] waterMask = new bool[WorldWidth * WorldHeight];

    // Decorative positions are authored separately from collision tiles; paths stay open.
    private static readonly (int Sprite, int X, int Y)[] GroundDetails =
    [
        (0, 154, 71), (1, 303, 91), (0, 171, 241), (1, 309, 250),
        (0, 47, 266), (1, 334, 319), (0, 203, 43), (1, 367, 122),
        (2, 93, 259), (2, 130, 49), (2, 72, 186), (2, 222, 299),
        (3, 138, 268), (3, 325, 176), (3, 173, 115), (3, 316, 51),
        (3, 289, 285), (3, 29, 201), (3, 346, 221), (3, 192, 333),
        (4, 123, 81), (4, 100, 39), (4, 67, 251), (4, 93, 302),
        (4, 68, 343), (4, 92, 106), (5, 302, 310), (5, 168, 35),
        (5, 27, 85), (5, 333, 96),
    ];

    private static int BuildingSprite(int tile) => tile switch
    {
        1 => 0, 5 => 1, 6 => 2, 7 => 3, 10 => 4, 48 => 5, 57 => 6, _ => -1,
    };

    private Texture2D CreateRiver(GraphicsDevice device)
    {
        var source = new Color[terrain.Width * terrain.Height];
        terrain.GetData(source);
        var pixels = new Color[WorldWidth * WorldHeight];
        for (var y = 0; y < WorldHeight; y++)
            for (var x = 0; x < WorldWidth; x++)
            {
                var connections = map.Tile(new(x / 32, y / 32)) switch
                {
                    14 or 89 or 11 => 5, 15 => 6, 16 => 9, _ => 0,
                };
                if (connections == 0) continue;
                var p = new Vector2(x % 32 + 0.5f, y % 32 + 0.5f);
                var distance = float.MaxValue;
                if ((connections & 1) != 0) distance = MathF.Min(distance, SegmentDistance(p, new(16, 0)));
                if ((connections & 2) != 0) distance = MathF.Min(distance, SegmentDistance(p, new(32, 16)));
                if ((connections & 4) != 0) distance = MathF.Min(distance, SegmentDistance(p, new(16, 32)));
                if ((connections & 8) != 0) distance = MathF.Min(distance, SegmentDistance(p, new(0, 16)));
                var radius = 7.5f + MathF.Sin(x * 0.35f + y * 0.19f) * 1.3f
                    + MathF.Sin(x * 0.13f - y * 0.41f) * 0.6f;
                var index = y * WorldWidth + x;
                if (distance < radius)
                {
                    waterMask[index] = true;
                    pixels[index] = source[(32 + y % 32) * terrain.Width + x % 32];
                    if (distance > radius - 1) pixels[index] = Palette[28];
                }
                else if (distance < radius + 1.5f) pixels[index] = Palette[16];
                else if (distance < radius + 3) pixels[index] = Palette[(x / 3 + y / 4) % 3 == 0 ? 10 : 9];
                if (distance >= radius + 1 && distance < radius + 2 && (x * 13 + y * 7) % 37 == 0)
                    pixels[index] = Palette[3];
            }
        var texture = new Texture2D(device, WorldWidth, WorldHeight);
        texture.SetData(pixels);
        return texture;

        static float SegmentDistance(Vector2 p, Vector2 end)
        {
            var start = new Vector2(16);
            var direction = end - start;
            var t = Math.Clamp(Vector2.Dot(p - start, direction) / direction.LengthSquared(), 0, 1);
            return Vector2.Distance(p, start + direction * t);
        }
    }

    private (Texture2D Shadows, Texture2D Shade, Texture2D Glow) CreateBuildingLighting(GraphicsDevice device)
    {
        var source = new Color[town.Width * town.Height];
        town.GetData(source);
        var shadows = new Color[6 * 64 * 16];
        var shade = new Color[source.Length];
        var glow = new Color[source.Length];
        for (var sprite = 0; sprite < 6; sprite++)
            for (var y = 0; y < 44; y++)
                for (var x = 0; x < 48; x++)
                {
                    var index = (sprite / 3 * 48 + y) * town.Width + sprite % 3 * 48 + x;
                    var color = source[index];
                    if (color.A == 0) continue;
                    // Project each silhouette southeast from its ground anchor; sunlight is northwest.
                    var sx = x + (43 - y) / 3;
                    var sy = (43 - y) / 4;
                    shadows[sy * (6 * 64) + sprite * 64 + sx] = Palette[0] * 0.22f;
                    if (sprite < 5 && y is 29 or 30) shade[index] = Palette[0] * 0.18f;
                    // Warm light belongs to the forge and magic-shop windows, not their walls.
                    if (sprite is 2 or 4 && x is >= 32 and < 38 && y is >= 32 and < 40
                        && color.R > 165 && color.G > 130 && color.B < 170)
                        glow[index] = Palette[7] * 0.14f;
                }
        return (Texture(shadows, 6 * 64, 16), Texture(shade, town.Width, town.Height), Texture(glow, town.Width, town.Height));

        Texture2D Texture(Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(device, width, height);
            texture.SetData(pixels);
            return texture;
        }
    }

    private void DrawRiver(SpriteBatch batch, double time)
    {
        batch.Draw(river, Vector2.Zero, Color.White);
        // Current follows the town river south, west around the bend, then south again.
        var tick = (int)(time * 6);
        for (var i = 0; i < 19; i++)
        {
            var travel = (i * 23 + tick) % 416;
            var point = travel < 144 ? new Point(112, travel)
                : travel < 176 ? new Point(112 - (travel - 144), 144)
                : new Point(80, travel - 32);
            var ripple = travel is >= 144 and < 176
                ? new Rectangle(point.X, point.Y - 2, 1, 4)
                : new Rectangle(point.X - 2 + i % 3, point.Y, 4, 1);
            if (IsWater(ripple)) batch.Draw(pixel, ripple, Palette[29] * 0.6f);
            // Sparse foam flecks stay in the water beside alternating banks.
            var foam = new Rectangle(point.X + (i % 2 == 0 ? 5 : -6), point.Y + 3, 2, 1);
            if (i % 3 == 0 && IsWater(foam)) batch.Draw(pixel, foam, Palette[30] * 0.55f);
        }
    }

    private void DrawHeroReflection(SpriteBatch batch, PlayerMotion player, double time)
        => DrawReflection(batch, player, time, null, null);

    internal void DrawWorldReflection(SpriteBatch batch, PlayerMotion player, double time, Adventure game, WorldRenderer renderer)
        => DrawReflection(batch, player, time, game, renderer);

    private void DrawReflection(SpriteBatch batch, PlayerMotion player, double time, Adventure? world, WorldRenderer? renderer)
    {
        if (world is not null && WorldRenderer.IsIce(world.Map))
        {
            var at = player.VisualPosition;
            if (!world.Visibility.Contains(new((int)MathF.Floor(at.X), (int)MathF.Floor(at.Y)))) return;
            var pose = HeroFrame(player, time); pose.Height = 28;
            renderer!.DrawIceReflection(batch, world, HeroTexture, pose,
                new((int)MathF.Round(at.X * 32) + 16, (int)MathF.Round(at.Y * 32) + 25));
            return;
        }
        bool Water(int x, int y)
        {
            if (world is null) return IsWater(new(x, y, 1, 1));
            if (x < 0 || y < 0 || x >= Map.Width * 32 || y >= Map.Height * 32) return false;
            var cell = new MapPosition(x / 32, y / 32);
            return world.Visibility.Contains(cell) && renderer!.WaterAt(x, y);
        }
        var position = player.VisualPosition;
        var strength = 0f;
        // Fade using the animated position, so accepting a grid step cannot pop the reflection.
        for (var y = Math.Max(0, (int)position.Y - 1); y <= Math.Min(TownMap.Height - 1, (int)position.Y + 1); y++)
            for (var x = Math.Max(0, (int)position.X - 1); x <= Math.Min(TownMap.Width - 1, (int)position.X + 1); x++)
            {
                if ((world?.Map.Tile(new(x, y)) ?? map.Tile(new(x, y))) != 11) continue;
                var across = MathHelper.SmoothStep(0, 1, Math.Max(0, 1 - Math.Abs(position.X - x)));
                var along = MathHelper.SmoothStep(0, 1, Math.Max(0, 1 - Math.Abs(position.Y - y)));
                strength = Math.Max(strength, across * along);
            }
        var feet = new Point((int)MathF.Round(position.X * 32) + 16, (int)MathF.Round(position.Y * 32) + 25);
        var caveWater = renderer?.HasPuddles == true || world is not null && WorldRenderer.IsWaterCave(world.Map);
        if (caveWater)
        {
            // A small footprint around the animated boots fades the existing
            // reflection in and out at a puddle's irregular shoreline.
            var wet = 0;
            for (var dy = -4; dy <= 4; dy++)
                for (var dx = -8; dx <= 8; dx++)
                    if (Water(feet.X + dx, feet.Y + dy)) wet++;
            strength = Math.Max(strength, MathHelper.SmoothStep(0, 1, wet / (17f * 9)));
        }
        if (strength <= 0) return;

        var source = HeroFrame(player, time);
        var tint = world is not null && WorldRenderer.IsWaterCave(world.Map) ? new Color(125, 202, 187) : Palette[caveWater ? 18 : 30];
        var tick = (int)(time * 6);
        for (var row = 0; row < 20; row++)
        {
            // Mirror from the boot line, compress vertically and ripple by at most one pixel.
            var sourceY = source.Y + 27 - row * 28 / 20;
            var ripple = (int)MathF.Round(MathF.Sin(tick * 0.55f + row * 0.65f));
            var left = feet.X - 16 + ripple;
            var y = feet.Y + 2 + row;
            var opacity = strength * 0.28f * (1 - row / 28f) * (row % 4 == 2 ? 0.7f : 1);
            // Clip each scan row to real water; banks stay untouched and the bridge draws over it.
            for (var column = 0; column < 32;)
            {
                while (column < 32 && !Water(left + column, y)) column++;
                var start = column;
                while (column < 32 && Water(left + column, y)) column++;
                if (column > start)
                    batch.Draw(HeroTexture, new Vector2(left + start, y),
                        new Rectangle(source.X + start, sourceY, column - start, 1), tint * opacity);
            }
        }
    }

    private bool IsWater(Rectangle area)
    {
        if (area.Left < 0 || area.Top < 0 || area.Right > WorldWidth || area.Bottom > WorldHeight) return false;
        for (var y = area.Top; y < area.Bottom; y++)
            for (var x = area.Left; x < area.Right; x++)
                if (!waterMask[y * WorldWidth + x]) return false;
        return true;
    }

    private void DrawGroundDetails(SpriteBatch batch, PlayerMotion player, double time)
    {
        foreach (var prop in GroundDetails) DrawProp(batch, prop.Sprite, new(prop.X, prop.Y), time);
        var facing = player.FacingRow switch { 0 => new Point(0, 1), 1 => new Point(-1, 0), 2 => new Point(1, 0), _ => new Point(0, -1) };
        // Worn turf softens the doorstep where traffic leaves the paving.
        for (var y = 1; y < TownMap.Height - 1; y++)
            for (var x = 1; x < TownMap.Width - 1; x++)
            {
                var sprite = BuildingSprite(map.Tile(new(x, y)));
                if (sprite < 0) continue;
                var groundY = y * 32 + (sprite == 6 ? 32 : 28);
                for (var row = 0; row < 5; row++)
                {
                    var width = 16 - row * 2;
                    batch.Draw(pixel, new Rectangle(x * 32 + 16 - width / 2, groundY + 1 + row, width, 1),
                        Palette[18] * (0.12f - row * 0.015f));
                }
                if (player.IsMoving || player.Position.X + facing.X != x || player.Position.Y + facing.Y != y) continue;
                // This marks the existing entrance, without claiming a new interaction.
                batch.Draw(pixel, new Rectangle(x * 32 + 11, groundY + 2, 10, 1), Palette[6] * 0.85f);
                batch.Draw(pixel, new Rectangle(x * 32 + 10, groundY, 1, 3), Palette[6] * 0.85f);
                batch.Draw(pixel, new Rectangle(x * 32 + 21, groundY, 1, 3), Palette[6] * 0.85f);
            }
    }

    internal void DrawProp(SpriteBatch batch, int sprite, Vector2 feet, double time)
    {
        var source = Cell(sprite, 4, 32);
        var sway = (int)MathF.Round(MathF.Sin((float)time * 0.8f + feet.X * 0.1f));
        if (sprite is >= 8 and <= 12)
        {
            // A fixed 28-pixel canvas preserves readable symbols at a modest size.
            // Integer-sized pieces preserve crisp pixels and a stationary post.
            var x = (int)feet.X - 14;
            var y = (int)feet.Y - 24;
            batch.Draw(pixel, new Rectangle((int)feet.X - 8, (int)feet.Y - 1, 4, 1), Palette[0] * 0.2f);
            batch.Draw(details, new Rectangle(x, y, 28, 12),
                new Rectangle(source.X, source.Y, 32, 14), Color.White);
            // Exclude the four transparent bottom rows so point sampling keeps the last outline row.
            batch.Draw(details, new Rectangle(x, y + 12, 9, 12),
                new Rectangle(source.X, source.Y + 14, 10, 14), Color.White);
            batch.Draw(details, new Rectangle(x + 9 + sway, y + 12, 19, 12),
                new Rectangle(source.X + 10, source.Y + 14, 22, 14), Color.White);
            return;
        }
        var position = feet - new Vector2(16, 28);
        if (sprite is 3 or 4 or 5)
        {
            batch.Draw(details, position + new Vector2(sway, 0), new Rectangle(source.X, source.Y, 32, 19), Color.White);
            batch.Draw(details, position + new Vector2(0, 19), new Rectangle(source.X, source.Y + 19, 32, 13), Color.White);
        }
        else batch.Draw(details, position, source, Color.White);
    }

    private void DrawShopDetails(SpriteBatch batch, int x, int y, int sprite, double time)
    {
        var origin = new Vector2(x * 32, y * 32);
        // Keep each board outside its facade while sharing the building's ground line.
        // The smith's wider wall needs extra clearance; magic uses the left corner by the cliffs.
        if (sprite == 4)
            DrawProp(batch, 12, origin + new Vector2(-13, 28), time);
        else if (sprite < 5)
            DrawProp(batch, 8 + sprite, origin + new Vector2(sprite == 2 ? 44 : 42, 28), time);
    }

    private void DrawFootsteps(SpriteBatch batch, PlayerMotion player, Vector2 feet)
    {
        if (!player.IsMoving) return;
        var remaining = (new Vector2(player.Position.X, player.Position.Y) - player.VisualPosition) * 32;
        var distance = remaining.Length();
        if (distance <= 0 || distance >= 32) return;
        var surfacePosition = new MapPosition((int)feet.X / 32, (int)feet.Y / 32);
        if (!TownMap.Contains(surfacePosition)) return;
        var tile = map.Tile(surfacePosition);
        var grass = tile is 2 or 50;
        if (!grass && tile is not (8 or 22 or 25 or 26)) return;
        var direction = remaining / distance;
        var side = new Vector2(-direction.Y, direction.X);
        var progress = 1 - distance / 32;
        for (var foot = 0; foot < 2; foot++)
        {
            var age = (progress - foot * 0.45f - 0.05f) / 0.6f;
            if (age <= 0 || age >= 1) continue;
            var p = feet - direction * MathF.Min(32 - distance, 3 + age * 5)
                + side * (foot == 0 ? -3 : 3);
            var alpha = (1 - age) * (grass ? 0.4f : 0.22f);
            var color = Palette[grass ? 12 : 18] * alpha;
            batch.Draw(pixel, new Rectangle((int)MathF.Round(p.X), (int)MathF.Round(p.Y) - (grass ? 2 : 1),
                grass ? 1 : 2 + (int)(age * 2), grass ? 2 : 1), color);
        }
    }

    private void DrawLeaves(SpriteBatch batch, double time)
    {
        var tick = Math.Floor(time * 12) / 12;
        for (var i = 0; i < 48; i++)
        {
            var seed = i * 97 + 31;
            var age = (float)((tick + i * 3.71) % (14 + i % 7));
            if (age >= 12) continue;
            var fall = Math.Min(age / 7, 1);
            var landX = 20 + seed * 37 % (WorldWidth - 40);
            var landY = 48 + seed * 53 % (WorldHeight - 64);
            var x = landX - (int)((1 - fall) * 28) + (int)(MathF.Sin(age * 2.1f + seed) * 5 * (1 - fall));
            var y = landY - (int)((1 - fall) * 42);
            var fade = Math.Min(1, age * 3) * Math.Clamp((12 - age) / 2, 0, 1);
            // Leaves blow across open ground, then settle briefly. Draw before
            // buildings and actors so they cannot cover a doorway or character.
            var cell = new MapPosition(x / 32, y / 32);
            if (x < 1 || y < 1 || x + 3 >= WorldWidth || y + 3 >= WorldHeight || !TownMap.Contains(cell)
                || map.Tile(cell) is not (2 or 8 or 22 or 25 or 26 or 50)) continue;
            var color = Palette[i % 4 == 0 ? 17 : i % 4 == 1 ? 35 : 12] * fade;
            var edge = Palette[i % 4 < 2 ? 16 : 10] * fade;
            var turn = age < 7 ? (int)(age * 5 + i) % 4 : i % 4;
            if (turn == 2)
            {
                batch.Draw(pixel, new Rectangle(x + 1, y, 1, 3), edge);
                batch.Draw(pixel, new Rectangle(x + 1, y + 1, 1, 1), color);
            }
            else
            {
                batch.Draw(pixel, new Rectangle(x + 1, y, 2, 1), color);
                batch.Draw(pixel, new Rectangle(x, y + 1, turn == 0 ? 4 : 3, 1), color);
                batch.Draw(pixel, new Rectangle(x + (turn == 3 ? 2 : 1), y + 2, 1, 1), edge);
            }
        }
    }

    internal (float Gain, float Pan) RiverSoundAt(Vector2 position)
    {
        var feet = position * 32 + new Vector2(16, 25);
        const int reach = 104;
        var nearest = (float)(reach * reach); var riverX = feet.X;
        // Sample the rendered water mask, including the bends and water under
        // both bridges. A small nearby grid avoids scanning every river pixel.
        for (var y = Math.Max(0, (int)(feet.Y - reach) / 4 * 4); y < Math.Min(WorldHeight, feet.Y + reach); y += 4)
            for (var x = Math.Max(0, (int)(feet.X - reach) / 4 * 4); x < Math.Min(WorldWidth, feet.X + reach); x += 4)
            {
                if (!waterMask[y * WorldWidth + x]) continue;
                var distance = Vector2.DistanceSquared(feet, new(x, y));
                if (distance >= nearest) continue;
                nearest = distance; riverX = x;
            }
        var amount = Math.Clamp((MathF.Sqrt(nearest) - 12) / (reach - 12), 0, 1);
        return (1 - MathHelper.SmoothStep(0, 1, amount), Math.Clamp((riverX - feet.X) / 80, -.65f, .65f));
    }
}
