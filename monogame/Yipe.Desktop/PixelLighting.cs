using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Local, stepped light that brightens existing world pixels before presentation effects.</summary>
internal sealed class PixelLighting : IDisposable
{
    private const int MaskSize = 128;
    private static readonly int[] Radii = [36, 56, 44];
    private readonly GraphicsDevice device;
    private readonly Texture2D mask, pixel;
    private readonly RenderTarget2D lightMap;
    private readonly BlendState maximum = new()
    {
        ColorBlendFunction = BlendFunction.Max, ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Max, AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One,
    };
    private readonly BlendState illuminate = new()
    {
        ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };
    private readonly ushort[] tiles = new ushort[Map.TileCount];
    private readonly List<Light> lights = new();
    private readonly List<(int Light, MapPosition Cell, Rectangle Destination, Rectangle Source)> patches = new();
    private Map? map;
    private readonly record struct Light(MapPosition Cell, Point Position, Color Color, bool Torch, bool Fire);

    public PixelLighting(GraphicsDevice device, Texture2D pixel, int width, int height)
    {
        this.device = device; this.pixel = pixel;
        lightMap = new RenderTarget2D(device, width, height);
        mask = new Texture2D(device, MaskSize, MaskSize * Radii.Length);
        var data = new Color[MaskSize * MaskSize * Radii.Length];
        for (var row = 0; row < Radii.Length; row++)
            for (var y = 0; y < MaskSize; y++)
                for (var x = 0; x < MaskSize; x++)
                {
                    var distance = new Vector2(x + .5f - MaskSize / 2, y + .5f - MaskSize / 2).Length() / Radii[row];
                    var level = Math.Clamp((int)MathF.Ceiling((1 - distance) * 4), 0, 4);
                    data[(row * MaskSize + y) * MaskSize + x] = Color.White * (level / 4f);
                }
        mask.SetData(data);
    }

    public void Prepare(SpriteBatch batch, Adventure game, Matrix camera, double time)
    {
        var changed = !ReferenceEquals(map, game.Map);
        for (var x = 0; x < Map.Width; x++)
            for (var y = 0; y < Map.Height; y++)
            {
                var i = x * Map.Height + y; var tile = game.Map.Tile(new(x, y));
                changed |= tiles[i] != tile; tiles[i] = tile;
            }
        if (changed) Build(game.Map);
        device.SetRenderTarget(lightMap);
        device.Clear(Color.Transparent);
        // Max blending caps overlapping fire sources instead of washing out dense Hell walls.
        batch.Begin(blendState: maximum, samplerState: SamplerState.PointClamp, transformMatrix: camera);
        foreach (var patch in patches)
        {
            var light = lights[patch.Light];
            if (!game.Visibility.Contains(light.Cell) || !game.Visibility.Contains(patch.Cell)) continue;
            batch.Draw(mask, patch.Destination, patch.Source, light.Color * Flicker(light, time));
        }
        batch.End();
    }

    public void DrawTorches(SpriteBatch batch, MapVisibility visibility, double time)
    {
        foreach (var light in lights)
        {
            if (!light.Torch || !visibility.Contains(light.Cell)) continue;
            var x = light.Position.X; var y = light.Position.Y;
            var height = Flicker(light, time) > .93f ? 5 : 4;
            batch.Draw(pixel, new Rectangle(x - 3, y - 1, 7, 10), RetroArt.Palette[0]);
            batch.Draw(pixel, new Rectangle(x - 1, y, 3, 7), RetroArt.Palette[3]);
            batch.Draw(pixel, new Rectangle(x - 2, y - height, 5, height + 2), RetroArt.Palette[21]);
            batch.Draw(pixel, new Rectangle(x - 1, y - height + 1, 3, height), RetroArt.Palette[23]);
            batch.Draw(pixel, new Rectangle(x, y - 2, 1, 3), RetroArt.Palette[6]);
        }
    }

    public void Apply(SpriteBatch batch)
    {
        // Multiplying the existing colors preserves outlines and adds no glow over empty space.
        batch.Begin(blendState: illuminate, samplerState: SamplerState.PointClamp);
        batch.Draw(lightMap, Vector2.Zero, Color.White);
        batch.End();
    }

    private static float Flicker(Light light, double time)
    {
        if (!light.Fire) return 1;
        var phase = (float)(Math.Floor(time * 6) / 6) * 2.4f + light.Cell.X * 1.7f + light.Cell.Y * 2.3f;
        return .92f + .05f * MathF.Sin(phase) + .03f * MathF.Sin(phase * 2.1f);
    }

    private void Build(Map next)
    {
        map = next; lights.Clear(); patches.Clear();
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var cell = new MapPosition(x, y); var tile = Terrain(map.Tile(cell));
                var window = tile is 6 or 10;
                var fire = tile == 0x24;
                var torch = map.Id is >= 41 and < 117 && tile is 0x22 or 0x23 or 0x25
                    && y + 1 < Map.Height && Terrain(map.Tile(new(x, y + 1))) is 0x27 or 0x28 or 0x29 or 0x2a
                    && (x * 3 + y * 5 + map.Id) % 7 == 0;
                if (!window && !fire && !torch) continue;
                var position = new Point(x * 32 + (window ? 27 : 16), y * 32 + (window ? 19 : 20));
                if (fire && map.Id is >= 117 and <= 120)
                {
                    var feet = WorldRenderer.HellFlameFeet(map, cell);
                    position = new((int)feet.X, (int)feet.Y - 3);
                }
                var color = window ? new Color(60, 43, 20) : torch ? new Color(83, 51, 22) : new Color(86, 36, 10);
                var index = lights.Count;
                lights.Add(new(cell, position, color, torch, fire || torch || tile == 6));
                var kind = window ? 0 : torch ? 1 : 2;
                var radius = Radii[kind];
                var bounds = new Rectangle(position.X - MaskSize / 2, position.Y - MaskSize / 2, MaskSize, MaskSize);
                var reach = new Rectangle(position.X - radius, position.Y - radius, radius * 2, radius * 2);
                if (window || torch)
                {
                    reach.Height -= position.Y - 8 - reach.Y;
                    reach.Y = position.Y - 8;
                }
                for (var cy = Math.Max(0, reach.Top / 32); cy <= Math.Min(Map.Height - 1, reach.Bottom / 32); cy++)
                    for (var cx = Math.Max(0, reach.Left / 32); cx <= Math.Min(Map.Width - 1, reach.Right / 32); cx++)
                    {
                        var target = new MapPosition(cx, cy);
                        if (!Reaches(map, cell, target)) continue;
                        var destination = Rectangle.Intersect(new(cx * 32, cy * 32, 32, 32), reach);
                        if (destination.Width <= 0 || destination.Height <= 0) continue;
                        patches.Add((index, target, destination,
                            new(destination.X - bounds.X, destination.Y - bounds.Y + kind * MaskSize,
                                destination.Width, destination.Height)));
                    }
            }
    }

    private static ushort Terrain(ushort tile) => tile switch
    {
        0x43 => 0x22, 0x44 => 0x23, 0x5a => 0x25, 0x45 => 3, 0x46 => 2,
        0x47 => 0x27, 0x48 => 0x28, 0x42 => 0x2d, _ => tile,
    };

    private static bool Reaches(Map map, MapPosition from, MapPosition to)
    {
        bool Blocked(int x, int y) => Terrain(map.Tile(new(x, y))) is 1 or 3 or 4 or 5 or 6 or 7 or 9 or 10
            or 0x22 or 0x23 or 0x24 or 0x25 or 0x30 or 0x39 or 0x3e or 0x3f or 0x40;
        var x = from.X; var y = from.Y;
        var dx = Math.Abs(to.X - x); var dy = Math.Abs(to.Y - y);
        var sx = Math.Sign(to.X - x); var sy = Math.Sign(to.Y - y);
        var error = dx - dy;
        while (x != to.X || y != to.Y)
        {
            var twice = error * 2; var nx = x; var ny = y;
            if (twice > -dy) { error -= dy; nx += sx; }
            if (twice < dx) { error += dx; ny += sy; }
            if (nx != x && ny != y && (Blocked(nx, y) || Blocked(x, ny))) return false;
            x = nx; y = ny;
            if ((x != to.X || y != to.Y) && Blocked(x, y)) return false;
        }
        return true;
    }

    public void Dispose()
    {
        mask.Dispose(); lightMap.Dispose(); maximum.Dispose(); illuminate.Dispose();
    }
}
