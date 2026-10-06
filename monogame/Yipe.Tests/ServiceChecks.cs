using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class ServiceChecks
{
    public static int Run(Action<bool, string> check, string? tracePath = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        uint[] seeds = [0, 1, 2, 0x7fffffff, 0x80000000, 0xffffffff, 0xdeadbeef, 2566201570];
        void Row(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        void Result(int status, int amount, uint rng, Player p)
        {
            Row($"{status},{amount},{rng}"); PlayerChecks.State(writer, p);
        }
        static int B(bool value) => value ? 1 : 0;
        foreach (var map in new[] { 17, 27, 8, 12 }) for (ushort stage = 0; stage < 5; stage++)
            for (var key = 0; key < 2; key++) for (var difficulty = 1; difficulty <= 3; difficulty++)
            {
                var basis = new Player { MiscellaneousShopStage = stage, HasRainbowKey = key != 0, Difficulty = difficulty };
                foreach (var item in Shops.MiscellaneousStock(basis, map)) for (var mode = 0; mode < 5; mode++)
                {
                    var p = new Player { MiscellaneousShopStage = stage, HasRainbowKey = key != 0, Difficulty = difficulty,
                        Gold = mode == 0 ? 0 : mode == 1 ? item.Price : 99999 };
                    if (mode == 3) Array.Fill(p.Inventory, (ushort)1);
                    if (mode == 4) p.HelmetRank = p.GlovesRank = p.RingRank = p.BootsRank = p.NecklaceRank = 10;
                    var r = Shops.Purchase(p, item);
                    Row($"shop|{map},{stage},{key},{difficulty},{mode},{item.Name},{item.Price},{(int)item.Kind},{item.Type},{item.Tier}|");
                    Result((int)r.Status, r.InventorySlot, 1, p);
                }
            }
        for (ushort stage = 0; stage < 5; stage++) foreach (var level in new[] { 25, 26 })
            foreach (var difficulty in new[] { 1, 2, 3 }) foreach (var strength in new[] { 30, 31, 32, 33 })
            {
                var p = new Player { MiscellaneousShopStage = stage, Level = level, Difficulty = difficulty, Strength = strength };
                var notice = Shops.EnterMiscellaneous(p); writer.Write("enter|"); Result(B(notice), 0, 1, p);
            }
        foreach (var kind in new[] { EquipmentKind.Weapon, EquipmentKind.Armor })
        {
            foreach (var map in new[] { 17, 27, 8, 12 }) foreach (var item in Shops.EquipmentStock(kind, map))
                Row($"stock|{(int)kind},{map},{item.Rank},{item.Price}\n");
            foreach (var owned in new[] { 1, 2, 15 }) for (var rank = -1; rank <= 43; rank++) foreach (var offset in new[] { -1, 0, 1 })
            {
                var p = new Player { WeaponRank = owned, ArmorRank = owned, Gold = Shops.EquipmentPrice(kind, rank) + offset };
                var confirmation = Shops.SaleRequiresConfirmation(p, kind); var sale = Shops.SaleValue(p, kind);
                var buy = Shops.PurchaseEquipment(p, kind, rank);
                Row($"equip|{(int)kind},{owned},{rank},{offset},{B(confirmation)},{sale}|"); Result((int)buy, 0, 1, p);
                var sold = Shops.SellEquipment(p, kind); writer.Write("sell|"); Result((int)sold, 0, 1, p);
            }
        }
        foreach (var seed in seeds) foreach (var level in new[] { 1, 17, 18, 36 }) for (var service = 0; service < 6; service++)
            foreach (var health in new[] { 0, 1, 50, 51 }) for (var rich = 0; rich < 2; rich++)
            {
                var p = new Player { Level = level, MaximumHealth = 50, MaximumSpellPoints = 50, Health = health,
                    SpellPoints = health, Gold = rich != 0 ? 1000 : 0 };
                var random = new MsvcRandom(seed);
                var r = service < 4 ? Shops.PurchaseHealing(p, service, random) : Shops.PurchaseSpellHealing(p, service == 5, random);
                Row($"heal|{seed},{level},{service},{health},{rich}|"); Result((int)r.Status, r.Amount, random.State, p);
            }
        for (var difficulty = 1; difficulty <= 3; difficulty++) for (var known = 0; known <= 12; known++)
            foreach (var level in new[] { 1, 7, 17, 40, 50 }) foreach (var gold in new[] { 0, 100, 101, 1000, 1001 })
            {
                var p = new Player { Difficulty = difficulty, Level = level, Gold = gold };
                Array.Clear(p.Spells); for (var i = 0; i < known; i++) p.Spells[i] = 1;
                var offer = Shops.NextSpell(p);
                Row($"learn|{difficulty},{known},{level},{gold},{offer?.Index ?? -1},{offer?.RequiredLevel ?? 0}|");
                var r = Shops.LearnNextSpell(p); Result((int)r, 0, 1, p);
            }
        for (var environment = -1; environment <= 8; environment++) for (var ability = 0; ability <= 4; ability++)
            foreach (var gold in new[] { 0, 149, 150, 250, 400 })
            {
                var p = new Player { SpecialAbility = (SpecialAbility)ability, Gold = gold };
                var offer = Shops.AbilityForEnvironment(environment);
                Row($"train|{environment},{ability},{gold},{(int)(offer?.Ability ?? SpecialAbility.None)}|");
                var r = Shops.TrainAbility(p, environment); Result((int)r, 0, 1, p);
            }
        foreach (var seed in seeds) for (var spell = 0; spell < 12; spell++) for (var specialty = 0; specialty < 4; specialty++)
            for (var power = 1; power <= 2; power++) for (var variant = 0; variant < 5; variant++)
            {
                var p = new Player { Specialty = specialty, Level = 17, Intelligence = 25, Health = 3, MaximumHealth = 100,
                    AbilityPoints = 1, MaximumAbilityPoints = 10, HasBonusHealingRing = variant == 4,
                    MonsterRepellentSteps = 65530, RandomEncounterThreshold = variant == 4 ? (ushort)125 : (ushort)85,
                    SpellPoints = variant == 1 ? 0 : 500 };
                p.Spells[spell] = variant == 0 ? (ushort)0 : (ushort)1;
                var l = new WorldLocation { MapLayer = variant == 2 ? 0 : 1, AlternateMapSet = variant == 3 };
                var random = new MsvcRandom(seed);
                var r = Spells.CastExploration(p, l, spell, (SpellPower)power, random);
                Row($"spell|{seed},{spell},{specialty},{power},{variant},{Spells.PointCost(p, spell, (SpellPower)power)},{r.RepellentDuration},{B(r.LocationChanged)}|");
                Result((int)r.Status, r.Healing, random.State, p);
            }
        foreach (var seed in seeds)
        {
            var random = new MsvcRandom(seed); var slots = new SlotMachineState { Gold = 1000 };
            for (var i = 0; i < 100; i++)
            {
                if (i % 3 == 0) Casino.InsertCredits(slots);
                var s = Casino.Spin(slots, random);
                Row($"slots|{seed},{i},{B(s.Spun)},{s.Payout},{s.Symbols[0]},{s.Symbols[1]},{s.Symbols[2]},{slots.Gold},{slots.Credits},{random.State}\n");
                if (i % 7 == 0) Casino.CollectCredits(slots);
            }
            for (var selected = 0; selected < 5; selected++) foreach (var bet in new[] { 0, 1, 100, 250 })
            {
                var wager = new CasinoWager { Gold = 1000, Bet = bet }; var race = Casino.Race(wager, selected, random);
                Row($"race|{seed},{selected},{bet},{B(race.Raced)},{race.Winner},{race.Payout},{race.FinalPositions[0]},{race.FinalPositions[1]},{race.FinalPositions[2]},{wager.Gold},{wager.Bet},{random.State}\n");
            }
            for (var aim = 16; aim <= 80; aim++) for (var i = 0; i < 3; i++)
            {
                var dart = Casino.ThrowDart(aim, i, random);
                Row($"dart|{seed},{aim},{i},{dart.FinalY},{dart.Score},{random.State}\n");
            }
        }
        foreach (var gold in new[] { 0, 10, 100, 1000 }) foreach (var bet in new[] { 0, 249, 250 }) foreach (var amount in new[] { -1, 0, 1, 10, 250, 251 })
        {
            var wager = new CasinoWager { Gold = gold, Bet = bet }; var status = Casino.PlaceWager(wager, amount);
            Row($"wager|{gold},{bet},{amount},{(int)status},{wager.Gold},{wager.Bet}");
            Casino.ClearWager(wager); Row($",{wager.Gold},{wager.Bet}\n");
        }
        for (var aim = 10; aim <= 90; aim++) Row($"aim|{aim},{Casino.AdjustDartAim(aim, false)},{Casino.AdjustDartAim(aim, true)}\n");
        for (var total = 0; total <= 180; total++) Row($"payout|{total},{Casino.DartPayout([total, 0, 0], 250)}\n");
        var text = writer.ToString();
        if (tracePath is not null) File.WriteAllText(tracePath, text);
        var count = text.Count(c => c == '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        check(count == 17883 && hash == "6bc81a9e9cbdf9804af21d4f71fd768f0564db56e2b677d4edb2725b83eb5eb8", $"C++ shops/spells/casinos: {count} state/RNG records (SHA-256 {hash})");
        var stageThree = new Player { MiscellaneousShopStage = 3, HasRainbowKey = true };
        Shops.LeaveMiscellaneous(stageThree);
        check(stageThree.MiscellaneousShopStage == 4 && Shops.MiscellaneousStock(stageThree, 12)[8].Type == 2,
            "Closing Outback after its key purchase restores the accessory row");
        return count;
    }
}
