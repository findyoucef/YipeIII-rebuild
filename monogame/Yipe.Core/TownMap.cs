namespace Yipe.Core;

/// <summary>Maynard preview adapter; map loading and all movement rules belong to the shared world model.</summary>
public sealed class TownMap
{
    public const int Id = MapCatalog.StartingMapId;
    public const int Width = Map.Width;
    public const int Height = Map.Height;
    private readonly Map map;

    public TownMap(Map map)
    {
        if (map.Id != Id) throw new ArgumentException("The town renderer requires Maynard.", nameof(map));
        this.map = map;
    }

    public static TownMap Load(Stream source) => new(MapCatalog.Load(source).CreateMap(Id)
        ?? throw new InvalidDataException("The original map catalog does not contain Maynard."));

    public static bool Contains(MapPosition position) => Map.Contains(position);
    public ushort Tile(MapPosition position) => map.Tile(position);
    public MovementResult Move(MapPosition from, int dx, int dy) => map.Move(from, dx, dy);

    // Preview saves remain limited to safe interior town cells, independent of future world saves.
    public bool CanResume(MapPosition position) => position.X > 0 && position.X < Width - 1
        && position.Y > 0 && position.Y < Height - 1
        && Tile(position) is 2 or 8 or 0x0b or 0x16 or 0x19 or 0x1a or 0x32;
}
