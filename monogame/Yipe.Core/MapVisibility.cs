namespace Yipe.Core;

/// <summary>Map-instance visibility; never persisted. Reopened rooms reveal from the saved tile.</summary>
public sealed class MapVisibility
{
    private readonly bool[] visible = new bool[Map.TileCount];
    private readonly bool[] expanded = new bool[Map.TileCount];
    public int MapId { get; private set; } = -1;
    public bool Contains(MapPosition position) => Map.Contains(position) && visible[position.X * Map.Height + position.Y];
    public bool IsExpanded(MapPosition position) => Map.Contains(position) && expanded[position.X * Map.Height + position.Y];

    public void Initialize(Map map, MapPosition position)
    {
        MapId = map.Id;
        var fullyVisible = map.Id < 0x29;
        Array.Fill(visible, fullyVisible); Array.Fill(expanded, fullyVisible);
        if (!fullyVisible) RevealArea(map, position);
    }
    public void RevealFrom(Map map, MapPosition position)
    {
        if (MapId != map.Id) Initialize(map, position);
        else RevealArea(map, position);
    }
    private void RevealArea(Map map, MapPosition center)
    {
        for (var x = center.X - 1; x <= center.X + 1; x++)
            for (var y = center.Y - 1; y <= center.Y + 1; y++)
                if (Map.Contains(new(x, y))) visible[x * Map.Height + y] = true;
        for (var x = center.X - 1; x <= center.X + 1; x++)
            for (var y = center.Y - 1; y <= center.Y + 1; y++)
            {
                var position = new MapPosition(x, y);
                if (!Map.Contains(position)) continue;
                var i = x * Map.Height + y;
                if (expanded[i]) continue;
                expanded[i] = true;
                if (map.Tile(position) is not (0x22 or 0x23 or 0x24 or 0x25 or 0x3e or 0x3f or 0x40 or 0x43 or 0x44 or 0x5a) && visible[i])
                    RevealArea(map, position);
            }
    }
}
