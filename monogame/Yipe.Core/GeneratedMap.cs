namespace Yipe.Core;

/// <summary>The original room/corridor generator, including its in-place smoothing order.</summary>
internal static class GeneratedMap
{
    public static Map Create(int id, MapPosition entry, MsvcRandom random, MapPosition[] chests)
    {
        if (entry.X is < 1 or > 11 || entry.Y is < 1 or > 10)
            throw new InvalidDataException("Generated maps require an interior entry position.");
        var tiles = new ushort[Map.TileCount];
        Array.Fill(tiles, (ushort)0x24);
        var rooms = new (int MinX, int MinY, int MaxX, int MaxY)[8];
        rooms[0] = (10, 1, 12, 2);
        var count = random.Next() % 3 + 4;
        var penalty = count / 2;
        for (var i = 1; i < count; i++)
        {
            var height = random.Next() % (6 - penalty) + 1;
            var width = random.Next() % (5 - penalty) + 1;
            var y = Math.Min(random.Next() % (10 - height) + 3, 9);
            var x = random.Next() % (11 - width) + 1;
            var room = rooms[i] = (x, y, x + width, Math.Min(y + height, 10));
            for (x = room.MinX; x < room.MaxX; x++)
                for (y = room.MinY; y < room.MaxY; y++) Set(x, y, 0x27);
        }
        rooms[count++] = (1, 1, 2, 2);
        Set(11, 1, 0x27); Set(1, 1, 0x27); Set(1, 2, 0x27); Set(2, 1, 0x27); Set(2, 2, 0x27);
        if (entry != new MapPosition(1, 1))
        {
            rooms[count++] = (entry.X, entry.Y, entry.X, entry.Y);
            Set(entry.X, entry.Y, 0x27);
        }
        for (var i = 0; i < count - 1; i++)
        {
            var a = rooms[i]; var b = rooms[i + 1];
            var fromX = Between(a.MinX, a.MaxX); var fromY = Between(a.MinY, a.MaxY);
            var toX = Between(b.MinX, b.MaxX); var toY = Between(b.MinY, b.MaxY);
            var stepX = fromX <= toX ? 1 : -1;
            for (var x = fromX; ; x += stepX) { Set(x, fromY, 0x27); if (x == toX) break; }
            var stepY = fromY <= toY ? 1 : -1;
            for (var y = fromY; ; y += stepY) { Set(toX, y, 0x27); if (y == toY) break; }
        }
        Set(0, 1, 0x27); Set(0, 2, 0x24); Set(11, 2, 0x24);
        Set(12, 0, 0x24); Set(12, 1, 0x27); Set(12, 2, 0x24);
        for (var x = 1; x <= 10; x++)
            for (var offset = 0; offset < 11; offset++)
            {
                // Preserve the original linear look-ahead at the bottom of each column.
                var i = x * Map.Height + 1 + offset;
                var nextX = tiles[i + Map.Height]; var diagonal = tiles[i + Map.Height + 1];
                if (tiles[i] == 0x24 && nextX == 0x27 && tiles[i + 1] == 0x27 && diagonal == 0x24)
                    tiles[i] = 0x27;
                if (tiles[i] is 0x27 or 0x28 && nextX == 0x24 && tiles[i + 1] == 0x24 && diagonal == 0x27)
                    tiles[i + 1] = 0x27;
            }
        var events = new MapEvents();
        chests.CopyTo(events.Chests, 0);
        return new(id, 7, tiles, events);

        void Set(int x, int y, ushort tile) => tiles[x * Map.Height + y] = tile;
        // A degenerate range still draws: changing this would change all later gameplay RNG.
        int Between(int minimum, int maximum) => minimum + random.Next() % (maximum - minimum + 1);
    }
}
