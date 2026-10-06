namespace Microsoft.Xna.Framework
{
    internal readonly record struct Color(byte R, byte G, byte B, byte A = 255);
    internal readonly record struct Rectangle(int X, int Y, int Width, int Height);
}
namespace Microsoft.Xna.Framework.Graphics
{
    using Microsoft.Xna.Framework;
    internal sealed class GraphicsDevice { }
    internal sealed class Texture2D : IDisposable
    {
        internal readonly int Width, Height;
        internal Color[] Pixels;
        internal Texture2D(GraphicsDevice device, int width, int height)
        { Width = width; Height = height; Pixels = new Color[width * height]; }
        internal void GetData(Color[] data) => Pixels.CopyTo(data, 0);
        internal void SetData(Color[] data) => Pixels = (Color[])data.Clone();
        internal void GetData(int level, Rectangle rectangle, Color[] data, int start, int count)
        {
            for (var y = 0; y < rectangle.Height; y++)
                Array.Copy(Pixels, (rectangle.Y + y) * Width + rectangle.X, data, start + y * rectangle.Width, rectangle.Width);
        }
        public void Dispose() { }
    }
}
