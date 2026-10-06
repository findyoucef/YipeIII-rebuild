using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Yipe.Desktop;

/// <summary>Gentle tilt-shift at presentation resolution, with continuously varying sample offsets.</summary>
internal sealed class MiniatureEffect : IDisposable
{
    private const int PresentationScale = 4;
    private readonly GraphicsDevice device;
    private readonly RenderTarget2D enlarged;
    private readonly RenderTarget2D horizontal;
    private readonly RenderTarget2D blurred;
    private readonly BasicEffect sampling;
    private readonly VertexPositionColorTexture[] vertices;
    private readonly BlendState accumulate = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    public RenderTarget2D World { get; }
    // The horizontal intermediate can hold the final composite after the vertical pass.
    public RenderTarget2D Result => horizontal;

    public MiniatureEffect(GraphicsDevice device, int width, int height)
    {
        this.device = device;
        World = new RenderTarget2D(device, width, height);
        width *= PresentationScale;
        height *= PresentationScale;
        enlarged = new RenderTarget2D(device, width, height);
        horizontal = new RenderTarget2D(device, width, height);
        blurred = new RenderTarget2D(device, width, height);
        vertices = new VertexPositionColorTexture[height * 5 * 6];
        sampling = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            Projection = Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1),
        };
    }

    public void Apply(SpriteBatch batch, float focusY, bool strong, float blurMix = 1f)
    {
        var target = blurMix == 0 ? Result : enlarged;
        device.SetRenderTarget(target);
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(World, target.Bounds, Color.White);
        batch.End();
        if (blurMix == 0) return;
        Blur(enlarged, horizontal, focusY, strong, vertical: false);
        Blur(horizontal, blurred, focusY, strong, vertical: true);

        device.SetRenderTarget(Result);
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(enlarged, Vector2.Zero, Color.White);
        // Keep plenty of original detail even in the furthest rows. Preserve the art's colors.
        for (var y = 0; y < Result.Height; y++)
        {
            var amount = Falloff((y + 0.5f) / PresentationScale, focusY) * (strong ? 0.75f : 0.5f) * blurMix;
            if (amount <= 0) continue;
            var row = new Rectangle(0, y, Result.Width, 1);
            batch.Draw(blurred, row, row, Color.White * amount);
        }
        batch.End();
    }

    private void Blur(Texture2D source, RenderTarget2D target, float focusY, bool strong, bool vertical)
    {
        var width = target.Width;
        var height = target.Height;
        var index = 0;
        for (var y = 0; y < height; y++)
        {
            var radius = Falloff((y + 0.5f) / PresentationScale, focusY)
                * (strong ? 1.2f : 0.8f) * PresentationScale;
            for (var tap = -2; tap <= 2; tap++)
            {
                var weight = tap switch { -2 or 2 => 16, -1 or 1 => 64, _ => 95 };
                var color = new Color(weight, weight, weight, weight);
                var du = vertical ? 0 : tap * radius / width;
                var dv = vertical ? tap * radius / height : 0;
                var top = y / (float)height + dv;
                var bottom = (y + 1f) / height + dv;
                var a = new VertexPositionColorTexture(new(0, y, 0), color, new(du, top));
                var b = new VertexPositionColorTexture(new(width, y, 0), color, new(1 + du, top));
                var c = new VertexPositionColorTexture(new(0, y + 1, 0), color, new(du, bottom));
                var d = new VertexPositionColorTexture(new(width, y + 1, 0), color, new(1 + du, bottom));
                vertices[index++] = a; vertices[index++] = b; vertices[index++] = c;
                vertices[index++] = b; vertices[index++] = d; vertices[index++] = c;
            }
        }

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);
        device.BlendState = accumulate;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        sampling.Texture = source;
        sampling.CurrentTechnique.Passes[0].Apply();
        device.SamplerStates[0] = SamplerState.LinearClamp;
        device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
    }

    private static float Falloff(float y, float focusY)
    {
        // A broad Gaussian falloff has no outer cutoff or fully blurred band.
        var distance = MathF.Max(0, MathF.Abs(y - focusY) - 18) / 85;
        return 1 - MathF.Exp(-distance * distance);
    }

    public void Dispose()
    {
        World.Dispose();
        enlarged.Dispose();
        horizontal.Dispose();
        blurred.Dispose();
        sampling.Dispose();
        accumulate.Dispose();
    }
}
