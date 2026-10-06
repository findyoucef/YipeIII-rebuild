using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class SessionChecks
{
    public static int Run(Action<bool, string> check, string? tracePath)
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        var catalog = MapCatalog.Load(source);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var traceFile = tracePath is null ? null : new StreamWriter(tracePath, false, new UTF8Encoding(false));
        var records = 0;
        void W(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        void State(Map map)
        {
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) W($",{map.Tile(new(x, y))}");
            foreach (var p in map.Events.Chests) W($",{p.X},{p.Y}");
            writer.Write('\n');
        }
        void Verify(string section, int count, string expected)
        {
            var text = writer.ToString();
            traceFile?.Write(text);
            traceFile?.Flush();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            check(text.Count(c => c == '\n') == count && hash == expected,
                $"C++ session {section}: {count} records (actual SHA-256 {hash})");
            records += count;
            writer.GetStringBuilder().Clear();
        }

        foreach (var id in catalog.Ids.Order())
            for (var sample = 0; sample < 172; sample++)
            {
                var variant = sample < 156 ? 0 : sample - 156;
                var entry = sample < 156 ? new MapPosition(sample / 12, sample % 12) : new(5, 5);
                ushort[] flags = [0, 1, 4, 5, 32767, 32768, 65535];
                var player = new Player { Strength = variant % 2 == 0 ? 39 : 40,
                    GatekeeperState = flags[variant % 7], StampsFound = flags[variant % 7], HealingWaterAvailable = false };
                if (id <= 128) player.OpenedChests[id - 1] = (byte)variant;
                var session = new GameSession(catalog, player, new() { MapId = id, Position = entry });
                W($"entry|{id},{sample},{(player.HealingWaterAvailable ? 1 : 0)}");
                State(session.CurrentMap);
            }
        Verify("entry", 19264, "7193b87ebcf938edfedfc12f95affe6c17b8f5abd7b61365ce67e2c008d7da84");

        foreach (var id in catalog.Ids.Order())
            foreach (var mask in new[] { 0, 15 })
                for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
                {
                    var from = new MapPosition(x == 0 ? 1 : x - 1, y);
                    var dx = x == 0 ? -1 : 1;
                    var p = new Player { HasWaterproofShoeSpray = mask != 0, HasRainbowKey = mask != 0,
                        HasUnderworldKey = mask != 0, HasTreeClimbingShoes = mask != 0 };
                    var session = new GameSession(catalog, p, new() { MapId = id, Position = from }, uint.MaxValue);
                    if (session.CurrentMap.Move(from, dx, 0, new(mask != 0, mask != 0, mask != 0, mask != 0)).OriginalCode
                        is not (0x0c or 0x12 or 0x13 or 0x14 or 0x17 or 0x1a or 0x1b)) continue;
                    p.Health = 1; p.SpellPoints = 0; p.AbilityPoints = 0;
                    for (var step = 0; step < 3; step++)
                    {
                        session.Location.Position = from;
                        var result = session.Move(dx, 0);
                        W($"action|{id},{mask},{x},{y},{step},{result.OriginalCode},{result.Position.X},{result.Position.Y},{(result.PositionChanged ? 1 : 0)},{(result.Restored ? 1 : 0)},{result.Damage},{p.Health},{p.SpellPoints},{p.AbilityPoints},{p.RestorationUses},{session.Random.State}");
                        State(session.CurrentMap);
                    }
                }
        Verify("action", 2058, "16b202f935c504640710d5bcaeae99785e82bf46e1f1105d0fc08296ea199222");

        foreach (var difficulty in new[] { 0, 1, 2, 3, 7 }) for (var uses = 0; uses < 9; uses++)
        {
            var p = new Player { Difficulty = difficulty, RestorationUses = uses, Health = 1, SpellPoints = 0, AbilityPoints = 0 };
            var session = new GameSession(catalog, p);
            session.CurrentMap.RestoreTile(new(6, 5), 0x0c);
            var result = session.Move(1, 0);
            W($"restore|{difficulty},{uses},{(result.Restored ? 1 : 0)},{p.Health},{p.SpellPoints},{p.AbilityPoints},{p.RestorationUses}\n");
        }
        Verify("restore", 45, "2425d1b06827aa6c87cf93b1530cf4140da530a9a66c35a60c4f960e31ddfff7");
        foreach (var seed in new[] { 0u, 1u, uint.MaxValue }) foreach (var difficulty in new[] { 0, 1, 2, 3 })
            foreach (var level in new[] { 0, 1, 10, 100 }) foreach (var health in new[] { 1, 2, 13 })
            {
                var p = new Player { Difficulty = difficulty, Level = level, Health = health };
                var session = new GameSession(catalog, p, randomState: seed);
                session.CurrentMap.RestoreTile(new(6, 5), 0x24);
                var result = session.Move(1, 0);
                W($"fire|{seed},{difficulty},{level},{health},{result.Damage},{p.Health},{session.Random.State}\n");
            }
        Verify("fire", 144, "86c070629fc54c8fc60a3fcd89c7055b75f414f019fc58545c3fd179c82b31d2");

        var initial = new WorldLocation();
        var game = new GameSession(catalog, location: initial);
        check(game.Player.Health == 13 && game.Player.CurrentMapId == 17 && game.Random.State == 1
            && game.CurrentMap.Id == game.Location.MapId, "A new session owns synchronized character, world, map and RNG");
        initial.Position = new(1, 1);
        check(game.Location.Position == new MapPosition(5, 5), "The session takes its own location snapshot");
        var walk = game.Move(1, 0);
        check(walk.PositionChanged && walk.Position == new MapPosition(6, 5) && game.Location.Position == walk.Position
            && game.Random.State == 1, "Logical movement commits without animation or random draws");
        var shop = game.Move(0, 1);
        check(shop.OriginalCode == 8 && !shop.PositionChanged && game.Player.Gold == 0,
            "Shop commands hand off the original interaction code without changing position or resources");

        game.Location.Position = new(1, 2);
        game.Player.HealingWaterAvailable = false;
        var mapBefore = game.CurrentMap;
        var blocked = game.Move(0, -1, allowMapChanges: false);
        check(blocked.Transition == MapTransition.Disabled && !blocked.PositionChanged
            && game.Location.Position == new MapPosition(1, 2) && ReferenceEquals(game.CurrentMap, mapBefore)
            && !game.Player.HealingWaterAvailable && game.Player.SurfaceReturnMapId == 0,
            "Preview boundary does not enter a map, rearm water, or store a surface return");
        var cave = game.Move(0, -1);
        check(cave.Transition == MapTransition.Changed && cave.OriginalCode == 0x0e
            && game.Location.MapId == 57 && game.Location.MapLayer == 1 && game.CurrentMap.Id == 57
            && game.Player.CurrentMapId == 57 && game.Player.SurfaceReturnMapId == 17
            && game.Player.HealingWaterAvailable, "Cave entry loads and initializes the map and records the previous surface location");
        game.Player.Inventory[0] = 5;
        var potion = game.UseInventoryItem(0);
        check(potion.Consumed && game.Player.Inventory[0] == 0 && game.Player.PendingRelocation == 0
            && game.Location.Position == new MapPosition(1, 2) && game.CurrentMap.Id == 17
            && game.Player.CurrentMapId == 17 && game.Random.State == 1,
            "Surface potion uses session context, consumes the item and places the player below Maynard's cave");
        game.Move(0, -1);
        game.Location.Position = new(1, 2);
        var ascent = game.Move(0, -1);
        check(ascent.OriginalCode == 0x0f && ascent.Transition == MapTransition.Changed
            && game.Location.MapLayer == 0 && game.CurrentMap.Id == 17 && game.Player.CurrentMapId == 17,
            "The authored cave depth event returns through the session to the surface map");
        game.Location.Position = new(11, 5);
        var east = game.Move(1, 0);
        check(east.Transition == MapTransition.Changed && game.Location.RegionX == 1
            && game.Location.Position == new MapPosition(1, 5) && game.CurrentMap.Id == 18 && game.Player.CurrentMapId == 18,
            "An authored town edge enters and synchronizes the neighboring static map");
        game.CurrentMap.RestoreTile(new(0, 5), 2);
        game.Move(-1, 0);
        check(game.Location.RegionX == 0 && game.Location.Position == new MapPosition(11, 5) && game.CurrentMap.Id == 17,
            "Crossing back recreates the previous map at its corresponding edge");

        game.Player.PendingRelocation = (ushort)PendingRelocation.Hell;
        game.ApplyPendingRelocation();
        check(game.Location.MapId == 117 && game.Player.AlternateMapSet && game.Player.EnteredHellThroughSecretPassage
            && game.Player.HellGatekeeperState == HellGatekeeperState.Waiting && game.Location.Position == new MapPosition(6, 10),
            "Queued Hell relocation synchronizes map, entry, alternate set and gatekeeper state");
        game.Player.Inventory[0] = 5;
        check(!game.UseInventoryItem(0).Consumed && game.Player.Inventory[0] == 5 && game.Location.MapId == 117,
            "Exploration item checks use the current alternate map set");
        game.CurrentMap.RestoreTile(new(7, 10), 0x55);
        check(game.Move(1, 0).Transition == MapTransition.None && game.Location.MapId == 117,
            "The waiting Hell gatekeeper requires an interaction instead of granting a free exit");
        game.Player.HellGatekeeperState = HellGatekeeperState.Paid;
        check(game.Move(1, 0).Transition == MapTransition.Changed && game.Location.MapId == 17
            && game.Location.Position == new MapPosition(4, 5) && !game.Player.AlternateMapSet
            && game.Player.HellGatekeeperState == HellGatekeeperState.Absent,
            "Paid gatekeeper exit returns to Maynard and clears the gatekeeper as in C++");

        game = new(catalog, location: new() { MapId = 117, RegionX = -4, RegionY = -1,
            AlternateMapSet = true, Position = new(11, 5) });
        game.CurrentMap.RestoreTile(new(12, 5), 0x27);
        var generated = game.Move(1, 0);
        check(generated.Transition == MapTransition.Changed && generated.PositionChanged
            && game.Location.MapId == 118 && game.Location.RegionX == -3 && game.Location.Position == new MapPosition(1, 5)
            && game.Player.CurrentMapId == 118 && game.Random.State != 1,
            "Generated destinations load and consume the gameplay RNG");
        game.Player.PendingRelocation = (ushort)PendingRelocation.Surface;
        game.Player.SurfaceReturnMapId = 999;
        Throws<InvalidOperationException>(() => game.ApplyPendingRelocation(), "Unavailable relocation is explicit");
        check(game.Player.PendingRelocation == 1 && game.CurrentMap.Id == 118 && game.Player.AlternateMapSet,
            "Failed relocation remains queued without partial mutation");
        game.Player.PendingRelocation = (ushort)PendingRelocation.StoneCircle;
        game.ApplyPendingRelocation();
        check(game.CurrentMap.Id == 21 && game.Location.RegionX == -2 && game.Location.RegionY == -1
            && game.Location.Position == new MapPosition(6, 8) && !game.Player.AlternateMapSet,
            "Stone-circle relocation loads its static map with the original surface coordinates");
        game.Player.HealingWaterAvailable = false;
        check(game.ApplyPendingRelocation() == PendingRelocation.None && !game.Player.HealingWaterAvailable,
            "No queued relocation leaves map-instance state alone");
        game = new(catalog, new Player { PendingRelocation = (ushort)PendingRelocation.Yipe },
            new() { MapId = 117, AlternateMapSet = true });
        check(game.CurrentMap.Id == 17 && game.Location.Position == new MapPosition(4, 5) && game.Player.PendingRelocation == 0,
            "Opening a session resolves a saved pending relocation before normal map restoration");

        // Persistent edits must follow initialization, including quest tiles and chest filtering.
        game = new(catalog, new Player { Strength = 40 }, new() { MapId = 45 },
            tileChanges: [new(45, new(11, 9), 2)]);
        check(game.CurrentMap.Tile(new(11, 9)) == 2, "Saved tile edits take precedence over strength-gated entry changes");
        var chestMap = catalog.Ids.Select(id => catalog.CreateMap(id)!).First(m => m.Events.Chests.Any(p => Map.Contains(p) && m.Tile(p) != 0x24));
        var chest = chestMap.Events.Chests.First(p => Map.Contains(p) && chestMap.Tile(p) != 0x24);
        game = new(catalog, location: new() { MapId = chestMap.Id }, tileChanges: [new(chestMap.Id, chest, 0x24)]);
        check(game.CurrentMap.Events.Chests.Contains(chest), "Chest filtering precedes applying saved wall tiles");
        var overlayMap = catalog.Ids.Select(id => catalog.CreateMap(id)!).First(m => m.Events.Overlay.Value > 0);
        game = new(catalog, location: new() { MapId = overlayMap.Id }, tileChanges: [new(overlayMap.Id, new(5, 5), 0x0c)]);
        check(game.CurrentMap.Events.Overlay.Position == new MapPosition(5, 5),
            "Opening saved state restores D7 platform support after persistent tiles");

        game = new(catalog, tileChanges: [new(17, new(1, 1), 2), new(17, new(9, 8), 0x39)]);
        game.Player.SurfaceReturnMapId = 17;
        game.Player.PendingRelocation = (ushort)PendingRelocation.Surface;
        game.ApplyPendingRelocation();
        check(game.Location.Position == new MapPosition(1, 2), "Surface placement scans the original template before saved entrance changes");

        game = new(catalog);
        game.CurrentMap.RestoreTile(new(5, 5), 0x27);
        game.CurrentMap.RestoreTile(new(6, 5), 0x3e);
        game.Move(1, 0);
        check(game.CurrentMap.Tile(new(6, 5)) == 0x27 && game.TileChanges.SequenceEqual([new MapTileChange(17, new(6, 5), 0x27)]),
            "Opening a door copies the previous floor and remembers that tile");
        var reopened = new GameSession(catalog, game.Player, game.Location, game.Random.State, game.TileChanges);
        check(reopened.CurrentMap.Tile(new(6, 5)) == 0x27 && reopened.CurrentMap.Tile(new(5, 5)) == 2,
            "A fresh map restores remembered changes while unrecorded instance edits reset");
        game = new(catalog);
        game.CurrentMap.Events.Linked[0] = new(new(7, 5), 2);
        game.CurrentMap.Events.Linked[1] = new(new(8, 5), 0x27);
        game.CurrentMap.RestoreTile(new(7, 5), 2);
        game.CurrentMap.RestoreTile(new(6, 5), 0x5d);
        game.Move(1, 0);
        check(game.TileChanges.Count == 2 && game.CurrentMap.Tile(new(7, 5)) == 0x27 && game.CurrentMap.Tile(new(8, 5)) == 2,
            "A linked switch remembers both changed tiles");
        game.Location.Position = new(5, 5);
        game.Move(1, 0);
        check(game.TileChanges.Count == 2 && game.TileChanges[0].Tile == 2 && game.TileChanges[1].Tile == 0x27,
            "Repeated switch use replaces the existing persistence entries");
        reopened = new(catalog, game.Player, game.Location, game.Random.State, game.TileChanges);
        check(reopened.CurrentMap.Tile(new(7, 5)) == 2 && reopened.CurrentMap.Tile(new(8, 5)) == 0x27,
            "Both switch results survive map recreation");

        var zero = new Map(0, 0, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        var zeroPlayer = new Player { HealingWaterAvailable = false };
        zero.InitializeActions(zeroPlayer, new(5, 5));
        check(!zeroPlayer.HealingWaterAvailable, "Map zero retains the original water exception");
        Throws<ArgumentOutOfRangeException>(() => zero.InitializeActions(zeroPlayer, new(-1, 0)), "Invalid map entry is rejected");
        Throws<InvalidOperationException>(() => new GameSession(catalog, location: new() { MapId = 999 }), "Missing start map is explicit");
        Throws<ArgumentException>(() => new GameSession(catalog, tileChanges: [new(17, new(13, 0), 2)]), "Invalid persistent coordinates are rejected");
        Throws<ArgumentException>(() => new TownMap(catalog.CreateMap(57)!), "Town presentation cannot silently render a different map");
        return records;

        void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); }
            catch (T) { check(true, label); return; }
            check(false, label);
        }
    }
}
