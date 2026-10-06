using System.Text.Json;

namespace Yipe.Core;

/// <summary>Original static map templates. Every CreateMap call returns independent mutable state.</summary>
public sealed class MapCatalog
{
    public const int StartingMapId = 0x11;
    private readonly Dictionary<int, Map> maps = new();
    private readonly Dictionary<int, MapPosition[]> chests = new();
    public IReadOnlyCollection<int> Ids => maps.Keys;

    private MapCatalog() { }

    public Map? CreateMap(int id) => maps.TryGetValue(id, out var map) ? map.Copy() : null;

    public bool Contains(int id) => maps.ContainsKey(id) || IsGenerated(id);
    public static bool IsGenerated(int id) => id is >= 118 and <= 120;
    public Map? LoadMap(int id, MapPosition entry, MsvcRandom random) => IsGenerated(id)
        ? GeneratedMap.Create(id, entry, random, OriginalChestsForMap(id)) : CreateMap(id);

    // The original chest table includes generated map 0x78, which has no static tile template.
    public MapPosition[] OriginalChestsForMap(int id) => chests.TryGetValue(id, out var slots)
        ? (MapPosition[])slots.Clone() : [new(-1, -1), new(-1, -1), new(-1, -1), new(-1, -1)];

    public static MapCatalog Load(Stream source)
    {
        try
        {
            using var document = JsonDocument.Parse(source);
            var root = document.RootElement;
            var dimensions = root.GetProperty("dimensions");
            if (root.GetProperty("format").GetString() != "yipe3-original-map-tables-v5"
                || dimensions.GetProperty("width").GetInt32() != Map.Width
                || dimensions.GetProperty("height").GetInt32() != Map.Height
                || dimensions.GetProperty("storage").GetString() != "column-major")
                throw new InvalidDataException("Unsupported original map format, dimensions or storage order.");

            var catalog = new MapCatalog();
            foreach (var entry in root.GetProperty("chests").EnumerateArray())
            {
                var id = entry.GetProperty("map_id").GetInt32();
                var slots = entry.GetProperty("slots");
                if (id < 1 || slots.GetArrayLength() != 4)
                    throw new InvalidDataException("Chest records require a positive map ID and four stable slots.");
                var positions = slots.EnumerateArray().Select(ReadPosition).ToArray();
                if (!catalog.chests.TryAdd(id, positions)) throw new InvalidDataException($"Duplicate chest map ID {id}.");
            }

            foreach (var entry in root.GetProperty("maps").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetInt32();
                var environment = entry.GetProperty("environment").GetInt32();
                if (id < 1 || id is 0x76 or 0x77 or 0x78 || environment is < 0 or > 7)
                    throw new InvalidDataException("Invalid static map ID or environment.");
                var columns = entry.GetProperty("tiles_by_x");
                if (columns.GetArrayLength() != Map.Width) throw new InvalidDataException("A map requires 13 columns.");
                var tiles = new ushort[Map.TileCount];
                for (var x = 0; x < Map.Width; x++)
                {
                    if (columns[x].GetArrayLength() != Map.Height)
                        throw new InvalidDataException("Each map column requires 12 tiles.");
                    for (var y = 0; y < Map.Height; y++) tiles[x * Map.Height + y] = columns[x][y].GetUInt16();
                }
                var data = entry.GetProperty("events");
                var events = new MapEvents
                {
                    Message = ReadEvent(data.GetProperty("message")),
                    Overlay = ReadEvent(data.GetProperty("overlay")),
                };
                var depth = data.GetProperty("depth");
                var linked = data.GetProperty("linked");
                if (depth.GetArrayLength() > 2 || linked.GetArrayLength() != 2)
                    throw new InvalidDataException("Maps require at most two depth events and two linked slots.");
                for (var i = 0; i < depth.GetArrayLength(); i++) events.Depth[i] = ReadEvent(depth[i]);
                for (var i = 0; i < 2; i++) events.Linked[i] = ReadEvent(linked[i]);
                var princess = data.GetProperty("princess");
                if (princess.ValueKind != JsonValueKind.Null)
                {
                    var index = princess.GetProperty("rescue_index").GetInt32();
                    var portrait = princess.GetProperty("value").GetInt32();
                    if (index < 0 || index >= Player.InventorySize || portrait < 0)
                        throw new InvalidDataException("Invalid princess rescue slot or portrait.");
                    events.Princess = new(ReadPosition(princess), portrait, index);
                }
                var pursuer = data.GetProperty("pursuer");
                if (pursuer.ValueKind != JsonValueKind.Null)
                {
                    var type = pursuer.GetProperty("value").GetInt32();
                    var upgraded = pursuer.TryGetProperty("upgraded_value", out var upgrade) ? upgrade.GetInt32() : 0;
                    var level = pursuer.TryGetProperty("upgrade_level", out var atLevel) ? atLevel.GetInt32() : 0;
                    if (type <= 0 || upgraded < 0 || level < 0 || ((upgraded > 0) != (level > 0)))
                        throw new InvalidDataException("Invalid pursuing monster record.");
                    events.Pursuer = new(ReadPosition(pursuer), type, upgraded, level);
                }
                catalog.OriginalChestsForMap(id).CopyTo(events.Chests, 0);
                if (!catalog.maps.TryAdd(id, new Map(id, environment, tiles, events)))
                    throw new InvalidDataException($"Duplicate static map ID {id}.");
            }
            if (catalog.maps.Count == 0) throw new InvalidDataException("The map catalog is empty.");
            return catalog;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("Malformed original map catalog.", error);
        }
    }

    private static MapPosition ReadPosition(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Null) return new(-1, -1);
        var position = new MapPosition(data.GetProperty("x").GetInt32(), data.GetProperty("y").GetInt32());
        if (position != new MapPosition(-1, -1) && !Map.Contains(position))
            throw new InvalidDataException("Event position is outside the map.");
        return position;
    }

    private static MapEvent ReadEvent(JsonElement data) => data.ValueKind == JsonValueKind.Null
        ? MapEvent.None : new(ReadPosition(data), data.GetProperty("value").GetInt32());
}
