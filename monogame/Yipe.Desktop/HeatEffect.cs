using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Yipe.Desktop;

/// <summary>Small, smooth refractions of hot scenery, before the sharp UI is composited.</summary>
internal sealed class HeatEffect : IDisposable
{
    private const int Scale = 4, Step = 4;
    private readonly GraphicsDevice device;
    private readonly RenderTarget2D enlarged, result;
    private readonly BasicEffect sampling;
    private readonly VertexPositionColorTexture[] vertices;

    public HeatEffect(GraphicsDevice device, int width, int height)
    {
        this.device = device;
        enlarged = new RenderTarget2D(device, width * Scale, height * Scale);
        result = new RenderTarget2D(device, width * Scale, height * Scale);
        vertices = new VertexPositionColorTexture[width / Step * (height / Step) * 6];
        sampling = new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            Projection = Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1),
        };
    }

    public Texture2D Apply(SpriteBatch batch, Texture2D source, WorldRenderer world,
        Vector2 camera, Vector2 hero, double time, bool crisp = false, float strength = 1)
    {
        // Enlarge with point sampling first: subpixel refraction should not blur native art.
        device.SetRenderTarget(enlarged);
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(source, enlarged.Bounds, Color.White);
        batch.End();

        var width = result.Width / Scale;
        var height = result.Height / Scale;
        var t = (float)time;
        var index = 0;
        for (var y = 0; y < height; y += Step)
            for (var x = 0; x < width; x += Step)
            {
                var a = Vertex(x, y); var b = Vertex(x + Step, y);
                var c = Vertex(x, y + Step); var d = Vertex(x + Step, y + Step);
                vertices[index++] = a; vertices[index++] = b; vertices[index++] = c;
                vertices[index++] = b; vertices[index++] = d; vertices[index++] = c;
            }

        device.SetRenderTarget(result);
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        sampling.Texture = enlarged;
        sampling.CurrentTechnique.Passes[0].Apply();
        // Hell keeps hard pixel edges as the enlarged image ripples. Sampling
        // at presentation resolution still permits quarter-native-pixel motion.
        device.SamplerStates[0] = crisp ? SamplerState.PointClamp : SamplerState.LinearClamp;
        device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, index / 3);
        return result;

        VertexPositionColorTexture Vertex(float x, float y)
        {
            var p = new Vector2(x, y) + camera;
            var distance = ((p - hero) / new Vector2(24, 32)).Length();
            var calm = MathHelper.SmoothStep(0, 1, Math.Clamp((distance - 1) / .65f, 0, 1));
            var edge = Math.Min(Math.Min(x, width - x), Math.Min(y, height - y));
            var amount = world.HeatAt(p) * calm * MathHelper.SmoothStep(0, 1, Math.Clamp(edge / 8, 0, 1)) * strength;
            // Two broad waves drift upward at different speeds. World coordinates keep
            // them attached to the landscape during camera movement, without screen shake.
            var dx = amount * (.7f * MathF.Sin(p.Y * .17f + t * 2.3f)
                + .3f * MathF.Sin(p.X * .07f + p.Y * .09f + t * 1.4f));
            var dy = amount * .16f * MathF.Sin(p.X * .10f + p.Y * .13f + t * 1.8f);
            return new(new(x, y, 0), Color.White, new((x + dx) / width, (y + dy) / height));
        }
    }

    public void Dispose()
    {
        enlarged.Dispose(); result.Dispose(); sampling.Dispose();
    }
}
