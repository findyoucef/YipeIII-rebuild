using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class PixelLighting
{
    private Texture2D? softwareMask;
    private Color[] softwareMaskPixels = [], softwareLightPixels = [];

    private void PrepareSoftware(Adventure game, Matrix camera, double time)
    {
        var texture = caveMask ?? mask;
        if (!ReferenceEquals(softwareMask, texture))
        {
            softwareMaskPixels = new Color[texture.Width * texture.Height];
            texture.GetData(softwareMaskPixels);
            softwareMask = texture;
        }
        if (softwareLightPixels.Length == 0) softwareLightPixels = new Color[lightMap.Width * lightMap.Height];
        Array.Clear(softwareLightPixels);
        foreach (var patch in patches)
        {
            var light = lights[patch.Light];
            if (!game.Visibility.Contains(light.Cell) || !game.Visibility.Contains(patch.Cell)) continue;
            var color = light.Color * (Flicker(light, time) * (light.Window ? 1 + NightAmount * 1.8f : 1));
            var destination = patch.Destination;
            // The game's camera uses whole-pixel translation and these patches are never scaled.
            destination.Offset((int)camera.M41, (int)camera.M42);
            var visible = Rectangle.Intersect(destination, lightMap.Bounds);
            for (var y = visible.Top; y < visible.Bottom; y++)
                for (var x = visible.Left; x < visible.Right; x++)
                {
                    var source = softwareMaskPixels[(patch.Source.Y + y - destination.Y) * texture.Width
                        + patch.Source.X + x - destination.X];
                    ref var result = ref softwareLightPixels[y * lightMap.Width + x];
                    result = new Color(
                        Math.Max(result.R, (source.R * color.R + 127) / 255),
                        Math.Max(result.G, (source.G * color.G + 127) / 255),
                        Math.Max(result.B, (source.B * color.B + 127) / 255),
                        Math.Max(result.A, (source.A * color.A + 127) / 255));
                }
        }
        device.SetRenderTarget(null);
        lightMap.SetData(softwareLightPixels);
    }
}
