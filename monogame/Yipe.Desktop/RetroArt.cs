using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Yipe.Desktop;

internal static class RetroArt
{
    // Snow needs a cold white ramp; the shared atlas highlights are warm cream.
    // Keep this local to winter rendering so other biomes retain their palette.
    internal static readonly Color[] SnowPalette =
    [new(245, 249, 248), new(224, 235, 239), new(194, 214, 225)];

    // One palette for every atlas. Import removes gradients and partial-alpha edge pixels.
    internal static readonly Color[] Palette =
    [
        new(20, 25, 36), new(37, 43, 59), new(64, 73, 87), new(105, 117, 133),
        new(164, 173, 176), new(213, 216, 204), new(241, 220, 158), new(255, 241, 198),
        new(39, 59, 47), new(54, 78, 51), new(73, 99, 72), new(107, 135, 71),
        new(153, 169, 87), new(192, 194, 119), new(65, 44, 37), new(85, 56, 46),
        new(131, 86, 62), new(183, 136, 88), new(217, 184, 124), new(242, 198, 143),
        new(103, 43, 58), new(137, 55, 71), new(191, 86, 82), new(232, 137, 101),
        new(37, 65, 109), new(50, 109, 168), new(114, 164, 194), new(36, 71, 94),
        new(43, 107, 131), new(74, 158, 170), new(145, 199, 188), new(82, 59, 107),
        new(128, 86, 129), new(175, 123, 158), new(155, 115, 47), new(217, 172, 79),
        new(217, 155, 112), new(168, 93, 64), new(113, 71, 45), new(82, 112, 112),
    ];
    internal static Texture2D Load(GraphicsDevice device, string content, string name, int columns, int rows, int cellSize)
    {
        using var stream = File.OpenRead(Path.Combine(content, "Art", "Retro", name));
        using var source = Texture2D.FromStream(device, stream);
        if (source.Width < columns * cellSize || source.Height < rows * cellSize)
            throw new InvalidDataException($"{name} is smaller than its native sprite grid.");
        var sourcePixels = new Color[source.Width * source.Height];
        source.GetData(sourcePixels);
        var width = columns * cellSize;
        var height = rows * cellSize;
        var native = new Color[width * height];
        // Compile the magnified source art once. Rendering thereafter uses only native pixels.
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var left = x * source.Width / width;
                var right = (x + 1) * source.Width / width;
                var top = y * source.Height / height;
                var bottom = (y + 1) * source.Height / height;
                var red = 0;
                var green = 0;
                var blue = 0;
                var opaque = 0;
                for (var sy = top; sy < bottom; sy++)
                    for (var sx = left; sx < right; sx++)
                    {
                        var color = sourcePixels[sy * source.Width + sx];
                        if (color.A < 128) continue;
                        red += color.R; green += color.G; blue += color.B; opaque++;
                    }
                if (opaque * 2 < (right - left) * (bottom - top) || opaque == 0) continue;
                var average = new Color(red / opaque, green / opaque, blue / opaque);
                var bestDistance = int.MaxValue;
                // The cave's rock rim shares the mountain's slate shades, without moss colors.
                var colors = name == "town.png" && x / cellSize == 0 && y / cellSize == 2
                    ? Palette.AsSpan(0, 6) : Palette.AsSpan();
                foreach (var candidate in colors)
                {
                    var dr = average.R - candidate.R;
                    var dg = average.G - candidate.G;
                    var db = average.B - candidate.B;
                    var distance = dr * dr + dg * dg + db * db;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    native[y * width + x] = candidate;
                }
            }
        var character = name == "hero.png" || name.StartsWith("World/heroes-", StringComparison.Ordinal);
        if (character || name is "town.png" or "shops.png" or "details.png" or "World/quests.png" or "World/snow-props.png")
        {
            try { AlignFrames(native, width, columns, rows, cellSize, character, name == "details.png"); }
            catch (InvalidDataException error) { throw new InvalidDataException($"{name}: {error.Message}", error); }
        }
        if (name == "terrain.png") MatchMountainStone(native, width, cellSize);
        if (name == "town.png") SoftenCaveRim(native, width, cellSize);
        var texture = new Texture2D(device, width, height);
        texture.SetData(native);
        return texture;
    }

    private static void MatchMountainStone(Color[] atlas, int width, int size)
    {
        var highlights = new bool[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                highlights[y * size + x] = atlas[(y + size) * width + x + size] == Palette[4];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var index = (y + size) * width + x + size;
                if (atlas[index] == Palette[39]) atlas[index] = Palette[2];
                if (!highlights[y * size + x]) continue;
                // Keep the bright cores of existing clusters, with quieter edges.
                var neighbors = (highlights[y * size + (x + size - 1) % size] ? 1 : 0)
                    + (highlights[y * size + (x + 1) % size] ? 1 : 0)
                    + (highlights[(y + size - 1) % size * size + x] ? 1 : 0)
                    + (highlights[(y + 1) % size * size + x] ? 1 : 0);
                if (neighbors < 3) atlas[index] = Palette[3];
            }
    }

    private static void SoftenCaveRim(Color[] atlas, int width, int size)
    {
        var top = Enumerable.Repeat(size, size).ToArray();
        for (var y = 0; y < size; y++)
        {
            var left = size; var right = -1;
            for (var x = 0; x < size; x++)
                if (atlas[(y + size * 2) * width + x].A != 0)
                {
                    left = Math.Min(left, x); right = x;
                    top[x] = Math.Min(top[x], y);
                }
            for (var x = left; x <= right; x++)
            {
                // Only the external two-pixel rim joins the mountain. The inner
                // arch, tunnel and bottom threshold retain their authored shadows.
                if (y >= size - 6 || x - left >= 2 && right - x >= 2 && y - top[x] >= 2) continue;
                var index = (y + size * 2) * width + x;
                if (atlas[index] == Palette[0] || atlas[index] == Palette[1]) atlas[index] = Palette[2];
            }
        }
    }


    // Authored road exits, clockwise from north. Buildings retain Maynard's paved doorstep.
    internal static int PathConnections(ushort tile) => tile switch
    {
        8 => 5, 0x16 => 10, 0x17 => 6, 0x18 => 9, 0x19 => 3, 0x1a => 12,
        1 or 4 or 5 or 6 or 7 or 10 or 0x30 => 15,
        0x36 or 0x39 => 4,
        _ => 0,
    };

    internal static Texture2D CreatePaths(GraphicsDevice device, Texture2D terrain)
    {
        // Four stone orientations for each connection mask, compiled once at native resolution.
        const int width = 128;
        var source = new Color[terrain.Width * terrain.Height];
        terrain.GetData(source);
        var pixels = new Color[width * 512];
        for (var variant = 0; variant < 4; variant++)
            for (var connections = 1; connections < 16; connections++)
                for (var y = 0; y < 32; y++)
                    for (var x = 0; x < 32; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        var distance = PathDistance(p, connections);
                        // Periodic variation meets exactly at tile boundaries.
                        var radius = 6 + 0.6f * MathF.Sin(MathF.Tau * (p.X + 2 * p.Y) / 32)
                            + 0.35f * MathF.Cos(MathF.Tau * (2 * p.X - p.Y) / 32);
                        if (distance >= radius + 0.8f) continue;
                        var sx = (variant & 1) != 0 ? 31 - x : x;
                        var sy = (variant & 2) != 0 ? 31 - y : y;
                        var color = distance < radius ? source[sy * terrain.Width + 32 + sx] : Palette[17];
                        var frame = connections + variant * 16;
                        pixels[(frame / 4 * 32 + y) * width + frame % 4 * 32 + x] = color;
                    }
        var texture = new Texture2D(device, width, 512);
        texture.SetData(pixels);
        return texture;

        static float PathDistance(Vector2 p, int connections)
        {
            if (connections is 3 or 6 or 9 or 12)
            {
                // Reflect each bend into the north/east case: two short legs and a 10-pixel arc.
                if ((connections & 8) != 0) p.X = 32 - p.X;
                if ((connections & 4) != 0) p.Y = 32 - p.Y;
                var vertical = new Vector2(p.X - 16, MathF.Max(0, p.Y - 6)).Length();
                var horizontal = new Vector2(MathF.Min(0, p.X - 26), p.Y - 16).Length();
                var distance = MathF.Min(vertical, horizontal);
                if (p.X <= 26 && p.Y >= 6)
                    distance = MathF.Min(distance, MathF.Abs(Vector2.Distance(p, new(26, 6)) - 10));
                return distance;
            }
            var delta = p - new Vector2(16);
            var v = float.PositiveInfinity;
            var h = float.PositiveInfinity;
            if ((connections & 1) != 0) v = new Vector2(delta.X, MathF.Max(0, delta.Y)).Length();
            if ((connections & 4) != 0) v = MathF.Min(v, new Vector2(delta.X, MathF.Min(0, delta.Y)).Length());
            if ((connections & 2) != 0) h = new Vector2(MathF.Min(0, delta.X), delta.Y).Length();
            if ((connections & 8) != 0) h = MathF.Min(h, new Vector2(MathF.Max(0, delta.X), delta.Y).Length());
            // Round the inside corners where three or four roads meet.
            var blend = MathF.Max(0, 1 - MathF.Abs(v - h) / 4);
            return MathF.Min(v, h) - blend * blend;
        }
    }

    private static void AlignFrames(Color[] atlas, int width, int columns, int rows, int size, bool character, bool prop)
    {
        // Translation only: all poses keep the same pixel size, canvas and foot anchor.
        var frame = new Color[size * size];
        for (var index = 0; index < columns * rows; index++)
        {
            var cellX = index % columns * size;
            var cellY = index / columns * size;
            var left = size;
            var right = 0;
            var top = size;
            var bottom = 0;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var color = atlas[(cellY + y) * width + cellX + x];
                    frame[y * size + x] = color;
                    atlas[(cellY + y) * width + cellX + x] = Color.Transparent;
                    if (color.A == 0) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x + 1);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
                }
            if (right <= left || bottom <= top) throw new InvalidDataException("The sprite sheet contains an empty frame.");
            var centerX = (left + right) / 2;
            if (character)
            {
                // Helmet pixels give a stable body anchor even when a cape or foot swings sideways.
                var sum = 0;
                var count = 0;
                for (var y = top; y < top + (bottom - top) / 2; y++)
                    for (var x = left; x < right; x++)
                    {
                        var color = frame[y * size + x];
                        if (color.A == 0 || color.R < 100 || Math.Abs(color.R - color.G) > 25
                            || Math.Abs(color.G - color.B) > 25) continue;
                        sum += x; count++;
                    }
                if (count != 0) centerX = (int)MathF.Round(sum / (float)count);
            }
            var dx = size / 2 - centerX;
            var dy = character || prop ? 28 - bottom : index == 7 ? size / 2 - (top + bottom) / 2 : 44 - bottom;
            for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                {
                    var color = frame[y * size + x];
                    if (color.A == 0) continue;
                    if (x + dx < 0 || x + dx >= size || y + dy < 0 || y + dy >= size)
                        throw new InvalidDataException($"Sprite {index + 1} extends beyond its fixed frame.");
                    atlas[(cellY + y + dy) * width + cellX + x + dx] = color;
                }
        }
    }

}
