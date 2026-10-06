using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Yipe.Core;

internal static class MapChecks
{
    public static int Run(Action<bool, string> check, string? tracePath)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var catalog = MapCatalog.Load(source);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var traceFile = tracePath is null ? null : new StreamWriter(tracePath, false, new UTF8Encoding(false));
        var records = 0;
        void W(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        void Verify(string section, int count, string expected)
        {
            var text = writer.ToString();
            traceFile?.Write(text);
            traceFile?.Flush();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            check(text.Count(c => c == '\n') == count && hash == expected,
                $"C++ map {section}: {count} records (actual SHA-256 {hash})");
            records += count;
            writer.GetStringBuilder().Clear();
        }
        MapPosition[] directions = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];
        void Move(Map map, MapPosition from, MapPosition delta, int mask)
        {
            var result = map.Move(from, delta.X, delta.Y, new((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0));
            W($"|{result.OriginalCode},{result.Position.X},{result.Position.Y},{(result.Moved ? 1 : 0)}\n");
        }
        void Location(WorldLocation l) => W($",{l.Position.X},{l.Position.Y},{l.RegionX},{l.RegionY},{l.MapLayer},{(l.AlternateMapSet ? 1 : 0)},{l.MapId}");
        void PlayerLocation(Player p) => W($",{p.PendingRelocation},{p.CurrentMapId},{(p.AlternateMapSet ? 1 : 0)},{(p.EnteredHellThroughSecretPassage ? 1 : 0)},{(int)p.HellGatekeeperState},{p.SurfaceReturnMapId},{p.SurfaceReturnRegionX},{p.SurfaceReturnRegionY}");

        for (var id = -1; id <= 134; id++)
        {
            W($"catalog|{id}");
            var map = catalog.CreateMap(id);
            if (map is null) writer.Write("|missing\n"); else State(writer, map);
        }
        Verify("catalog", 136, "9776dedcda71b14f0d97d3fcd6f08f9894f29acd8c1b73a2f3cdaa9554ccaab0");
        for (var id = -1; id <= 134; id++)
        {
            W($"chests|{id}");
            foreach (var p in catalog.OriginalChestsForMap(id)) Point(writer, p);
            writer.Write('\n');
        }
        Verify("chests", 136, "f91d9b6038a589195f00c7712fcf225354e9d0638d0445dea684cab43e6a4dd0");
        foreach (var id in catalog.Ids.Order())
        {
            var map = catalog.CreateMap(id)!;
            foreach (var mask in new[] { 0, 15 })
                for (var x = 0; x < 13; x++)
                    for (var y = 0; y < 12; y++)
                        foreach (var delta in directions)
                        {
                            W($"moves|{id},{mask},{x},{y},{delta.X},{delta.Y}");
                            Move(map, new(x, y), delta, mask);
                        }
        }
        Verify("moves", 279552, "540f06a4d075debee7e9aed8baf1406ea378d22f0be7c1ef780fe6b8ffcdaa06");
        for (var raw = 0; raw <= 128; raw++)
            for (var mask = 0; mask < 16; mask++)
                for (var variant = 0; variant < 6; variant++)
                {
                    var tile = raw == 128 ? 65535 : raw;
                    var tiles = Enumerable.Repeat((ushort)2, Map.TileCount).ToArray();
                    var target = variant is 1 or 3 ? new MapPosition(0, 5) : new(6, 5);
                    var from = new MapPosition(target.X == 0 ? 1 : 5, 5);
                    var delta = new MapPosition(target.X == 0 ? -1 : 1, 0);
                    tiles[target.X * 12 + target.Y] = (ushort)tile;
                    var events = new MapEvents();
                    if (variant == 2) events.Depth[0] = new(target, 0x3a);
                    if (variant is 3 or 4) events.Depth[0] = new(target, 0x3b);
                    if (variant is 4 or 5) events.Overlay = new(target, variant == 4 ? 1 : 0);
                    W($"tiles|{tile},{mask},{variant}");
                    Move(new(0, 0, tiles, events), from, delta, mask);
                }
        Verify("tiles", 12384, "d7a153b1a86c1ed72a9ecf9e35136ae5f2c64811923f2ea9cd63c9f419f0297f");
        foreach (var id in catalog.Ids.Order())
        {
            var map = catalog.CreateMap(id)!;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var toggled = map.ToggleLinkedTiles();
                W($"mutations|{id},toggle,{attempt},{(toggled ? 1 : 0)}");
                State(writer, map);
            }
            map.OpenDoor(new(5, 5), new(6, 5));
            W($"mutations|{id},door"); State(writer, map);
            for (var slot = 0; slot < 4; slot++)
            {
                map.RemoveChest(slot);
                W($"mutations|{id},chest,{slot}"); State(writer, map);
            }
        }
        Verify("mutations", 784, "5f3f15786c4dce411e8a53e1d460cee9f0b2feb247be650ecce08b6ad4b65f91");
        foreach (var id in catalog.Ids.Order())
            foreach (var p in new MapPosition[] { new(5, 5), new(6, 5), new(2, 2), new(-1, -1), new(13, 12) })
                for (var op = 0; op < 2; op++)
                {
                    var map = catalog.CreateMap(id)!;
                    if (op == 0) map.RelocateOverlayAfterCombat(p); else map.RestoreOverlayForSavedPosition(p);
                    W($"overlays|{id},{p.X},{p.Y},{op}");
                    Event(writer, map.Events.Overlay); writer.Write('\n');
                }
        Verify("overlays", 1120, "cc091495516f4b8d3ff35439509c0c5c797f3a973d3c6c37a47a59bf9de5b902");
        MapPosition[] positions = [new(0, 0), new(12, 11), new(0, 5), new(12, 5), new(5, 0), new(5, 11), new(5, 5), new(0, 11), new(12, 0)];
        for (var rx = -4; rx <= 1; rx++)
            for (var ry = -3; ry <= 2; ry++)
                for (var layer = 0; layer < 3; layer++)
                    foreach (var alternate in new[] { false, true })
                        foreach (var p in positions)
                            foreach (var op in new[] { -3, -2, -1, 0, 1, 14, 15, 16, 17 })
                            {
                                var l = new WorldLocation { Position = p, RegionX = rx, RegionY = ry, MapLayer = layer, AlternateMapSet = alternate, MapId = 999 };
                                var changed = true;
                                if (op == -3) changed = l.CrossMapEdge();
                                else if (op == -2) l.AscendToSurface();
                                else if (op == -1) l.ReturnToSurface();
                                else changed = l.ChangeMapDepth(op);
                                W($"world|{rx},{ry},{layer},{(alternate ? 1 : 0)},{p.X},{p.Y},{op}|{(changed ? 1 : 0)}");
                                Location(l); writer.Write('\n');
                            }
        Verify("world", 17496, "22942b9133979b44d780d173ff97b21550d406fd713632374492ae566c2a58c7");
        foreach (var code in new[] { 0, 1, 2, 3, 4, 5, 65535 })
            foreach (var gate in new[] { -2, -1, 0, 1 })
                foreach (var returnMap in new[] { -1, 1, 17, 133 })
                {
                    var p = new Player
                    {
                        PendingRelocation = (ushort)code,
                        HellGatekeeperState = (HellGatekeeperState)gate,
                        SurfaceReturnMapId = (short)returnMap,
                        SurfaceReturnRegionX = -4,
                        SurfaceReturnRegionY = 2,
                        CurrentMapId = 77,
                        AlternateMapSet = true,
                    };
                    var l = new WorldLocation { Position = new(8, 9), RegionX = 1, RegionY = -2, MapLayer = 2, AlternateMapSet = true, MapId = 88 };
                    var result = -1;
                    try { result = (int)l.ApplyPendingRelocation(p); }
                    catch (InvalidOperationException) { }
                    W($"relocations|{code},{gate},{returnMap}|{result}");
                    Location(l); PlayerLocation(p); writer.Write('\n');
                }
        Verify("relocations", 112, "32212015dc028ebd2ca997b5ea0445764200eb03e9a833f74cd2bb0c076c51e5");
        foreach (var gate in new[] { -2, -1, 0, 1 })
            foreach (var secret in new[] { false, true })
            {
                var p = new Player { HellGatekeeperState = (HellGatekeeperState)gate };
                var l = new WorldLocation();
                l.EnterHell(p, secret);
                W($"hell|{gate},{(secret ? 1 : 0)}|");
                Location(l); PlayerLocation(p); writer.Write('\n');
            }
        Verify("hell", 8, "57bde0f55777de72c3942f3722aa387464b557a94b9f30dfe93604b1c41b00b5");

        CheckBoundaries(check, catalog, json);
        return records;
    }

    private static void CheckBoundaries(Action<bool, string> check, MapCatalog catalog, string json)
    {
        check(catalog.Ids.Count == 112 && catalog.CreateMap(0x76) is null && catalog.CreateMap(0x77) is null
            && catalog.CreateMap(0x78) is null && catalog.OriginalChestsForMap(0x78).Count(Map.Contains) == 2,
            "Static maps and generated-map chest records retain their distinct identities");
        var first = catalog.CreateMap(43)!;
        var sibling = catalog.CreateMap(43)!;
        first.RestoreTile(new(5, 5), 65535);
        first.Events.Depth[0] = MapEvent.None;
        first.Events.Linked[0] = MapEvent.None;
        first.Events.Overlay = MapEvent.None;
        first.RemoveChest(1);
        var reloaded = catalog.CreateMap(43)!;
        check(sibling.Tile(new(5, 5)) == reloaded.Tile(new(5, 5)) && sibling.Tile(new(5, 5)) != 65535
            && sibling.Events.Depth[0] == reloaded.Events.Depth[0] && sibling.Events.Depth[0].HasPosition
            && sibling.Events.Linked[0] == reloaded.Events.Linked[0] && sibling.Events.Linked[0].HasPosition
            && sibling.Events.Overlay == reloaded.Events.Overlay && sibling.Events.Overlay.HasPosition
            && sibling.Events.Chests[1] == reloaded.Events.Chests[1] && Map.Contains(sibling.Events.Chests[1]),
            "Map mutations cannot leak into another instance or the catalog template");
        var chestSlots = catalog.OriginalChestsForMap(120);
        chestSlots[2] = new(-1, -1);
        check(Map.Contains(catalog.OriginalChestsForMap(120)[2]), "Chest lookup returns independent stable slots");
        var tiles = new ushort[Map.TileCount];
        tiles[6 * Map.Height + 6] = 2;
        var events = new MapEvents();
        events.Depth[1] = new(new(1, 1), 59);
        var corner = new Map(0, 0, tiles, events);
        tiles[6 * Map.Height + 6] = 0;
        events.Depth[1] = MapEvent.None;
        check(corner.Move(new(5, 5), 1, 1) == new MovementResult(1, new(6, 6), true)
            && corner.Tile(new(6, 5)) == 0 && corner.Tile(new(5, 6)) == 0,
            "Original diagonals test the destination, even when both adjacent cardinal cells block movement");
        check(corner.DepthActionAt(new(1, 1)) == 16, "Map constructor copies caller-owned tiles and event arrays");
        var defaults = new WorldLocation();
        check(defaults.Position == new MapPosition(5, 5) && defaults.MapId == 17
            && defaults.RegionX == 0 && defaults.RegionY == 0 && defaults.MapLayer == 0 && !defaults.AlternateMapSet,
            "World defaults reproduce the original starting location");

        void Reject(Action action, string label)
        {
            var rejected = false;
            try { action(); }
            catch (Exception e) when (e is ArgumentException or InvalidDataException) { rejected = true; }
            check(rejected, label);
        }
        foreach (var delta in new MapPosition[] { new(0, 0), new(2, 0), new(0, -2), new(int.MinValue, 1), new(1, int.MaxValue) })
            Reject(() => corner.Move(new(5, 5), delta.X, delta.Y), "Core rejects non-adjacent movement commands");
        foreach (var p in new MapPosition[] { new(-1, 1), new(13, 1), new(1, 12), new(int.MaxValue, 1) })
        {
            Reject(() => corner.Move(p, 1, 0), "Core rejects invalid movement origins");
            Reject(() => corner.Tile(p), "Map lookup rejects out-of-grid coordinates");
            Reject(() => corner.RestoreTile(p, 2), "Tile mutation rejects out-of-grid coordinates");
        }
        Reject(() => corner.RemoveChest(-1), "Negative chest slots are rejected");
        Reject(() => corner.RemoveChest(4), "Chest slot numbering stays within the original four entries");
        Reject(() => new Map(1, 0, new ushort[155]), "A map cannot have a truncated tile buffer");
        void BadCatalog(Action<JsonNode> mutate, string label)
        {
            var node = JsonNode.Parse(json)!;
            mutate(node);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
            Reject(() => MapCatalog.Load(stream), label);
        }
        BadCatalog(n => n["format"] = "unknown", "Unsupported catalog versions are rejected");
        BadCatalog(n => n["dimensions"]!["storage"] = "row-major", "Transposed storage declarations are rejected");
        BadCatalog(n => n["dimensions"]!["height"] = 13, "Incorrect map dimensions are rejected");
        BadCatalog(n => n["maps"]![1]!["id"] = 1, "Duplicate map identities are rejected");
        BadCatalog(n => n["maps"]![0]!["id"] = 120, "Generated IDs cannot masquerade as static maps");
        BadCatalog(n => n["maps"]![0]!["tiles_by_x"]!.AsArray().RemoveAt(0), "Missing map columns are rejected");
        BadCatalog(n => n["maps"]![0]!["tiles_by_x"]![0]!.AsArray().RemoveAt(0), "Truncated map columns are rejected");
        BadCatalog(n => n["maps"]![0]!["tiles_by_x"]![0]![0] = -1, "Negative tile data cannot wrap into ushort");
        BadCatalog(n => n["maps"]![0]!["tiles_by_x"]![0]![0] = 65536, "Oversized tile data cannot wrap into ushort");
        BadCatalog(n => n["maps"]![0]!["events"]!["message"] = JsonNode.Parse("{\"x\":13,\"y\":1,\"value\":1}"), "Off-grid event coordinates are rejected");
        BadCatalog(n => n["maps"]![0]!["events"]!["linked"]!.AsArray().RemoveAt(0), "Missing linked-event slots are rejected");
        BadCatalog(n => n["maps"]![0]!["events"]!["princess"] = JsonNode.Parse("{\"x\":1,\"y\":1,\"value\":1,\"rescue_index\":30}"), "Invalid rescue indices are rejected");
        BadCatalog(n => n["chests"]![1]!["map_id"] = 1, "Duplicate chest table identities are rejected");
        BadCatalog(n => n["chests"]![0]!["slots"]!.AsArray().RemoveAt(0), "Missing chest slots cannot shift saved identities");
        BadCatalog(n => n["maps"]![0]!.AsObject().Remove("events"), "Missing event records are rejected");
        using var truncated = new MemoryStream(Encoding.UTF8.GetBytes(json[..100]));
        Reject(() => MapCatalog.Load(truncated), "Truncated JSON is rejected");
    }

    private static void Point(TextWriter writer, MapPosition p)
    {
        writer.Write(','); writer.Write(p.X); writer.Write(','); writer.Write(p.Y);
    }
    private static void Event(TextWriter writer, MapEvent e)
    {
        Point(writer, e.Position); writer.Write(','); writer.Write(e.Value);
    }
    private static void State(TextWriter writer, Map map)
    {
        writer.Write('|'); writer.Write(map.Id);
        void V(int value) { writer.Write(','); writer.Write(value); }
        V(map.Environment);
        for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) V(map.Tile(new(x, y)));
        var e = map.Events;
        Event(writer, e.Message); Event(writer, e.Overlay);
        foreach (var v in e.Depth) Event(writer, v);
        foreach (var v in e.Linked) Event(writer, v);
        Point(writer, e.Princess.Position); V(e.Princess.PortraitVariant); V(e.Princess.RescueIndex);
        Point(writer, e.Pursuer.Position); V(e.Pursuer.TypeId); V(e.Pursuer.UpgradedTypeId); V(e.Pursuer.UpgradeLevel);
        foreach (var level in new[] { 0, 1, 5, 6, 12, 30, 100 }) V(e.Pursuer.TypeForLevel(level));
        foreach (var p in e.Chests) Point(writer, p);
        Point(writer, map.LinkedDestination ?? new(-1, -1));
        Point(writer, map.SurfaceReturnPosition() ?? new(-1, -1));
        writer.Write('\n');
    }
}
