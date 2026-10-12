using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>World-anchored rain driven only by the pausable scene clock.</summary>
internal static class Rain
{
    private static readonly Color Drop = new(153, 185, 199);
    private static readonly Color Splash = new(190, 209, 210);
    private const float FallTime = .42f;
    private const float SplashTime = .28f;

    public static float WeatherAmount(double outdoorTime)
    {
        // A separate deterministic weather sequence never consumes gameplay randomness.
        // Reconstructing from one clock also keeps frame rate and map changes irrelevant.
        for (uint shower = 0; ; shower++)
        {
            var dry = 300 + Hash(unchecked(shower * 2 + 17)) % 301;
            var wet = 30 + Hash(unchecked(shower * 2 + 18)) % 31;
            if (outdoorTime <= dry) return 0;
            outdoorTime -= dry;
            if (outdoorTime < wet)
                return MathHelper.SmoothStep(0, 1, (float)Math.Min(outdoorTime / 4, (wet - outdoorTime) / 4));
            outdoorTime -= wet;
        }
    }

    public static void DrawStreaks(SpriteBatch batch, Texture2D pixel, double time, float strength,
        Func<int, int, bool>? snowAt = null)
    {
        for (var i = 0; i < strength * 156; i++)
        {
            var (point, age) = Sample(i, time);
            if (age >= FallTime) continue;
            var opacity = Math.Clamp(strength * 156 - i, 0, 1);
            var remaining = 1 - age / FallTime;
            var x = point.X - (int)MathF.Round(remaining * 12);
            var y = point.Y - (int)MathF.Round(remaining * 52);
            // A short stepped diagonal, with a dim tail and no filtering.
            DropPixel(x - 1, y - 3, .22f); DropPixel(x - 1, y - 2, .22f);
            DropPixel(x, y - 1, .46f); DropPixel(x, y, .46f);
            void DropPixel(int px, int py, float alpha)
            {
                if (snowAt?.Invoke(px, py) != true)
                    batch.Draw(pixel, new Rectangle(px, py, 1, 1), Drop * (alpha * opacity));
            }
        }
    }

    public static void DrawSplashes(SpriteBatch batch, Texture2D pixel, double time, float strength,
        Func<MapPosition, ushort> tileAt, Func<int, int, bool> waterAt, Func<int, int, bool>? snowAt = null)
    {
        for (var i = 0; i < strength * 156; i++)
        {
            var (point, age) = Sample(i, time);
            if (age < FallTime || age >= FallTime + SplashTime) continue;
            if (snowAt?.Invoke(point.X, point.Y) == true) continue;
            var tile = tileAt(new(point.X / 32, point.Y / 32));
            // Bridge decks and solid scenery receive no ground-level impacts.
            if (tile is 0x0b or 0x58 or 0x59) continue;
            var progress = (age - FallTime) / SplashTime;
            var tint = Splash * (.5f * (1 - progress) * Math.Clamp(strength * 156 - i, 0, 1));
            var radius = 1 + (int)(progress * 3);
            if (waterAt(point.X, point.Y))
            {
                // Flat, expanding pixel rings; clip each pixel to the real shoreline.
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var dy = Math.Abs(dx) == radius ? 0 : radius == 3 && Math.Abs(dx) < 2 ? 2 : 1;
                    WaterPixel(point.X + dx, point.Y - dy);
                    if (dy != 0) WaterPixel(point.X + dx, point.Y + dy);
                }
            }
            else if (tile is 2 or 8 or >= 0x16 and <= 0x1a or 0x2d or 0x42)
            {
                var lift = progress < .5f ? 1 : 0;
                GroundPixel(point.X - radius, point.Y - lift);
                GroundPixel(point.X + radius, point.Y - lift);
            }

            void GroundPixel(int x, int y)
            {
                if (snowAt?.Invoke(x, y) != true) batch.Draw(pixel, new Rectangle(x, y, 1, 1), tint);
            }

            void WaterPixel(int x, int y)
            {
                if (!waterAt(x, y)) return;
                if (snowAt?.Invoke(x, y) == true) return;
                if (tileAt(new(x / 32, y / 32)) is 0x0b or 0x58 or 0x59) return;
                batch.Draw(pixel, new Rectangle(x, y, 1, 1), tint);
            }
        }
    }

    private static (Point Point, float Age) Sample(int index, double time)
    {
        var seed = Hash((uint)index + 1);
        var period = .95 + (seed % 401) / 1000d;
        var clock = time / period + (seed % 997) / 997d;
        var cycle = (uint)(Math.Floor(clock) % uint.MaxValue);
        var position = Hash(unchecked(seed + cycle * 374761393u));
        var x = (int)(position % (Map.Width * 32));
        var y = (int)(Hash(position) % (Map.Height * 32));
        return (new(x, y), (float)((clock - Math.Floor(clock)) * period));
    }

    private static uint Hash(uint value)
    {
        unchecked
        {
            value = (value ^ (value >> 13)) * 1274126177u;
            return value ^ (value >> 16);
        }
    }
}
