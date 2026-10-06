using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yipe.Core;

internal static class PlayerChecks
{
    public static int Run(Action<bool, string> check, string? tracePath)
    {
        // Expected hashes come only from player_reference.cpp linked to the existing C++ core.
        uint[] seeds = [0, 1, 2, 0x7fffffff, 0x80000000, 0xffffffff, 0xdeadbeef, 2566201570];
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var traceFile = tracePath is null ? null : new StreamWriter(tracePath, false, new UTF8Encoding(false));
        var records = 0;
        void Row(FormattableString text) => writer.Write(text.ToString(CultureInfo.InvariantCulture));
        void Verify(string section, int count, string expected)
        {
            var text = writer.ToString();
            traceFile?.Write(text);
            traceFile?.Flush();
            var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            check(text.Count(c => c == '\n') == count && actual == expected,
                $"C++ player {section}: {count} complete reference records (actual SHA-256 {actual})");
            records += count;
            writer.GetStringBuilder().Clear();
        }
        void Growth(Player p, MsvcRandom random, LevelUpResult result)
        {
            Row($"|{(result.LeveledUp ? 1 : 0)},{result.HealthIncrease},{result.SpellPointIncrease},{(int)result.StatIncrease},{random.State}");
            State(writer, p);
        }
        void Item(Player p, MsvcRandom random, ItemUseResult result)
        {
            Row($"|{(result.Consumed ? 1 : 0)},{result.Amount},{result.SoundId},{result.Message},{random.State}");
            State(writer, p);
        }

        for (var type = 1; type <= 3; type++)
        {
            Row($"defaults|{type}");
            State(writer, new Player((CharacterType)type));
        }
        Verify("defaults", 3, "4c528861411d708114a4a408659306989ff372967a2ddc038677b97ac946f886");

        foreach (var seed in seeds)
        {
            var p = new Player(CharacterType.Wizard)
            {
                Name = "Reroll",
                Difficulty = 3,
                CombatStyle = 1,
                Level = 7,
                Experience = -123,
                Gold = 321,
                StatCapMode = 1,
                SillyMode = true,
                HellGatekeeperState = HellGatekeeperState.Paid,
                SurfaceReturnRegionX = -7,
                SurfaceReturnRegionY = -8,
            };
            p.Inventory[28] = 11;
            p.InventoryValues[29] = ushort.MaxValue;
            var random = new MsvcRandom(seed);
            for (var roll = 0; roll < 64; roll++)
            {
                p.RerollRandomCharacter(random);
                Row($"rerolls|{seed},{roll}|{random.State}");
                State(writer, p);
            }
        }
        Verify("rerolls", 512, "ea25157da8ff246f602b9eb57596fb23f85886340d3c4a3033926a7964deb4a7");

        foreach (var health in new[] { -10, 0, 1, 13, 20 })
            foreach (var delta in new[] { -20, -1, 0, 1, 20 })
            {
                var p = new Player { Health = health };
                var alive = p.AdjustHealth(delta);
                Row($"health|{health},{delta}|{(alive ? 1 : 0)}");
                State(writer, p);
            }
        Verify("health", 25, "81c4daafba0bf0fa881063e5ecbd22b6605ae829ba0699280cf6f04fd8c95387");

        for (var level = -2; level <= 102; level++)
            Row($"thresholds|{level}|{Player.ExperienceRequiredForNextLevel(level) ?? -1}\n");
        Verify("thresholds", 105, "5c9168d116d34d4708984cdd57f1b76f7faf38f4e43add127d882a88bbbe7fe4");

        foreach (var seed in seeds)
            for (var level = 1; level <= 100; level++)
                for (var difficulty = 1; difficulty <= 3; difficulty++)
                    for (var boundary = 0; boundary < 3; boundary++)
                    {
                        var required = Player.ExperienceRequiredForNextLevel(level) ?? 600000;
                        var p = new Player
                        {
                            Level = level,
                            Difficulty = difficulty,
                            CombatStyle = level % 4,
                            StatCapMode = level % 3,
                            Strength = 24,
                            Dexterity = 23,
                            Intelligence = difficulty % 2 == 0 ? 20 : 21,
                            Health = -3,
                            MaximumHealth = 80,
                            SpellPoints = 2,
                            MaximumSpellPoints = 99,
                            AbilityPoints = 1,
                            MaximumAbilityPoints = 9,
                            RestorationUses = boundary - 1,
                            Experience = boundary == 2 ? 2000000000 : required + boundary - 1,
                        };
                        var random = new MsvcRandom(seed);
                        var result = p.TryLevelUp(random);
                        Row($"growth|{seed},{level},{difficulty},{boundary}");
                        Growth(p, random, result);
                    }
        Verify("growth", 7200, "9e80b54d0ebe69fb25614beacf28b1d802f5921a995d5fc541813f276a246b4a");

        foreach (var mode in new[] { -1, 0, 1, 2, 3 })
            foreach (var style in new[] { 0, 1, 3 })
                foreach (var offset in new[] { -1, 0, 1 })
                {
                    var p = new Player
                    {
                        StatCapMode = mode,
                        CombatStyle = style,
                        Level = 4,
                        Experience = 1501,
                        RestorationUses = 2,
                    };
                    p.Strength = p.Dexterity = p.Intelligence = p.MaximumStat + offset;
                    var random = new MsvcRandom(1);
                    var result = p.TryLevelUp(random);
                    Row($"caps|{mode},{style},{offset}");
                    Growth(p, random, result);
                }
        Verify("caps", 45, "83226842cc6ad52308baf3b5378997fc933e6aa9c8708742fe697ac16f0772a5");

        foreach (var mode in new[] { -1, 0, 1, 2, 3 })
            foreach (var id in new[] { 6, 7, 8 })
                foreach (var offset in new[] { -1, 0, 1 })
                {
                    var p = new Player { StatCapMode = mode };
                    p.Strength = p.Dexterity = p.Intelligence = p.MaximumStat + offset;
                    p.Inventory[1] = (ushort)id;
                    var random = new MsvcRandom(1);
                    var result = p.UseInventoryItem(1, random, false);
                    Row($"stat_items|{mode},{id},{offset}");
                    Item(p, random, result);
                }
        Verify("stat_items", 45, "14f3b81a5499f8f6119d6dd618e26674de9ecd4e39f9a93dc795ec6ed96093df");

        int[] healthValues = [1, 13, 20, -20, 0, 12];
        int[] spellPoints = [0, 10, 15, -10, 1, 9];
        int[] intelligence = [1, 10, 20, 1000, 100, 10];
        int[] mapIds = [57, 57, 17, 155, 57, 40];
        ushort[] thresholds = [85, 99, 100, 125, 65535, 85];
        foreach (var seed in seeds)
            foreach (var id in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 65535 })
                foreach (var slot in new[] { 0, 12, 13, 28, 29 })
                    for (var variant = 0; variant < 6; variant++)
                    {
                        var p = new Player
                        {
                            Health = healthValues[variant],
                            SpellPoints = spellPoints[variant],
                            StatCapMode = variant % 3,
                            HasBonusHealingRing = variant % 2 == 1,
                            Level = variant == 3 ? 100 : variant + 1,
                            RandomEncounterThreshold = thresholds[variant],
                            SavedRandomEncounterThreshold = 73,
                            MonsterRepellentSteps = (ushort)(variant == 3 ? 65530 : 9),
                            SurfaceReturnMapId = (short)(variant == 1 ? 1 : -7),
                            PendingRelocation = 4,
                        };
                        p.Inventory[slot] = (ushort)id;
                        p.InventoryValues[slot] = ushort.MaxValue;
                        p.Strength = p.Dexterity = p.MaximumStat + variant % 3 - 1;
                        p.Intelligence = id == 8 ? p.Strength : intelligence[variant];
                        var random = new MsvcRandom(seed);
                        var result = p.UseInventoryItem(slot, random, (seed & 1) == 0,
                            variant == 4, mapIds[variant], variant == 3);
                        Row($"items|{seed},{id},{slot},{variant}");
                        Item(p, random, result);
                    }
        Verify("items", 3120, "035f4e136648aa28f1ae022d6060c0e4500b982daf442834f135da4d834d989b");

        foreach (var used in new[] { 0, 1 })
            foreach (var mapId in new[] { 39, 40 })
                foreach (var alternate in new[] { false, true })
                    foreach (var combat in new[] { false, true })
                    {
                        var p = new Player { SurfaceReturnMapId = (short)used };
                        p.Inventory[0] = 5;
                        var random = new MsvcRandom(1);
                        var result = p.UseInventoryItem(0, random, true, combat, mapId, alternate);
                        Row($"relocation|{used},{mapId},{(alternate ? 1 : 0)},{(combat ? 1 : 0)}");
                        Item(p, random, result);
                    }
        Verify("relocation", 16, "1f23e26c572181fc12666e06654d16e729820ef678de5674b76ce89925d37cac");

        var repellentPlayer = new Player();
        var repellentRandom = new MsvcRandom(1);
        for (var use = 0; use < 10; use++)
        {
            repellentPlayer.Inventory[0] = 4;
            var result = repellentPlayer.UseInventoryItem(0, repellentRandom);
            Row($"repellent|{use}");
            Item(repellentPlayer, repellentRandom, result);
        }
        Verify("repellent", 10, "a3c44bb5e18ee06cf00dfdb4f6735ee120c89cc7dc06f8f9fcf322971e8a4ad9");

        for (var slot = 0; slot < 30; slot++)
            foreach (var value in new[] { 0, 1, 2, 0x7fff, 0x8000, 0xffff })
            {
                var p = new Player { PrincessesFound = 20 };
                Array.Fill(p.Inventory, (ushort)1);
                Array.Fill(p.QuestFlags, true);
                p.InventoryValues[slot] = (ushort)value;
                Row($"rescues|{slot},{value}|{p.RescuedPrincessCount}");
                State(writer, p);
            }
        var allRescued = new Player();
        Array.Fill(allRescued.InventoryValues, (ushort)1);
        Row($"rescues|all|{allRescued.RescuedPrincessCount}");
        State(writer, allRescued);
        Verify("rescues", 181, "909e89378a274dac4e3d2c9c15ff9f6f9e8c8d10a37432d96670bbb63813c15e");

        var counts = new Player();
        for (var type = 1; type <= 50; type++)
        {
            counts.RecordMonsterDefeat(type);
            Row($"defeats|{type}");
            State(writer, counts);
        }
        foreach (var value in new[] { int.MaxValue, -1 })
        {
            counts.MonsterDefeats[34] = value;
            counts.QuestProgressTotal = value;
            counts.RecordMonsterDefeat(35);
            Row($"defeats|wrap,{value}");
            State(writer, counts);
        }
        foreach (var type in new[] { -1, 0, 51, int.MinValue, int.MaxValue })
        {
            var rejected = false;
            try { counts.RecordMonsterDefeat(type); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Row($"defeats|invalid,{type}|{(rejected ? 1 : 0)}");
            State(writer, counts);
        }
        Verify("defeats", 57, "fdca9053b02720431f067c71a58871f05db22f948712e705b30cbb2f36cf62ca");
        foreach (var slot in new[] { -1, 30, int.MaxValue })
        {
            var random = new MsvcRandom(1);
            var rejected = false;
            try { counts.UseInventoryItem(slot, random); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Row($"invalid_slots|{slot}|{(rejected ? 1 : 0)},{random.State}");
            State(writer, counts);
        }
        Verify("invalid_slots", 3, "6d2c41a5deb2d1d2a374ac706802cf77395f0a6fc351b9e070a04ffb19f72c84");

        // Direct assertions explain important contracts independently of the trace encoding.
        var fresh = new Player();
        check(Player.CarriedInventorySize == 13 && fresh.Inventory.Length == 30
            && fresh.InventoryValues.Length == 30 && fresh.Spells.Length == 15
            && fresh.QuestFlags.Length == 30 && fresh.OpenedChests.Length == 128
            && fresh.EncounteredMonsterTypes.Length == 42 && fresh.MonsterDefeats.Length == 50,
            "Player arrays preserve original sizes, including the 13/30 inventory distinction");
        check(!ReferenceEquals(fresh.Inventory, counts.Inventory) && !ReferenceEquals(fresh.InventoryValues, counts.InventoryValues)
            && !ReferenceEquals(fresh.Spells, counts.Spells) && !ReferenceEquals(fresh.QuestFlags, counts.QuestFlags)
            && !ReferenceEquals(fresh.OpenedChests, counts.OpenedChests)
            && !ReferenceEquals(fresh.EncounteredMonsterTypes, counts.EncounteredMonsterTypes)
            && !ReferenceEquals(fresh.MonsterDefeats, counts.MonsterDefeats), "Characters own independent arrays");
        check((short)HellGatekeeperState.Paid == -1 && (short)HellGatekeeperState.Defeated == -2,
            "Hell gatekeeper outcomes retain their signed values");

        var creationRandom = new MsvcRandom(1);
        fresh.RerollRandomCharacter(creationRandom);
        check(fresh.Dexterity == 13 && fresh.Strength == 13 && fresh.Intelligence == 12
            && fresh.Health == 12 && fresh.SpellPoints == 13 && fresh.AbilityPoints == 5
            && creationRandom.State == 10044473, "The first random character preserves the recovered roll order");
        fresh.RerollRandomCharacter(creationRandom);
        check(fresh.Dexterity == 13 && fresh.Strength == 13 && fresh.Intelligence == 11
            && fresh.SpellPoints == 14 && creationRandom.State == 2652862833,
            "Reroll continues the RNG stream instead of restarting it");

        var defaultContext = new Player { CurrentMapId = 57, AlternateMapSet = true };
        defaultContext.Inventory[0] = 5;
        var contextRandom = new MsvcRandom(1);
        check(defaultContext.UseInventoryItem(0, contextRandom).Message == "You are already on the surface"
            && contextRandom.State == 1 && defaultContext.Inventory[0] == 5,
            "Item context comes from the call arguments, matching C++ defaults");
        var completed = new Player { Level = 100, Experience = int.MaxValue };
        var completedRandom = new MsvcRandom(1);
        check(!completed.TryLevelUp(completedRandom).LeveledUp && completedRandom.State == 1,
            "The level cap consumes no randomness");
        return records;
    }

    // Snapshot follows player.hpp scalar declaration order, then array declaration order.
    // All scalar values, every array element and MaximumStat participate in comparisons.
    internal static void State(TextWriter writer, Player p)
    {
        writer.Write('|');
        writer.Write(p.Name);
        void V(int value) { writer.Write(','); writer.Write(value); }
        V(p.Dexterity);
        V(p.Intelligence);
        V(p.Strength);
        V(p.Health);
        V(p.MaximumHealth);
        V(p.SpellPoints);
        V(p.MaximumSpellPoints);
        V(p.AbilityPoints);
        V(p.MaximumAbilityPoints);
        V(p.Level);
        V(p.Experience);
        V(p.Gold);
        V(p.WeaponRank);
        V(p.ArmorRank);
        V(p.BootsRank);
        V(p.GlovesRank);
        V(p.HelmetRank);
        V(p.RingRank);
        V(p.NecklaceRank);
        V(p.Difficulty);
        V(p.CombatStyle);
        V(p.Specialty);
        V(p.SillyMode ? 1 : 0);
        V(p.EquipmentUpgradeCount);
        V(p.LootRollPenalty);
        V(p.RestorationUses);
        V(p.DeathCount);
        V(p.QuestScore);
        V(p.QuestProgressTotal);
        V(p.GatekeeperState);
        V(p.StampsFound);
        V(p.PrincessesFound);
        V(p.PrincessMysteryUses);
        V(p.MiscellaneousShopStage);
        V(p.Value06a4);
        V(p.HasLuckyRabbitFoot ? 1 : 0);
        V(p.HasWaterproofShoeSpray ? 1 : 0);
        V(p.HasRainbowKey ? 1 : 0);
        V(p.HasUnderworldKey ? 1 : 0);
        V(p.HasTreeClimbingShoes ? 1 : 0);
        V((int)p.SpecialAbility);
        V(p.SpecialAbilityDuration);
        V(p.EnrageBaseDexterity);
        V(p.EnrageBaseIntelligence);
        V(p.EnrageBaseStrength);
        V(p.MonsterDefeatHistoryComplete ? 1 : 0);
        V(p.AlternateMapSet ? 1 : 0);
        V(p.HasBonusHealingRing ? 1 : 0);
        V(p.CurrentMapId);
        V(p.StatCapMode);
        V(p.SurfaceReturnMapId);
        V(p.SurfaceReturnRegionX);
        V(p.SurfaceReturnRegionY);
        V(p.RandomEncounterThreshold);
        V(p.SavedRandomEncounterThreshold);
        V(p.MonsterRepellentSteps);
        V(p.PendingRelocation);
        V(p.EnteredHellThroughSecretPassage ? 1 : 0);
        V((int)p.HellGatekeeperState);
        V(p.BridgeTreasureAvailable ? 1 : 0);
        V(p.HealingWaterAvailable ? 1 : 0);
        V(p.CaveCoinsCollected ? 1 : 0);
        V(p.SnowSearchCount);
        V(p.HasDefeatedWormoid ? 1 : 0);
        foreach (var value in p.Inventory) V(value);
        foreach (var value in p.InventoryValues) V(value);
        foreach (var value in p.QuestFlags) V(value ? 1 : 0);
        foreach (var value in p.OpenedChests) V(value);
        foreach (var value in p.Spells) V(value);
        foreach (var value in p.EncounteredMonsterTypes) V(value ? 1 : 0);
        foreach (var value in p.MonsterDefeats) V(value);
        V(p.MaximumStat);
        writer.Write('\n');
    }
}
