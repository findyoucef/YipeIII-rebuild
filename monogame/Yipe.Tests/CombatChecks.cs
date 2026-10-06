using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class CombatChecks
{
    public static int Run(Action<bool, string> check, string? tracePath = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        void Row(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        static int B(bool x) => x ? 1 : 0;
        void State(Player p) => PlayerChecks.State(writer, p);
        void Reward(CombatReward r) => Row($"{r.Experience},{r.Gold},{(int)r.EquipmentUpgrade},{B(r.UnderworldKey)}");
        void Encounter(CombatEncounter e)
        {
            Row($"|{e.SelectedTarget},{e.MapEnvironment},"); Reward(e.PendingReward);
            foreach (var m in e.Monsters) Row($"|{m.TypeId},{m.Name},{m.BitmapResourceId},{m.Health},{m.MaximumHealth},{m.SpecialRating},{m.Rank},{m.AttackRating},{m.DefenseRating},{B(m.GatekeeperEmpowered)}");
        }
        void Turn(CombatTurn t)
        {
            Row($"|{t.PlayerDamage},{t.MonsterDamage},{(int)t.Outcome},"); foreach (var x in t.MonsterDamageBySlot) Row($"{x},"); Reward(t.Reward);
            Row($",{B(t.LevelUp.LeveledUp)},{t.LevelUp.HealthIncrease},{t.LevelUp.SpellPointIncrease},{(int)t.LevelUp.StatIncrease}");
        }
        static Player Profile(int level, int difficulty, int mode)
        {
            var p = new Player { Level = level, Experience = level == 1 ? 0 : level * level * 30, Difficulty = difficulty,
                Strength = 6 + level, Dexterity = 5 + level, Intelligence = 4 + level, Health = 12 + level * 4,
                MaximumHealth = 12 + level * 4, SpellPoints = 30 + level * 3, MaximumSpellPoints = 30 + level * 3,
                AbilityPoints = 15, MaximumAbilityPoints = 15, WeaponRank = 2 + level / 2, ArmorRank = 2 + level / 2,
                HelmetRank = (ushort)(mode + 1), GlovesRank = (ushort)mode, BootsRank = (ushort)mode, RingRank = (ushort)mode,
                NecklaceRank = (ushort)mode, SpecialAbility = (SpecialAbility)mode, Specialty = mode % 4,
                HasLuckyRabbitFoot = mode == 4, HasBonusHealingRing = mode == 3 };
            Array.Fill(p.Spells, (ushort)1); return p;
        }
        for (var id = 1; id <= 41; id++)
        {
            var d = CombatCatalog.Monster(id);
            Row($"definition|{id},{d.Name},{d.StringResourceId},{d.BitmapResourceId},{d.HealthBase},{d.RankAndHealthRange},{d.AttackBase},{d.DefenseBase},{d.SpecialRating},{d.DamageMinimum},{d.DamageMaximum}\n");
        }
        for (var rank = -1; rank <= 44; rank++) { var (a, b) = CombatCatalog.WeaponDamageRange(rank); Row($"weapon|{rank},{a},{b}\n"); }
        uint[] seeds = [0, 1, uint.MaxValue];
        foreach (var seed in seeds) for (var id = 1; id <= 41; id++) for (var count = 0; count <= 3; count++) for (var mode = 0; mode < 5; mode++)
        {
            var level = mode == 0 ? 1 : mode == 1 ? 4 : mode == 2 ? 10 : mode == 3 ? 25 : 45;
            var difficulty = mode % 3 + 1; var p = Profile(level, difficulty, mode); var random = new MsvcRandom(seed);
            var setup = new EncounterSetup { PlayerLevel = level, Difficulty = difficulty, CompanionType1 = mode == 2 ? 1 : 0,
                CompanionType2 = mode == 2 ? 2 : 0, GatekeeperState = mode == 4 ? 1 : 0, MapEnvironment = id - 34 };
            p.GatekeeperState = (ushort)setup.GatekeeperState;
            var e = Combat.StartMonsterEncounter(id, count, random, setup);
            Row($"start|{seed},{id},{count},{mode},{Combat.PlayerAttackEquipmentRating(p)},{Combat.PlayerDefenseEquipmentRating(p)},{random.State}"); Encounter(e); State(p);
            for (var i = 0; i < 16; i++)
            {
                var t = i == 0 && mode > 0 ? Combat.ResolveSpecialAbilityTurn(p, e, random)
                    : i % 3 == 1 && mode > 1 ? Combat.ResolveSpellTurn(p, e, mode == 2 ? 2 : mode == 3 ? 6 : 7, i % 2 != 0 ? SpellPower.High : SpellPower.Low, random).Turn
                    : Combat.ResolveAttackTurn(p, e, random);
                Row($"fight|{seed},{id},{count},{mode},{i},{random.State}"); Turn(t); Encounter(e); State(p);
                if (t.Outcome != CombatOutcome.Ongoing) break;
            }
        }
        foreach (var seed in seeds) for (var spell = 0; spell < 12; spell++) for (var power = 1; power <= 2; power++) for (var count = 1; count <= 3; count++) for (var mode = 0; mode < 5; mode++)
        {
            var p = Profile(30, 2, mode); p.Health = 15; p.Spells[spell] = mode == 0 ? (ushort)0 : (ushort)1;
            if (mode == 1) p.SpellPoints = 0;
            var random = new MsvcRandom(seed); var e = Combat.StartMonsterEncounter(mode == 4 ? 35 : 4, count, random, new() { PlayerLevel = 30 });
            e.SelectedTarget = count - 1;
            var s = Combat.ResolveSpellTurn(p, e, spell, (SpellPower)power, random);
            Row($"spell|{seed},{spell},{power},{count},{mode},{(int)s.Status},{s.Healing},"); foreach (var x in s.DamageBySlot) Row($"{x},");
            Row($"{random.State}"); Turn(s.Turn); Encounter(e); State(p);
        }
        foreach (var seed in seeds) for (var id = 1; id <= 41; id++) for (var mode = 0; mode < 5; mode++)
        {
            var p = Profile(mode == 0 ? 1 : 40, mode % 3 + 1, mode); p.Health = mode == 0 ? 2 : mode == 1 ? 6 : mode == 2 ? 20 : 100;
            var random = new MsvcRandom(seed); var e = Combat.StartMonsterEncounter(id, 3, random, new() { PlayerLevel = p.Level });
            var f = Combat.TryFlee(p, e, random);
            Row($"flee|{seed},{id},{mode},{B(f.Escaped)},{f.MonsterDamage},{B(f.PlayerDefeated)},"); foreach (var x in f.MonsterDamageBySlot) Row($"{x},");
            Row($"{random.State}"); Encounter(e); State(p);
            if (p.Health > 0 && !f.Escaped) { var t = Combat.ResolveMonsterTurn(p, e, random); Row($"counter|{random.State}"); Turn(t); Encounter(e); State(p); }
        }
        foreach (var seed in new uint[] { 0, 1, uint.MaxValue, 0xdeadbeef }) foreach (var level in new[] { 1, 15, 25, 50 }) for (var kind = 0; kind <= 4; kind++) foreach (var threshold in new[] { -10, 50, 100 }) for (var mode = 0; mode < 5; mode++)
        {
            var p = Profile(level, 2, mode); p.WeaponRank = mode == 0 ? 2 : mode == 1 ? 12 : mode == 2 ? 14 : mode == 3 ? 23 : 29;
            p.ArmorRank = mode == 4 ? 26 : 25; p.EquipmentUpgradeCount = mode % 3; p.LootRollPenalty = mode == 3 ? 100 : 2;
            p.StatCapMode = mode % 3; p.CurrentMapId = mode == 4 ? 0x63 : 0x11;
            if (mode == 2) Array.Fill(p.Inventory, (ushort)1);
            var random = new MsvcRandom(seed);
            for (var i = 0; i < 8; i++)
            {
                var loot = Combat.RollTreasure(p, threshold, kind, random);
                Row($"loot|{seed},{level},{kind},{threshold},{mode},{i},{(int)loot.EquipmentUpgrade},{loot.ItemId},{B(loot.InventoryFull)},{B(loot.WaterproofShoeSpray)},{random.State}"); State(p);
            }
        }
        var text = writer.ToString(); if (tracePath is not null) File.WriteAllText(tracePath, text);
        var records = text.Count(c => c == '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        check(records == 28363 && hash == "1ccfefc62b63a47f04b1f433039fc74553e094b6d6eb520f4734dfa9310ffffa", $"C++ combat/spells/loot: {records} state/RNG records (SHA-256 {hash})");
        return records;
    }
}
