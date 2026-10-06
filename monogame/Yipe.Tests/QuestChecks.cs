using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class QuestChecks
{
    public static int Run(Action<bool, string> check, string? tracePath = null)
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        var catalog = MapCatalog.Load(source);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        void Row(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        static int B(bool x) => x ? 1 : 0;
        void State(Player p) => PlayerChecks.State(writer, p);
        uint[] seeds = [0, 1, uint.MaxValue];
        foreach (var seed in seeds) for (ushort stamps = 0; stamps <= 6; stamps++) foreach (ushort princesses in new ushort[] { 0, 3, 10, 19, 20 }) for (ushort gate = 0; gate <= 3; gate++) for (var mode = 0; mode < 3; mode++)
        {
            var p = new Player { StampsFound = stamps, PrincessesFound = princesses, GatekeeperState = gate,
                QuestProgressTotal = 60, HasWaterproofShoeSpray = mode > 0 };
            if (mode == 1) { p.QuestFlags[0] = true; p.QuestFlags[1] = true; }
            else if (mode == 2) Array.Fill(p.QuestFlags, true);
            var r = new MsvcRandom(seed);
            for (var i = 0; i < 8; i++)
            {
                var k = Quests.TalkToKing(p, r, i * 3);
                Row($"king|{seed},{stamps},{princesses},{gate},{mode},{i},{k.GreetingResourceId},{k.MessageResourceId},{k.FooterResourceId},{k.Greeting},{k.Message},{k.Footer},{r.State}"); State(p);
            }
        }
        foreach (var seed in seeds) foreach (var level in new[] { 1, 15, 30, 45 }) for (var gift = 1; gift <= 4; gift++) for (var mode = 0; mode < 6; mode++) foreach (var index in new[] { 0, 1, 2, 13, 19, 29 })
        {
            var p = new Player { Level = level, MaximumHealth = 100, Health = 3, MaximumSpellPoints = 80, SpellPoints = 1,
                MaximumAbilityPoints = 10, AbilityPoints = 2, HasRainbowKey = mode == 1, RingRank = (ushort)mode,
                PrincessMysteryUses = (ushort)(mode * 5), PrincessesFound = mode == 5 ? ushort.MaxValue : (ushort)0 };
            var r = new MsvcRandom(seed);
            for (var i = 0; i < 2; i++)
            {
                var v = Quests.RescuePrincess(p, index, (PrincessGift)gift, r);
                Row($"rescue|{seed},{level},{gift},{mode},{index},{i},{B(v.Rescued)},{v.MessageResourceId},{v.Amount},{v.Message},{r.State}"); State(p);
            }
        }
        foreach (var seed in seeds) for (var id = 1; id <= 134; id++)
        {
            if (catalog.CreateMap(id) is null) continue;
            for (var slot = 0; slot < 4; slot++)
            {
                var map = catalog.CreateMap(id)!; var p = new Player { Level = id % 40 + 1, Difficulty = id % 3 + 1 };
                var r = new MsvcRandom(seed); var pos = map.Events.Chests[slot];
                for (var i = 0; i < 2; i++)
                {
                    var v = Quests.OpenChest(p, map, pos, slot, r);
                    Row($"chest|{seed},{id},{slot},{i},{B(v.Opened)},{v.Experience},{v.Gold},{(int)v.Loot.EquipmentUpgrade},{v.Loot.ItemId},{B(v.Loot.InventoryFull)},{B(v.Loot.WaterproofShoeSpray)},{map.Events.Chests[slot].X},{map.Events.Chests[slot].Y},{r.State}"); State(p);
                }
            }
        }
        foreach (var seed in seeds) foreach (var level in new[] { 1, 2, 12, 31, 45 }) for (var deaths = 0; deaths < 13; deaths++) foreach (var monster in new[] { 1, 5, 41 }) for (var mode = 0; mode < 4; mode++)
        {
            var p = new Player { Level = level, DeathCount = deaths, Gold = mode * 20, Health = 0,
                Strength = mode == 0 ? 10 : 40, Dexterity = mode == 0 ? 10 : 40, ArmorRank = mode * 3,
                BootsRank = (ushort)mode, GlovesRank = (ushort)mode, RingRank = (ushort)mode, GatekeeperState = mode == 3 ? (ushort)1 : (ushort)0 };
            var l = new WorldLocation { MapId = 57, MapLayer = 1, RegionX = 0, RegionY = 0, Position = new(5, 8) };
            var r = new MsvcRandom(seed); var d = Quests.ResolveCombatDefeat(p, l, monster, r);
            Row($"death|{seed},{level},{deaths},{monster},{mode},{d.MessageResourceId},{d.Message},{r.State}|{l.MapId},{l.MapLayer},{l.RegionX},{l.RegionY},{l.Position.X},{l.Position.Y},{B(l.AlternateMapSet)}"); State(p);
        }
        for (var difficulty = 1; difficulty <= 3; difficulty++) foreach (var level in new[] { 1, 25, 50 }) foreach (var gold in new[] { 0, 1, 9, 10, 99, 1000, 10000 })
        {
            var p = new Player { Difficulty = difficulty, Level = level, Gold = gold, MaximumHealth = 100, Health = 1 };
            var quote = Quests.GatekeeperRecoveryFee(p); var fee = Quests.PayGatekeeperRecovery(p);
            Row($"fee|{difficulty},{level},{gold},{quote},{fee}"); State(p);
        }
        for (ushort gate = 0; gate <= 3; gate++) for (var rescued = 0; rescued <= 30; rescued++)
        {
            var p = new Player { GatekeeperState = gate }; for (var i = 0; i < rescued; i++) p.InventoryValues[i] = 1;
            var ended = Quests.CompleteGatekeeperFinale(p); Row($"finale|{gate},{rescued},{B(ended)}"); State(p);
        }
        var text = writer.ToString(); if (tracePath is not null) File.WriteAllText(tracePath, text);
        var records = text.Count(c => c == '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        check(records == 20311 && hash == "799f84ce66ba26e4b203c23e4e1f920a82575464815467725be743b2ca047b56", $"C++ quests/rewards/death: {records} state/RNG records (SHA-256 {hash})");
        return records;
    }
}
