using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class ExplorationChecks
{
    public static int Run(Action<bool, string> check, string? tracePath = null)
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        var catalog = MapCatalog.Load(source);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        void Row(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        static int B(bool value) => value ? 1 : 0;
        void State(Player p) => PlayerChecks.State(writer, p);
        void Visibility(MapVisibility v)
        {
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) writer.Write(B(v.Contains(new(x, y))));
            writer.Write(',');
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) writer.Write(B(v.IsExpanded(new(x, y))));
        }
        for (var id = 1; id <= 134; id++)
        {
            if (catalog.CreateMap(id) is null) continue;
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++)
            {
                var map = catalog.CreateMap(id)!; var v = new MapVisibility(); v.Initialize(map, new(x, y));
                Row($"visibility|{id},{x},{y}|"); Visibility(v);
                map.RestoreTile(new(x, y), 0x27); v.RevealFrom(map, new((x + 2) % 13, (y + 3) % 12));
                writer.Write('|'); Visibility(v); writer.Write('\n');
            }
        }
        uint[] seeds = [0, 1, uint.MaxValue];
        foreach (var seed in seeds) foreach (var id in new[] { 3, 6, 17, 18, 27, 33, 69, 87, 109 })
            for (ushort tile = 0; tile <= 0x71; tile++) for (var variant = 0; variant < 4; variant++)
            {
                var map = new Map(id, 0, Enumerable.Repeat(tile, Map.TileCount).ToArray());
                var p = new Player { Level = variant == 0 ? 1 : 20, Health = variant == 0 ? 1 : variant == 1 ? 3 : 100,
                    MaximumHealth = 100, Difficulty = variant % 3 + 1, Intelligence = 25, HasBonusHealingRing = variant == 3,
                    CaveCoinsCollected = variant > 1, BridgeTreasureAvailable = variant < 2, HealingWaterAvailable = variant != 2,
                    SnowSearchCount = (ushort)variant, GatekeeperState = (ushort)variant };
                var random = new MsvcRandom(seed); var r = WorldActions.Other(p, map, new(5, 5), random);
                Row($"other|{seed},{id},{tile},{variant},{r.MessageResourceId},{r.SoundId},{r.Message},{map.Tile(new(3, 7))},{map.Tile(new(4, 7))},{random.State}"); State(p);
            }
        for (var id = 0; id <= 134; id++) for (var variant = 0; variant < 2; variant++)
        {
            var p = new Player { Level = variant != 0 ? 11 : 10, CaveCoinsCollected = variant != 0, HasDefeatedWormoid = variant != 0 };
            p.InventoryValues[11] = (ushort)variant;
            Row($"hint|{id},{variant},{WorldActions.MovementHint(p, id)}\n");
        }
        for (var solved = 0; solved < 2; solved++) foreach (var uses in new[] { 0, 1, 5 }) foreach (var code in new[] { "", "hhhhhh", "HHHHHH", "hhhhhhh" })
        {
            var p = new Player { HasBonusHealingRing = solved != 0, RestorationUses = (ushort)uses };
            var r = WorldActions.EnterTreeCode(p, code); Row($"tree|{solved},{uses},{code},{(int)r}"); State(p);
        }
        {
            var p = new Player { MaximumSpellPoints = 500, SpellPoints = 0, MaximumAbilityPoints = 50, AbilityPoints = 0 };
            var timer = new ExplorationTimer(); var a = catalog.CreateMap(5)!; var b = catalog.CreateMap(57)!;
            for (var i = 0; i < 760; i++)
            {
                var map = i / 190 % 2 == 0 ? a : b;
                var pos = i % 76 is >= 40 and <= 65 ? new MapPosition(7, 8) : new(5, 5);
                var tick = timer.Advance(p, map, pos); Row($"timer|{i},{map.Id},{tick},{map.Tile(new(7, 8))}"); State(p);
            }
        }
        foreach (ushort steps in new ushort[] { 0, 1, 2, 32767, 32768, 65535 })
            foreach (ushort saved in new ushort[] { 0, 85, 100, 101, 32767, 32768, 65535 }) foreach (var difficulty in new[] { 1, 2, 3 })
            {
                var p = new Player { MonsterRepellentSteps = steps, SavedRandomEncounterThreshold = saved, RandomEncounterThreshold = 125, Difficulty = difficulty };
                for (var i = 0; i < 3; i++) { var ended = Exploration.AdvanceRepellent(p); Row($"repel|{steps},{saved},{difficulty},{i},{B(ended)}"); State(p); }
            }
        for (var id = 1; id <= 134; id++)
        {
            if (Exploration.HazardForMap(id) is not { } h) continue;
            foreach (var seed in seeds) foreach (ushort tile in new ushort[] { 0x24, 0x2a, 0x3c, 0x27, 0x28, 0x29, 0x53, 0x2b, 0x3d, 2 })
                for (var rider = 0; rider < 3; rider++) for (var revealed = 0; revealed < 2; revealed++)
                {
                    var map = new Map(id, 0, Enumerable.Repeat((ushort)0x27, Map.TileCount).ToArray()); var v = new MapVisibility();
                    if (revealed != 0) v.Initialize(map, new(5, 5));
                    map.RestoreTile(h.First, tile); map.RestoreTile(h.Second, tile);
                    var p = new Player { MaximumHealth = 100, Health = 1, Level = 5 };
                    var pos = rider == 0 ? new MapPosition(5, 5) : rider == 1 ? h.First : h.Second;
                    var random = new MsvcRandom(seed); var r = Exploration.AdvanceHazard(map, v, p, pos, h.First, random);
                    Row($"hazard|{id},{seed},{tile},{rider},{revealed},{B(r.Changed)},{B(r.Fell)},{r.SoundId},{r.Damage},{B(!r.RemoveRoaming)},{map.Tile(h.First)},{map.Tile(h.Second)},{random.State}"); State(p);
                }
        }
        for (var direction = 0; direction <= 6; direction++) foreach (var seed in seeds) for (var blocked = 0; blocked < 2; blocked++) for (var rider = 0; rider < 2; rider++)
        {
            var map = new Map(74, 5, Enumerable.Repeat((ushort)0x0c, Map.TileCount).ToArray(), new MapEvents { Overlay = new(new(5, 5), direction) });
            var v = new MapVisibility(); v.Initialize(map, new(5, 5)); var random = new MsvcRandom(seed);
            var pos = rider != 0 ? new MapPosition(5, 5) : new(1, 1);
            for (var i = 0; i < 40; i++)
            {
                var carried = map.AdvanceOverlay(v, ref pos, random, blocked != 0 ? new(5, 4) : null);
                Row($"overlay|{direction},{seed},{blocked},{rider},{i},{B(carried)},{pos.X},{pos.Y},{map.Events.Overlay.Position.X},{map.Events.Overlay.Position.Y},{map.Events.Overlay.Value},{random.State}\n");
            }
        }
        var text = writer.ToString(); if (tracePath is not null) File.WriteAllText(tracePath, text);
        var count = text.Count(c => c == '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        check(count == 37096 && hash == "382fbcd9e06d4a4b92b1dd3939f6068df6b54d7c41c0914c3f2db345cadc08b5", $"C++ visibility/actions/timers/hazards/platforms: {count} state/RNG records (SHA-256 {hash})");
        return count;
    }
}
