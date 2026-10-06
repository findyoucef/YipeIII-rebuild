using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Yipe.Core;

namespace Yipe.Desktop;

public enum MapMark : byte { None, Shop, Entrance, Stairs, Bridge }

public sealed class MappedRegion
{
    public int Id { get; set; }
    public int Environment { get; set; }
    public bool[] Seen { get; set; } = new bool[Map.TileCount];
    public ushort[] Tiles { get; set; } = new ushort[Map.TileCount];
    public MapMark[] Marks { get; set; } = new MapMark[Map.TileCount];
    [JsonIgnore] public int Layer => Id >= 117 ? 3 : (Id - 1) / 40;
    [JsonIgnore] public int Cell => Id - (Layer == 3 ? 98 : Layer * 40) - 1;
    [JsonIgnore] public string Coordinate => $"{(char)('A' + Cell % 6)}{Cell / 6 + 1}";
    [JsonIgnore] public string Name => Id switch
    {
        17 => "Maynard", 8 => "Quizilog", 27 => "Ervyn", 117 => "Yipe Hell",
        _ => (MapCatalog.IsGenerated(Id) ? "Shifting cave" : Layer == 0 ? "Surface" : Environment switch
        { 1 => "Earth Cave", 2 => "Stone Cave", 3 => "Fire Cave", 4 => "Zephyr Cave", 5 => "Water Cave", 6 => "Final Cave", _ => "Cave" }) + " " + Coordinate,
    };
}

public readonly record struct MapLink(int From, int To);

/// <summary>Presentation-only discoveries. It observes play without loading maps or consuming RNG.</summary>
public sealed class MapJournal
{
    public int Version { get; set; } = 1;
    public string SaveHash { get; set; } = "";
    public List<MappedRegion> Regions { get; set; } = new();
    public List<MapLink> Links { get; set; } = new();
    private Map? observedMap;
    private GameSession? observedSession;

    public MappedRegion? Find(int id) => Regions.Find(r => r.Id == id);

    public void Observe(Adventure game)
    {
        var map = game.Map;
        if (!ReferenceEquals(observedSession, game.Session)) { observedSession = game.Session; observedMap = null; }
        if (observedMap is { } previous && previous.Id != map.Id)
        {
            var link = new MapLink(Math.Min(previous.Id, map.Id), Math.Max(previous.Id, map.Id));
            if (!Links.Contains(link)) Links.Add(link);
        }
        var region = Find(map.Id);
        if (region is null) { region = new() { Id = map.Id, Environment = map.Environment }; Regions.Add(region); }
        // Generated caves regenerate on entry/reload: never project an old layout into the new room.
        if (!ReferenceEquals(observedMap, map) && MapCatalog.IsGenerated(map.Id))
        { Array.Clear(region.Seen); Array.Clear(region.Tiles); Array.Clear(region.Marks); }
        observedMap = map;
        for (var x = 0; x < Map.Width; x++)
            for (var y = 0; y < Map.Height; y++)
            {
                var position = new MapPosition(x, y);
                if (!game.Visibility.Contains(position)) continue;
                var i = x * Map.Height + y;
                var tile = map.Tile(position);
                region.Seen[i] = true; region.Tiles[i] = tile;
                region.Marks[i] = tile switch
                {
                    1 or 4 or 5 or 6 or 7 or 10 or 0x30 => MapMark.Shop,
                    0x36 or 0x39 => MapMark.Entrance, 11 => MapMark.Bridge,
                    _ => MapMark.None,
                };
                if (map.Events.Depth.Any(d => d.HasPosition && d.Position == position)) region.Marks[i] = MapMark.Stairs;
            }
    }

    public byte[] Encode(byte[] save)
    {
        SaveHash = Convert.ToHexString(SHA256.HashData(save));
        return JsonSerializer.SerializeToUtf8Bytes(this, MapJournalJsonContext.Default.MapJournal);
    }

    public static MapJournal Decode(byte[] bytes, byte[] save, MapCatalog catalog)
    {
        if (bytes.Length > 512 * 1024) throw new InvalidDataException("The map journal is too large.");
        MapJournal? journal;
        try { journal = JsonSerializer.Deserialize(bytes, MapJournalJsonContext.Default.MapJournal); }
        catch (JsonException error) { throw new InvalidDataException("The map journal is invalid.", error); }
        if (journal is null || journal.Version != 1 || journal.Regions is null || journal.Links is null
            || journal.Regions.Count > 115 || journal.Links.Count > 1024
            || journal.SaveHash != Convert.ToHexString(SHA256.HashData(save)))
            throw new InvalidDataException("The map journal does not match this saved adventure.");
        var ids = new HashSet<int>();
        foreach (var region in journal.Regions)
        {
            if (region is null || !catalog.Contains(region.Id) || !ids.Add(region.Id) || region.Environment is < 0 or > 7
                || region.Seen is null || region.Seen.Length != Map.TileCount
                || region.Tiles is null || region.Tiles.Length != Map.TileCount
                || region.Marks is null || region.Marks.Length != Map.TileCount
                || region.Marks.Any(m => m is < MapMark.None or > MapMark.Bridge))
                throw new InvalidDataException("The map journal contains an invalid region.");
            for (var i = 0; i < Map.TileCount; i++)
            {
                if (!region.Seen[i]) { region.Tiles[i] = 0; region.Marks[i] = MapMark.None; }
                else if (region.Tiles[i] > 0x71) throw new InvalidDataException("The map journal contains an invalid tile.");
            }
        }
        if (journal.Links.Any(l => l.From >= l.To || !ids.Contains(l.From) || !ids.Contains(l.To))
            || journal.Links.Distinct().Count() != journal.Links.Count)
            throw new InvalidDataException("The map journal contains an invalid connection.");
        return journal;
    }
}

[JsonSerializable(typeof(MapJournal))]
internal partial class MapJournalJsonContext : JsonSerializerContext;
