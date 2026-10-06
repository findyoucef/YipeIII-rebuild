using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class EncounterChecks
{
    public static int Run(Action<bool, string> check, string? tracePath = null)
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        var catalog = MapCatalog.Load(source);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        void Row(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        static int B(bool x) => x ? 1 : 0;
        void Roaming(RoamingMonster? r) => Row($"{r?.TypeId ?? 0},{r?.Position.X ?? -1},{r?.Position.Y ?? -1}");
        void Pursuer(PursuingMonster? p) => Row($"{p?.TypeId ?? 0},{p?.Position.X ?? -1},{p?.Position.Y ?? -1},{p?.Origin.X ?? -1},{p?.Origin.Y ?? -1}");
        for (var level = 1; level <= 50; level++) for (var difficulty = 1; difficulty <= 3; difficulty++) for (var id = 1; id <= 134; id++)
            foreach (var p in MonsterPlacements.ForMap(id, difficulty, level))
            {
                var coverage = 0; for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) if (p.Contains(new(x, y))) coverage += x * 12 + y + 1;
                Row($"placement|{level},{difficulty},{id},{p.PlacementIndex},{p.TypeId},{p.Position.X},{p.Position.Y},{p.Slot},{p.EncounterSize},{p.CompanionType1},{p.CompanionType2},{B(p.RespawnsAfterMapReload)},{B(p.OccupiesTwoByTwoTiles)},{coverage}\n");
            }
        for (var env = -1; env <= 8; env++) for (var type = 0; type <= 42; type++) for (var alternate = 0; alternate < 2; alternate++)
            Row($"allowed|{env},{type},{alternate},{B(Encounters.Allowed(env, type, alternate != 0))}\n");
        uint[] seeds = [0, 1, uint.MaxValue];
        foreach (var seed in seeds) for (var id = 1; id <= 134; id++)
        {
            if (catalog.CreateMap(id) is null) continue;
            for (var mode = 0; mode < 4; mode++)
            {
                var map = catalog.CreateMap(id)!;
                var p = new Player { Level = mode == 0 ? 1 : mode == 1 ? 8 : mode == 2 ? 25 : 45,
                    Difficulty = mode % 3 + 1, MaximumHealth = 100, Health = mode == 0 ? 1 : 100, AlternateMapSet = mode == 3 };
                Array.Fill(p.EncounteredMonsterTypes, mode > 1);
                var placements = MonsterPlacements.ForMap(id, p.Difficulty, p.Level);
                var l = new WorldLocation { MapId = id, Position = new(5, 5) };
                var v = new MapVisibility(); v.Initialize(map, l.Position);
                var q = Encounters.PursuerForMap(map, p.Level);
                RoamingMonster? r = mode == 1 ? new(3, new(2, 2)) : null;
                var random = new MsvcRandom(seed);
                Row($"initial|{seed},{id},{mode}|"); Pursuer(q); writer.Write('\n');
                for (var i = 0; i < 12; i++)
                {
                    if (i == 6) { l.Position = new(10, 8); v.RevealFrom(map, l.Position); }
                    var update = Encounters.Update(ref r, map, p, l, map.Tile(l.Position), placements, v, mode == 0 ? 20 : mode == 1 ? 24 : 33, random, q?.Position);
                    var move = q is null ? RoamingMonsterMove.Stayed : Encounters.MovePursuer(q, map, l.Position, placements, r, v, random);
                    Row($"update|{seed},{id},{mode},{i},{(int)update},{(int)move},{random.State}|"); Roaming(r); writer.Write('|'); Pursuer(q); writer.Write('\n');
                    if (update == RoamingMonsterUpdate.EncounteredPlayer) r = null;
                }
            }
        }
        foreach (var seed in seeds) for (var type = 1; type <= 41; type++) for (ushort tile = 0; tile <= 0x71; tile++)
        {
            var map = new Map(69, 1, Enumerable.Repeat(tile, Map.TileCount).ToArray()); var m = new RoamingMonster(type, new(5, 5)); var random = new MsvcRandom(seed);
            for (var i = 0; i < 3; i++)
            {
                var result = Encounters.Move(m, map, new(7, 7), [], random, new(6, 6));
                Row($"terrain|{seed},{type},{tile},{i},{(int)result},{m.Position.X},{m.Position.Y},{random.State}\n");
            }
        }
        var text = writer.ToString(); if (tracePath is not null) File.WriteAllText(tracePath, text);
        var records = text.Count(c => c == '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        check(records == 88298 && hash == "84ce49c678d035b4a98274973df4406b975d5cb25e0cd24722c98749ef63a373", $"C++ placements/roaming/pursuers: {records} state/RNG records (SHA-256 {hash})");
        return records;
    }
}
