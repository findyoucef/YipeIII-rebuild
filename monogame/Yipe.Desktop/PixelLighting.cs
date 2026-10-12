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
    private readonly WorldRenderer world;
    private readonly Texture2D mask, pixel;
    private Texture2D? caveMask;
    private readonly RenderTarget2D lightMap;
    private readonly BlendState ambient = new()
    {
        ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };
    public float NightAmount { get; set; }
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
    private readonly record struct Light(MapPosition Cell, Point Position, Color Color, bool Torch, bool Fire, bool Window);

    public PixelLighting(GraphicsDevice device, Texture2D pixel, int width, int height, WorldRenderer world)
    {
        this.device = device; this.pixel = pixel; this.world = world;
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
            batch.Draw(caveMask ?? mask, patch.Destination, patch.Source,
                light.Color * (Flicker(light, time) * (light.Window ? 1 + NightAmount * 1.8f : 1)));
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

    public void ApplyAmbient(SpriteBatch batch, Color color)
    {
        if (color == Color.White) return;
        batch.Begin(blendState: ambient, samplerState: SamplerState.PointClamp);
        batch.Draw(pixel, lightMap.Bounds, color);
        batch.End();
    }

    public void ApplyFireAmbient(SpriteBatch batch, bool deep)
    {
        // Red bounce from the glowing joints fills the room. Both passes scale
        // existing pixels, retaining dark outlines and keeping black/alpha intact.
        ApplyAmbient(batch, new Color(255, deep ? 185 : 198, deep ? 153 : 169));
        batch.Begin(blendState: illuminate, samplerState: SamplerState.PointClamp);
        batch.Draw(pixel, lightMap.Bounds, new Color(deep ? 100 : 80, 4, 0));
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
        caveMask?.Dispose(); caveMask = null;
        var breeding = WorldRenderer.IsBreeding(map);
        var volcanic = WorldRenderer.IsFire(map);
        var earth = WorldRenderer.IsEarth(map);
        var stone = WorldRenderer.IsStone(map);
        var ice = WorldRenderer.IsIce(map);
        var flooded = WorldRenderer.IsWaterCave(map);
        var final = WorldRenderer.IsFinal(map);
        var contour = breeding || volcanic || earth || stone || ice || flooded || final;
        var garnets = final ? world.FinalLights(map) : [];
        var algae = flooded ? world.WaterCaveLights(map) : [];
        var crystals = ice ? world.IceLights(map) : [];
        var magma = volcanic ? world.FireLights(map) : [];
        var fungi = earth ? world.EarthFungi(map) : breeding ? WorldRenderer.BreedingFungi(map.Id) : [];
        var lamps = stone ? world.StoneLamps(map) : earth ? world.EarthLamps(map) : breeding ? WorldRenderer.BreedingLamps(map.Id) : [];
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
            {
                var cell = new MapPosition(x, y); var tile = Terrain(map.Tile(cell));
                var window = tile is 6 or 10;
                var molten = volcanic && tile == 0x25 && magma.Any(p => p.X / 32 == x && p.Y / 32 == y);
                var fire = tile == 0x24 || molten;
                var fungus = (breeding || earth) && tile == (earth ? 0x22 : 0x23) && fungi.Any(p => p.X / 32 == x && p.Y / 32 == y);
                var crystal = ice && tile is 0x22 or 0x23 && crystals.Any(p => p.X / 32 == x && p.Y / 32 == y);
                var garnet = final && tile == 0x22 && garnets.Any(p => p.X / 32 == x && p.Y / 32 == y);
                var luminous = flooded && tile == 0x25 && algae.Any(p => p.X / 32 == x && p.Y / 32 == y);
                var torch = breeding || earth || stone ? tile == (earth ? 0x22 : 0x23) && lamps.Any(p => p.X / 32 == x && p.Y / 32 == y)
                    : !volcanic && !ice && !flooded && !final && map.Id is >= 41 and < 117 && tile is 0x22 or 0x23 or 0x25
                    && y + 1 < Map.Height && Terrain(map.Tile(new(x, y + 1))) is 0x27 or 0x28 or 0x29 or 0x2a
                    && (x * 3 + y * 5 + map.Id) % 7 == 0;
                if (!window && !fire && !torch && !fungus && !crystal && !luminous && !garnet) continue;
                var position = new Point(x * 32 + (window ? 27 : 16), y * 32 + (window ? 19 : 20));
                if (garnet) position = garnets.First(p => p.X / 32 == x && p.Y / 32 == y);
                else if (molten) position = magma.First(p => p.X / 32 == x && p.Y / 32 == y);
                else if (crystal) position = crystals.First(p => p.X / 32 == x && p.Y / 32 == y);
                else if (luminous) position = algae.First(p => p.X / 32 == x && p.Y / 32 == y);
                else if (fungus) position = fungi.First(p => p.X / 32 == x && p.Y / 32 == y);
                else if ((breeding || earth || stone) && torch) position = lamps.First(p => p.X / 32 == x && p.Y / 32 == y);
                if (fire && map.Id is >= 117 and <= 120)
                {
                    var feet = WorldRenderer.HellFlameFeet(map, cell);
                    position = new((int)feet.X, (int)feet.Y - 3);
                }
                var color = garnet ? new Color(106, 16, 30) : luminous ? new Color(20, 62, 53) : crystal ? new Color(24, 48, 67) : molten ? new Color(140, 82, 26) : fungus ? earth ? new Color(32, 48, 22) : new Color(17, 43, 39) : window ? new Color(60, 43, 20)
                    : torch ? new Color(83, 51, 22) : new Color(86, 36, 10);
                var index = lights.Count;
                lights.Add(new(cell, position, color, torch, fire || torch || tile == 6, window));
                var kind = window ? 0 : torch ? 1 : 2;
                var radius = Radii[kind];
                var bounds = new Rectangle(position.X - MaskSize / 2, position.Y - MaskSize / 2, MaskSize, MaskSize);
                var reach = new Rectangle(position.X - radius, position.Y - radius, radius * 2, radius * 2);
                if (!contour && (window || torch))
                {
                    reach.Height -= position.Y - 8 - reach.Y;
                    reach.Y = position.Y - 8;
                }
                for (var cy = Math.Max(0, reach.Top / 32); cy <= Math.Min(Map.Height - 1, reach.Bottom / 32); cy++)
                    for (var cx = Math.Max(0, reach.Left / 32); cx <= Math.Min(Map.Width - 1, reach.Right / 32); cx++)
                    {
                        var target = new MapPosition(cx, cy);
                        if (!contour && !Reaches(map, cell, target)) continue;
                        var destination = Rectangle.Intersect(new(cx * 32, cy * 32, 32, 32), reach);
                        if (destination.Width <= 0 || destination.Height <= 0) continue;
                        patches.Add((index, target, destination,
                            new(destination.X - bounds.X, destination.Y - bounds.Y + (contour ? index : kind) * MaskSize,
                                destination.Width, destination.Height)));
                    }
            }
        if (contour && lights.Count > 0) BuildCaveMask(map);
    }

    private void BuildCaveMask(Map map)
    {
        // Share the rendered silhouette. Bake only when terrain changes; fog and
        // flicker still use the existing per-cell patches each frame.
        var volcanic = WorldRenderer.IsFire(map);
        var earth = WorldRenderer.IsEarth(map);
        var ice = WorldRenderer.IsIce(map);
        var flooded = WorldRenderer.IsWaterCave(map);
        var final = WorldRenderer.IsFinal(map);
        var rockTile = volcanic || flooded ? 0x25 : earth || final ? 0x22 : 0x23;
        var stone = final ? world.FinalRock(map) : flooded ? world.WaterCaveRock(map) : ice ? world.IceStone(map) : volcanic ? world.FireStone(map) : earth ? world.EarthStone(map)
            : WorldRenderer.IsStone(map) ? world.StoneRock(map) : world.BreedingStone(map);
        var data = new Color[MaskSize * MaskSize * lights.Count];
        for (var index = 0; index < lights.Count; index++)
        {
            var light = lights[index];
            var radius = Radii[light.Window ? 0 : light.Torch ? 1 : 2];
            var left = light.Position.X - MaskSize / 2; var top = light.Position.Y - MaskSize / 2;
            var visible = new bool[MaskSize * MaskSize];
            for (var y = 0; y < MaskSize; y++)
                for (var x = 0; x < MaskSize; x++)
                {
                    var px = left + x; var py = top + y;
                    if (px < 0 || px >= Map.Width * 32 || py < 0 || py >= Map.Height * 32) continue;
                    visible[y * MaskSize + x] = ReachesPixel(light.Position, px, py);
                }
            for (var y = 1; y < MaskSize - 1; y++)
                for (var x = 1; x < MaskSize - 1; x++)
                {
                    var i = y * MaskSize + x;
                    var distance = new Vector2(x + .5f - MaskSize / 2, y + .5f - MaskSize / 2).Length() / radius;
                    var level = Math.Clamp((int)MathF.Ceiling((1 - distance) * 4), 0, 4);
                    // Feather the occlusion by one native pixel, without blurring
                    // the artwork or creating a 32-pixel rectangular light edge.
                    var coverage = (visible[i] ? 2 : 0) + (visible[i - 1] ? 1 : 0) + (visible[i + 1] ? 1 : 0)
                        + (visible[i - MaskSize] ? 1 : 0) + (visible[i + MaskSize] ? 1 : 0);
                    data[index * MaskSize * MaskSize + i] = Color.White * (level / 4f * coverage / 6f);
                }
        }
        caveMask = new Texture2D(device, MaskSize, MaskSize * lights.Count);
        caveMask.SetData(data);

        bool ReachesPixel(Point source, int x, int y)
        {
            var dx = x - source.X; var dy = y - source.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            var leftWall = false; var penetration = 0;
            for (var step = 0; step <= steps; step++)
            {
                var fraction = steps == 0 ? 0 : step / (float)steps;
                var px = (int)MathF.Round(source.X + dx * fraction); var py = (int)MathF.Round(source.Y + dy * fraction);
                var tile = Terrain(map.Tile(new(px / 32, py / 32)));
                var wall = ice ? tile is 0x22 or 0x23 : tile == rockTile;
                var rock = wall && stone[py * Map.Width * 32 + px];
                var obstacle = !wall && Blocked(tile);
                if (!rock && !obstacle) leftWall = true;
                // Wall-mounted sources may leave their own rock. Once in open
                // air, light catches the next face but cannot pass through it.
                if (penetration > 0 || obstacle || (leftWall && rock))
                    if (++penetration > 2) return false;
            }
            return true;
        }
    }

    private static ushort Terrain(ushort tile) => tile switch
    {
        0x43 => 0x22, 0x44 => 0x23, 0x5a => 0x25, 0x45 => 3, 0x46 => 2,
        0x47 => 0x27, 0x48 => 0x28, 0x42 => 0x2d, _ => tile,
    };

    private static bool Reaches(Map map, MapPosition from, MapPosition to)
    {
        bool Solid(int x, int y) => Blocked(Terrain(map.Tile(new(x, y))));
        var x = from.X; var y = from.Y;
        var dx = Math.Abs(to.X - x); var dy = Math.Abs(to.Y - y);
        var sx = Math.Sign(to.X - x); var sy = Math.Sign(to.Y - y);
        var error = dx - dy;
        while (x != to.X || y != to.Y)
        {
            var twice = error * 2; var nx = x; var ny = y;
            if (twice > -dy) { error -= dy; nx += sx; }
            if (twice < dx) { error += dx; ny += sy; }
            if (nx != x && ny != y && (Solid(nx, y) || Solid(x, ny))) return false;
            x = nx; y = ny;
            if ((x != to.X || y != to.Y) && Solid(x, y)) return false;
        }
        return true;
    }

    private static bool Blocked(ushort tile) => tile is 1 or 3 or 4 or 5 or 6 or 7 or 10
        or 0x22 or 0x23 or 0x24 or 0x25 or 0x30 or 0x39 or 0x3e or 0x3f or 0x40;

    public void Dispose()
    {
        mask.Dispose(); caveMask?.Dispose(); lightMap.Dispose(); maximum.Dispose(); illuminate.Dispose(); ambient.Dispose();
    }
}
