namespace Yipe.Core;

public static class Combat
{
    public static CombatEncounter StartMonsterEncounter(MonsterPlacement placement, Player player,
        MsvcRandom random, int mapEnvironment = -1)
    {
        var encounter = StartMonsterEncounter(placement.TypeId, placement.EncounterSize, random, new()
        {
            PlayerLevel = player.Level,
            Difficulty = player.Difficulty,
            CompanionType1 = placement.CompanionType1,
            CompanionType2 = placement.CompanionType2,
            GatekeeperState = player.GatekeeperState,
            MapEnvironment = mapEnvironment,
        });
        foreach (var monster in encounter.Monsters)
            if (monster.TypeId >= 0 && monster.TypeId < player.EncounteredMonsterTypes.Length)
                player.EncounteredMonsterTypes[monster.TypeId] = true;
        return encounter;
    }

    public static int PlayerAttackEquipmentRating(Player player)
    {
        int value = player.WeaponRank;
        if (player.HelmetRank == 5)
        {
            ++value;
        }
        else if (player.HelmetRank == 6)
        {
            value += 2;
        }
        else if (player.HelmetRank == 7)
        {
            value += 4;
        }
        if (unchecked((short)player.GlovesRank) > 3)
        {
            ++value;
        }
        if (player.NecklaceRank == 5)
        {
            ++value;
        }
        else if (player.NecklaceRank == 6)
        {
            value += 2;
        }
        return value;
    }

    public static int PlayerDefenseEquipmentRating(Player player)
    {
        int value = player.ArmorRank + unchecked((short)player.BootsRank)
            + unchecked((short)player.GlovesRank)
            + unchecked((short)player.HelmetRank)
            + unchecked((short)player.RingRank)
            + unchecked((short)player.NecklaceRank);
        if (player.RingRank == 5)
        {
            ++value;
        }
        if (player.BootsRank == 6)
        {
            ++value;
        }
        return value;
    }

    private static int Roll(MsvcRandom random, int bound)
    {
        return random.Next() % Math.Max(bound, 1);
    }

    private static int MonsterAttackBonus(Player player, Monster monster,
                             MsvcRandom random)
    {
        int value = 0;
        if (monster.TypeId == 9 || monster.TypeId == 0x0f || monster.TypeId == 0x11
            || monster.TypeId == 0x29)
        {
            value = Roll(random, player.Level) + 3;
        }
        if (monster.TypeId > 0x22)
        {
            value += 10 + player.Difficulty;
        }
        if (monster.TypeId < 4 && player.Level > 8)
        {
            value += 5 + player.Level;
        }
        if (player.Level > 10)
        {
            value += 2;
        }
        if (player.Level > 20)
        {
            value += 3;
        }
        if (player.Level > 30)
        {
            value += 4;
        }
        if (player.Level > 40)
        {
            value += 5;
        }
        if (monster.GatekeeperEmpowered)
        {
            value += 10 + Roll(random, 10) + player.Difficulty;
        }
        if (player.Difficulty == 3)
        {
            ++value;
        }
        return value;
    }

    private static int CalculatePlayerDamage(Player player, Monster monster,
                                MsvcRandom random)
    {
        var (minimumWeaponDamage, maximumWeaponDamage) =
            CombatCatalog.WeaponDamageRange(player.WeaponRank);

        int strengthRoll = Roll(random, player.Strength);
        int weaponRankRoll = Roll(random, player.WeaponRank + 1);
        int levelRoll = Roll(random, Math.Min(player.Level, 40));
        int situationalHitBonus = player.Difficulty == 0 ? 0 : 2;
        if (monster.TypeId == 4 && player.Level < 5)
        {
            situationalHitBonus += 10 - player.Level;
        }
        if (player.SpecialAbility == SpecialAbility.Invisibility
            && player.SpecialAbilityDuration > 0)
        {
            situationalHitBonus += 1 + Roll(random, player.SpecialAbilityDuration);
        }
        if (player.HasLuckyRabbitFoot)
        {
            situationalHitBonus += 1 + Roll(random, 6);
        }
        int situationalRoll = Roll(random, situationalHitBonus + 1);
        int equipmentRoll = Roll(random, PlayerAttackEquipmentRating(player) + 1);

        int monsterSpecialDefense = 0;
        if (player.Difficulty == 3 && (monster.TypeId == 0x0f || monster.TypeId == 0x13))
        {
            monsterSpecialDefense = 4;
        }
        if (monster.GatekeeperEmpowered)
        {
            monsterSpecialDefense += player.Difficulty + 5 + Roll(random, 20);
        }
        int specialDefenseRoll = Roll(random, monsterSpecialDefense + 1);

        int difficultyBase = monster.TypeId;
        if (player.Difficulty == 1)
        {
            difficultyBase = 5;
        }
        else if (player.Difficulty == 2)
        {
            difficultyBase = 3;
        }
        else if (player.Difficulty == 3)
        {
            difficultyBase = 1;
        }

        int hitScore = difficultyBase - specialDefenseRoll + equipmentRoll
            + situationalRoll + levelRoll + 1 + weaponRankRoll + strengthRoll;
        hitScore -= Roll(random, monster.Rank);
        hitScore -= Roll(random, monster.DefenseRating);
        hitScore -= Roll(random, monster.DefenseRating);
        hitScore -= Roll(random, Math.Max(monster.TypeId - player.Level, 1));
        hitScore -= Roll(random, monster.AttackRating);
        if (player.SpecialAbility == SpecialAbility.Enrage
            && player.SpecialAbilityDuration > 0)
        {
            hitScore += 1 + Roll(random, 3);
        }

        int forcedHitRoll = Roll(random, 100);
        int cappedLevel = Math.Min(player.Level, 40);
        if (hitScore < 1 && forcedHitRoll + cappedLevel / 2 > 91)
        {
            hitScore = 1;
        }
        else if (hitScore < 1)
        {
            return 0;
        }

        int damage = Roll(random, maximumWeaponDamage - minimumWeaponDamage + 1)
            + minimumWeaponDamage + (player.Strength > 19 ? 1 : 0);
        if (hitScore > 15)
        {
            damage += Roll(random, cappedLevel / 2);
        }
        if (player.Difficulty == 1)
        {
            ++damage;
        }
        if (player.Specialty == 2 && player.Level < 10 && monster.TypeId > 2
            && Roll(random, 100) > 65)
        {
            ++damage;
        }
        if (player.SpecialAbility == SpecialAbility.Enrage
            && player.SpecialAbilityDuration > 0)
        {
            damage += 2 + Roll(random, 3);
        }
        return damage;
    }

    private static (int, int) MonsterDamageRange(int typeId)
    {
        var definition = CombatCatalog.Monster(typeId);
        return (definition.DamageMinimum, definition.DamageMaximum);
    }

    private static int LowLevelDefenseRange(int level)
    {
        if (level <= 4)
        {
            return 3;
        }
        if (level <= 9)
        {
            return 6;
        }
        if (level <= 14)
        {
            return 10;
        }
        if (level <= 19)
        {
            return 15;
        }
        if (level <= 27)
        {
            return 20;
        }
        return level <= 37 ? 28 : 35;
    }

    private static void ClearCombatStatus(Player player)
    {
        if (player.SpecialAbility == SpecialAbility.Enrage)
        {
            if (player.EnrageBaseDexterity > 0)
            {
                player.Dexterity = player.EnrageBaseDexterity;
            }
            if (player.EnrageBaseIntelligence > 0)
            {
                player.Intelligence = player.EnrageBaseIntelligence;
            }
            if (player.EnrageBaseStrength > 0)
            {
                player.Strength = player.EnrageBaseStrength;
            }
            player.EnrageBaseDexterity = 0;
            player.EnrageBaseIntelligence = 0;
            player.EnrageBaseStrength = 0;
        }
        player.SpecialAbilityDuration = 0;
    }

    private static int CalculateMonsterDamage(Player player, Monster monster,
                                 MsvcRandom random)
    {
        int armorRoll = Roll(random, player.ArmorRank * 2);
        int dexterityRoll = Roll(random, player.Dexterity);
        int levelDefenseRoll = Roll(random, LowLevelDefenseRange(player.Level));
        int typeRoll = Roll(random, monster.TypeId);
        int firstAttackRoll = Roll(random, monster.AttackRating) + player.Difficulty;
        int secondAttackRoll = Roll(random, monster.AttackRating);
        int specialRoll = Roll(random, MonsterAttackBonus(player, monster, random) + 2);
        int equipmentRoll = Roll(random, PlayerDefenseEquipmentRating(player) + 1);

        int attackScore = firstAttackRoll + secondAttackRoll + typeRoll + specialRoll
            - armorRoll - dexterityRoll - levelDefenseRoll - equipmentRoll;
        int forcedHitThreshold = 92;
        if (player.Level > 5)
        {
            forcedHitThreshold = 87;
        }
        if (player.Level > 12)
        {
            forcedHitThreshold = 75;
        }
        if (player.Level > 23)
        {
            forcedHitThreshold = 60;
        }
        int forcedHitRoll = Roll(random, 100);
        bool missed = attackScore < 1
            && forcedHitThreshold - monster.TypeId / 2 >= forcedHitRoll;
        int damage = 0;
        if (!missed)
        {
            var (minimumDamage, maximumDamage) = MonsterDamageRange(monster.TypeId);
            damage = Roll(random, maximumDamage - minimumDamage + 1) + minimumDamage;
            damage += Roll(random, monster.Rank + 1);
            if (monster.GatekeeperEmpowered)
            {
                damage += 5 + Roll(random, 5);
            }
            if (attackScore > 15)
            {
                damage += Roll(random, monster.Rank);
            }
            if (player.Difficulty == 3)
            {
                ++damage;
            }
            else if (player.Difficulty == 1 && damage > 2)
            {
                --damage;
            }
            if ((monster.TypeId == 7 || monster.TypeId == 13 || monster.TypeId == 15)
                && player.Level > 9)
            {
                damage += player.Difficulty;
            }
            if (monster.TypeId > 34)
            {
                damage += player.Difficulty;
            }
            if (damage > 3 && player.Level < 3)
            {
                --damage;
            }
            if (damage > 2 && player.Level == 1 && player.Health < 6)
            {
                damage = 1;
            }
            else if (damage > 2 && player.Level < 4 && player.Health < 6)
            {
                --damage;
            }
            if (damage > 10 && PlayerDefenseEquipmentRating(player) > 10)
            {
                damage -= 1 + Roll(random, 6);
            }
            if (damage > 20 && PlayerDefenseEquipmentRating(player) > 20)
            {
                damage -= 2 + Roll(random, 6);
            }
            if (damage > 28 && PlayerDefenseEquipmentRating(player) > 28)
            {
                damage -= 3 + Roll(random, 6);
            }
            if (damage > 10 && player.Health < 35)
            {
                damage -= 3;
            }
            if (damage > 20 && player.Health < 50)
            {
                damage -= 5;
            }
            if (player.Level == 1)
            {
                damage = Math.Min(damage, 5);
            }
            damage = Math.Max(damage, 0);
        }

        if (player.SpecialAbility == SpecialAbility.Invisibility
            && player.SpecialAbilityDuration > 0)
        {
            damage = 0;
            --player.SpecialAbilityDuration;
        }
        else if (player.SpecialAbility == SpecialAbility.Enrage
                   && player.SpecialAbilityDuration > 0)
        {
            if (damage > 2)
            {
                --damage;
            }
            --player.SpecialAbilityDuration;
            if (player.SpecialAbilityDuration < 1)
            {
                ClearCombatStatus(player);
            }
        }
        return damage;
    }

    private static int BackstabDamage(Player player, Monster monster,
                        MsvcRandom random)
    {
        int dexterityRange = 2;
        if (player.Dexterity > 20)
        {
            dexterityRange = 6;
        }
        if (player.Dexterity > 30)
        {
            dexterityRange += 2;
        }
        if (player.Dexterity > 40)
        {
            dexterityRange += 5;
        }

        int healthRange = Math.Max(monster.Health / 5, 1);
        int damage = Roll(random, healthRange) + 3 + Roll(random, dexterityRange);
        damage = Math.Max(damage, 6);
        if (monster.Health > 45)
        {
            damage = Math.Max(damage, 10);
        }
        if (monster.Health > 95)
        {
            damage = Math.Max(damage, 15);
        }
        return damage;
    }

    private static void StartEnrage(Player player, MsvcRandom random)
    {
        player.EnrageBaseDexterity = player.Dexterity;
        player.EnrageBaseIntelligence = player.Intelligence;
        player.EnrageBaseStrength = player.Strength;
        player.Dexterity += 1 + Roll(random, player.Dexterity / 2);
        player.Strength += 2 + Roll(random, player.Strength / 2);
        player.Intelligence += 1 + Roll(random, player.Intelligence / 2);
    }

    private static EquipmentUpgrade UpgradeEquipment(Player player, int choice)
    {
        switch (choice)
        {
            case 0:
            case 7:
                if (player.HelmetRank < 6)
                {
                    player.QuestScore += player.HelmetRank * 50;
                    ++player.HelmetRank;
                    return EquipmentUpgrade.Helmet;
                }
                break;
            case 1:
                {
                    int maximum = player.StatCapMode == 2 ? 29 : 23;
                    if (player.WeaponRank < maximum)
                    {
                        player.QuestScore += Shops.EquipmentPrice(EquipmentKind.Weapon, player.WeaponRank) / 5;
                        ++player.WeaponRank;
                        return EquipmentUpgrade.Weapon;
                    }
                    break;
                }
            case 2:
                if (player.ArmorRank <= 25)
                {
                    player.QuestScore += Shops.EquipmentPrice(EquipmentKind.Armor, player.ArmorRank) / 5;
                    ++player.ArmorRank;
                    return EquipmentUpgrade.Armor;
                }
                break;
            case 3:
            case 10:
                if (player.RingRank <= 4)
                {
                    player.QuestScore += player.RingRank * 50;
                    ++player.RingRank;
                    return EquipmentUpgrade.Ring;
                }
                break;
            case 4:
                if (player.NecklaceRank <= 5)
                {
                    player.QuestScore += player.NecklaceRank * (player.NecklaceRank > 3 ? 95 : 20);
                    ++player.NecklaceRank;
                    return EquipmentUpgrade.Necklace;
                }
                break;
            case 5:
            case 8:
                if (player.GlovesRank <= 4)
                {
                    player.QuestScore += player.GlovesRank * 30;
                    ++player.GlovesRank;
                    return EquipmentUpgrade.Gloves;
                }
                break;
            case 6:
            case 9:
                if (player.BootsRank <= 4)
                {
                    player.QuestScore += player.BootsRank * 50;
                    ++player.BootsRank;
                    return EquipmentUpgrade.Boots;
                }
                break;
            default:
                break;
        }
        return EquipmentUpgrade.None;
    }

    private static CombatReward RollMonsterReward(Player player, Monster monster,
                                     int primaryTypeId, int selectedSlot,
                                     MsvcRandom random)
    {
        int experience = Math.Max(
            (monster.Rank - player.Level + 1 + monster.TypeId) * 10, 5);
        int gold = experience + (monster.TypeId < 6 ? 1 : monster.TypeId * 3 / 2)
            - Roll(random, player.Level) + Roll(random, monster.Rank);
        gold = Math.Max(gold, monster.Rank * 9);
        if (monster.TypeId > 15)
        {
            gold += monster.Rank;
        }
        if (monster.TypeId < 10)
        {
            gold += Roll(random, monster.TypeId);
        }
        else
        {
            gold += 1 + Roll(random, monster.TypeId) + monster.TypeId;
        }
        if (monster.TypeId > 7 && experience < 20)
        {
            experience = Roll(random, monster.TypeId) + monster.TypeId * 3;
        }

        int slotBonus = primaryTypeId * selectedSlot + 1;
        experience += slotBonus;
        gold += Roll(random, slotBonus);
        return new(experience, gold);
    }

    private static int EncounterSize(CombatEncounter encounter)
    {
        return encounter.Monsters.Count(monster => monster.TypeId > 0);
    }

    private static CombatReward FinishEncounterRewards(Player player,
                                              CombatEncounter encounter,
                                              MsvcRandom random)
    {
        player.Experience += encounter.PendingReward.Experience;
        player.Gold += encounter.PendingReward.Gold;

        CombatReward reward = encounter.PendingReward;
        int primaryTypeId = encounter.Monsters[0].TypeId;
        // 0x00405210 awards the first key before the caller's quest-state check,
        // and both this reward and a matching stamp replace the random item drop.
        if (primaryTypeId == 41 && !player.HasUnderworldKey)
        {
            player.HasUnderworldKey = true;
            reward.UnderworldKey = true;
            return reward;
        }
        if (primaryTypeId >= 35 && primaryTypeId <= 40
            && primaryTypeId == encounter.MapEnvironment + 34)
        {
            return reward;
        }
        int opponents = EncounterSize(encounter);
        int opponentPenalty = opponents == 1 ? 1 : opponents == 2 ? 3 : 6;
        int dropBase = primaryTypeId < 35 || primaryTypeId == 41 ? 91 : 90;
        int dropThreshold =
            player.Level - primaryTypeId / 4 - primaryTypeId
            + dropBase - opponentPenalty;
        reward.EquipmentUpgrade = RollTreasure(player, dropThreshold, 0, random).EquipmentUpgrade;
        return reward;
    }

    private static Monster InitializeMonster(int typeId, MsvcRandom random)
    {
        var definition = CombatCatalog.Monster(typeId);
        var monster = new Monster { TypeId = definition.TypeId, Name = definition.Name, BitmapResourceId = definition.BitmapResourceId };
        monster.Health = definition.HealthBase
            + Roll(random, definition.RankAndHealthRange);
        monster.MaximumHealth = monster.Health;
        monster.SpecialRating = definition.SpecialRating;
        monster.Rank = definition.RankAndHealthRange;
        monster.AttackRating = definition.AttackBase + Roll(random, 2);
        monster.DefenseRating = definition.DefenseBase + Roll(random, 2);
        return monster;
    }

    private static bool HasLivingMonster(CombatEncounter encounter)
    {
        return encounter.Monsters.Any(monster => monster.Health > 0);
    }

    private static int FirstLivingMonster(CombatEncounter encounter)
    {
        for (int slot = 0; slot < encounter.Monsters.Length; ++slot)
        {
            if (encounter.Monsters[slot].Health > 0)
            {
                return slot;
            }
        }
        return 0;
    }

    private static CombatTurn ResolveCounterattacks(Player player, CombatEncounter encounter,
                                      MsvcRandom random)
    {
        var result = new CombatTurn();
        for (int slot = 0; slot < encounter.Monsters.Length; ++slot)
        {
            Monster monster = encounter.Monsters[slot];
            if (monster.Health < 1)
            {
                continue;
            }
            int damage = CalculateMonsterDamage(player, monster, random);
            if (slot == 2)
            {
                damage /= 2;
            }
            result.MonsterDamageBySlot[slot] = damage;
            result.MonsterDamage += damage;
            player.AdjustHealth(-damage);
        }
        if (player.Health < 1)
        {
            result.Outcome = CombatOutcome.Defeat;
            ClearCombatStatus(player);
        }
        return result;
    }

    private static void FinishEncounterVictory(Player player, CombatEncounter encounter,
                                  MsvcRandom random, CombatTurn result)
    {
        int primaryType = encounter.Monsters[0].TypeId;
        if (primaryType == 40 || primaryType == 41)
        {
            player.RecordMonsterDefeat(primaryType);
        }
        result.Outcome = CombatOutcome.Victory;
        result.Reward = FinishEncounterRewards(player, encounter, random);
        result.LevelUp = player.TryLevelUp(random);
        if (primaryType >= 35 && primaryType != 41
            && primaryType == encounter.MapEnvironment + 34)
        {
            player.StampsFound = unchecked((ushort)(player.StampsFound + 1));
        }
        if (primaryType == 40 && encounter.MapEnvironment == 6)
        {
            player.GatekeeperState = 1;
        }
        else if (primaryType == 41 && player.GatekeeperState == 1
                   && player.HasUnderworldKey)
        {
            player.GatekeeperState = 2;
        }
        ClearCombatStatus(player);
    }

    private static void FinishDefeatedTarget(Player player, CombatEncounter encounter,
                                int selectedSlot, MsvcRandom random,
                                CombatTurn result)
    {
        Monster target = encounter.Monsters[selectedSlot];
        CombatReward reward = RollMonsterReward(
            player, target, encounter.Monsters[0].TypeId, selectedSlot, random);
        encounter.PendingReward.Experience += reward.Experience;
        encounter.PendingReward.Gold += reward.Gold;
        if (target.TypeId != 40 && target.TypeId != 41)
        {
            player.RecordMonsterDefeat(target.TypeId);
        }
        if (!HasLivingMonster(encounter))
        {
            FinishEncounterVictory(player, encounter, random, result);
        }
        else
        {
            encounter.SelectedTarget = FirstLivingMonster(encounter);
        }
    }

    private static int LivingMonsterCount(CombatEncounter encounter)
    {
        return encounter.Monsters.Count(monster => monster.Health > 0);
    }

    private static int HealPlayer(Player player, int amount)
    {
        int actual = Math.Min(amount, player.MaximumHealth - player.Health);
        player.AdjustHealth(actual);
        return actual;
    }

    private static void ShuffleMonsterHealth(CombatEncounter encounter, MsvcRandom random)
    {
        var portions = new int[3];
        var livingSlots = new int[3];
        int count = 0;
        for (int slot = 0; slot < encounter.Monsters.Length; ++slot)
        {
            int health = encounter.Monsters[slot].Health;
            if (health < 1)
            {
                continue;
            }
            livingSlots[count++] = slot;
            int limit = slot == 0 ? 50 : 55;
            int portion = Roll(random, Math.Min(health, limit)) + 5;
            if (portion >= health)
            {
                portion = health - 1;
            }
            portions[slot] = portion;
        }

        if (count == 2)
        {
            int first = livingSlots[0];
            int second = livingSlots[1];
            encounter.Monsters[first].Health +=
                portions[second]
                - portions[first];
            encounter.Monsters[second].Health +=
                portions[first]
                - portions[second];
        }
        else if (count == 3)
        {
            int[] oldHealth = [
                encounter.Monsters[0].Health,
                encounter.Monsters[1].Health,
                encounter.Monsters[2].Health,
            ];
            encounter.Monsters[0].Health = oldHealth[0] - portions[0] + portions[1];
            encounter.Monsters[1].Health = oldHealth[1] - portions[1] + portions[2];
            encounter.Monsters[2].Health = oldHealth[2] - portions[2] + portions[0];
        }
    }

    private static void ReplaceMonsterWithRabbit(CombatEncounter encounter)
    {
        int slot = encounter.Monsters[2].Health > 0 ? 2 : 1;
        Monster monster = encounter.Monsters[slot];
        var rabbit = CombatCatalog.Monster(1);
        monster.TypeId = rabbit.TypeId;
        monster.Name = rabbit.Name;
        monster.BitmapResourceId = rabbit.BitmapResourceId;
        monster.SpecialRating = 5;
        monster.AttackRating = 5;
        monster.DefenseRating = 5;
        monster.GatekeeperEmpowered = false;
    }

    public static LootReward RollTreasure(Player player, int dropThreshold, int chestKind,
                             MsvcRandom random)
    {
        if (chestKind < 0 || chestKind > 4)
        {
            throw new InvalidOperationException("unknown chest kind");
        }
        bool forcedUpgrade = false;
        if (player.Level >= 15 && player.WeaponRank <= 12 && player.EquipmentUpgradeCount == 0)
        {
            forcedUpgrade = true;
            dropThreshold = -10;
            player.EquipmentUpgradeCount = 1;
        }
        else if (player.Level > 24 && player.WeaponRank < 15
                   && player.EquipmentUpgradeCount < 2)
        {
            forcedUpgrade = true;
            dropThreshold = -10;
            ++player.EquipmentUpgradeCount;
        }

        var result = new LootReward();
        if (player.CurrentMapId == 0x63 && chestKind == 2)
        {
            player.HasWaterproofShoeSpray = true;
            result.WaterproofShoeSpray = true;
            return result;
        }
        if (dropThreshold < Roll(random, 101) - player.LootRollPenalty * 2)
        {
            result.EquipmentUpgrade = UpgradeEquipment(
                player, forcedUpgrade ? 1 : Roll(random, 11));
            if (result.EquipmentUpgrade != EquipmentUpgrade.None) return result;
        }
        else if (forcedUpgrade)
        {
            // The original keeps its upgrade marker even if the penalty defeats
            // the forced threshold, suppressing the consumable fallback.
            return result;
        }
        if (chestKind == 0) return result;

        var slot = Array.IndexOf(player.Inventory, (ushort)0, 0, Player.CarriedInventorySize);
        if (slot < 0)
        {
            result.InventoryFull = true;
            return result;
        }
        int choice = chestKind == 1 ? Roll(random, 7)
            : chestKind == 2 ? Roll(random, 7) + 4
            : chestKind == 3 ? Roll(random, 6) + 11 : Roll(random, 6) + 14;
        switch (choice)
        {
            case 4: case 5: case 6: result.ItemId = 2; break;
            case 7: case 8: result.ItemId = 5; break;
            case 9: case 10: result.ItemId = 4; break;
            case 11: case 14: result.ItemId = player.Strength < 50 ? (ushort)6 : (ushort)2; break;
            case 12: case 15: result.ItemId = player.Dexterity < 50 ? (ushort)7 : (ushort)2; break;
            case 13: case 16: result.ItemId = player.Intelligence < 50 ? (ushort)8 : (ushort)2; break;
            case 17: result.ItemId = 9; break;
            case 18: result.ItemId = 10; break;
            case 19: result.ItemId = 3; break;
            default: result.ItemId = player.Level > 10 ? (ushort)2 : (ushort)1; break;
        }
        player.Inventory[slot] = result.ItemId;
        return result;
    }

    public static CombatEncounter StartMonsterEncounter(int typeId, int opponentCount,
                                            MsvcRandom random,
                                            EncounterSetup? setup = null)
    {
        setup ??= new();
        if (opponentCount < 0
            || opponentCount > CombatEncounter.MaximumOpponents)
        {
            throw new InvalidOperationException("opponent count must be between zero and three");
        }

        bool generatedCount = opponentCount == 0;
        if (generatedCount)
        {
            if (typeId + 3 < setup.PlayerLevel || Roll(random, 100) > 80)
            {
                opponentCount = Roll(random, 3) + 1;
            }
            if (opponentCount == 0 && setup.PlayerLevel < 3)
            {
                opponentCount = 1;
            }
        }

        var encounter = new CombatEncounter();
        encounter.MapEnvironment = setup.MapEnvironment;
        encounter.Monsters[0] = InitializeMonster(typeId, random);
        int variantRoll = Roll(random, 100);
        if (!generatedCount)
        {
            variantRoll = 0;
        }

        if (opponentCount == 2)
        {
            int secondTypeId = typeId;
            if (variantRoll >= 86 && typeId <= 40)
            {
                secondTypeId = typeId + 1;
            }
            else if (variantRoll > 80 && typeId > 2)
            {
                secondTypeId = typeId - 1;
            }
            encounter.Monsters[1] = InitializeMonster(secondTypeId, random);
        }
        else if (opponentCount == 3)
        {
            encounter.Monsters[1] = InitializeMonster(typeId, random);
            int thirdTypeId = typeId;
            if (variantRoll >= 66 && typeId <= 40)
            {
                thirdTypeId = typeId + 1;
            }
            else if (variantRoll >= 56 && typeId >= 3)
            {
                thirdTypeId = typeId - 1;
            }
            encounter.Monsters[2] = InitializeMonster(thirdTypeId, random);
        }

        if (typeId > 34)
        {
            int companionType1 = setup.CompanionType1;
            int companionType2 = setup.CompanionType2;
            bool replaceCompanions = companionType1 != 0 || companionType2 != 0;
            if (typeId == 41 && setup.GatekeeperState != 0)
            {
                encounter.Monsters[0].GatekeeperEmpowered = true;
                encounter.Monsters[0].Health = encounter.Monsters[0].Health * 4
                    + setup.PlayerLevel * 6;
                companionType1 = setup.Difficulty + 37;
                companionType2 = 37;
                replaceCompanions = true;
            }

            if (replaceCompanions)
            {
                encounter.Monsters[1] = companionType1 > 0
                    ? InitializeMonster(companionType1, random) : new Monster();
                encounter.Monsters[2] = companionType2 > 0
                    ? InitializeMonster(companionType2, random) : new Monster();
            }
            encounter.Monsters[0].Health += setup.PlayerLevel;
            if (setup.Difficulty == 3)
            {
                encounter.Monsters[0].Health += setup.PlayerLevel;
            }
            encounter.Monsters[0].MaximumHealth = encounter.Monsters[0].Health;
        }
        return encounter;
    }

    public static CombatTurn ResolveAttackTurn(Player player, CombatEncounter encounter,
                                   MsvcRandom random)
    {
        if (player.Health < 1 || encounter.SelectedTarget < 0
            || encounter.SelectedTarget
                >= CombatEncounter.MaximumOpponents
            || encounter.Monsters[encounter.SelectedTarget].Health < 1)
        {
            throw new InvalidOperationException("cannot attack after combat has ended");
        }

        var result = new CombatTurn();
        Monster target =
            encounter.Monsters[encounter.SelectedTarget];
        result.PlayerDamage = CalculatePlayerDamage(player, target, random);
        target.Health -= result.PlayerDamage;
        bool targetDefeated = target.Health < 1;
        int selectedSlot = encounter.SelectedTarget;

        if (HasLivingMonster(encounter))
        {
            CombatTurn counterattacks = ResolveCounterattacks(player, encounter, random);
            result.MonsterDamage = counterattacks.MonsterDamage;
            result.MonsterDamageBySlot = counterattacks.MonsterDamageBySlot;
            if (counterattacks.Outcome == CombatOutcome.Defeat)
            {
                result.Outcome = CombatOutcome.Defeat;
                return result;
            }
        }

        if (targetDefeated)
        {
            FinishDefeatedTarget(player, encounter, selectedSlot, random, result);
        }
        return result;
    }

    public static CombatTurn ResolveSpecialAbilityTurn(Player player, CombatEncounter encounter,
                                             MsvcRandom random)
    {
        if (player.Health < 1 || encounter.SelectedTarget < 0
            || encounter.SelectedTarget
                >= CombatEncounter.MaximumOpponents
            || encounter.Monsters[encounter.SelectedTarget].Health < 1)
        {
            throw new InvalidOperationException("cannot use an ability after combat has ended");
        }

        var result = new CombatTurn();
        if (player.SpecialAbility == SpecialAbility.None || player.AbilityPoints < 3)
        {
            return result;
        }

        int selectedSlot = encounter.SelectedTarget;
        Monster target = encounter.Monsters[selectedSlot];
        switch (player.SpecialAbility)
        {
            case SpecialAbility.Backstab:
                result.PlayerDamage = BackstabDamage(player, target, random);
                player.AbilityPoints -= 3;
                target.Health -= result.PlayerDamage;
                if (target.Health < 1)
                {
                    FinishDefeatedTarget(player, encounter, selectedSlot, random, result);
                }
                break;
            case SpecialAbility.Frighten:
                {
                    Monster third = encounter.Monsters[2];
                    if (third.Health < 1)
                    {
                        return result;
                    }
                    encounter.PendingReward.Experience += third.AttackRating + 2;
                    player.AbilityPoints -= 3;
                    third.TypeId = 0;
                    third.Health = 0;
                    if (selectedSlot == 2)
                    {
                        if (HasLivingMonster(encounter))
                        {
                            encounter.SelectedTarget = FirstLivingMonster(encounter);
                        }
                        else
                        {
                            FinishEncounterVictory(player, encounter, random, result);
                        }
                    }
                    break;
                }
            case SpecialAbility.Invisibility:
                player.SpecialAbilityDuration = 3 + Roll(random, player.Dexterity / 5);
                if (encounter.Monsters[1].Health > 0)
                {
                    player.SpecialAbilityDuration *= 2;
                }
                if (encounter.Monsters[2].Health > 0)
                {
                    player.SpecialAbilityDuration *= 4;
                }
                player.AbilityPoints -= 3;
                break;
            case SpecialAbility.Enrage:
                player.SpecialAbilityDuration = 3 + Roll(random, player.Strength / 4);
                if (encounter.Monsters[1].Health > 0)
                {
                    player.SpecialAbilityDuration *= 2;
                }
                if (encounter.Monsters[2].Health > 0)
                {
                    player.SpecialAbilityDuration += 6;
                }
                StartEnrage(player, random);
                player.AbilityPoints -= 3;
                break;
            case SpecialAbility.None:
                break;
        }
        return result;
    }

    public static CombatTurn ResolveMonsterTurn(Player player, CombatEncounter encounter,
                                    MsvcRandom random)
    {
        if (player.Health < 1 || !HasLivingMonster(encounter))
        {
            throw new InvalidOperationException("cannot continue a combat turn after combat has ended");
        }
        return ResolveCounterattacks(player, encounter, random);
    }

    public static FleeResult TryFlee(Player player, CombatEncounter encounter,
                        MsvcRandom random)
    {
        if (player.Health < 1 || !HasLivingMonster(encounter))
        {
            throw new InvalidOperationException("cannot flee after combat has ended");
        }

        Monster primaryMonster = encounter.Monsters[0];
        int cappedLevel = Math.Min(player.Level, 35);
        int chance = cappedLevel - primaryMonster.TypeId + 68;
        if (player.Difficulty == 1)
        {
            chance = cappedLevel - primaryMonster.TypeId + 86;
        }
        else if (player.Difficulty == 3)
        {
            chance = cappedLevel - primaryMonster.TypeId + 63;
        }
        if (cappedLevel < 4)
        {
            chance += 50;
        }
        if (player.Health < 10)
        {
            chance += 5;
        }
        if (Roll(random, 100) <= chance)
        {
            player.Gold = Math.Max(player.Gold + 1 - primaryMonster.TypeId, 0);
            ClearCombatStatus(player);
            return new() { Escaped = true };
        }

        var result = new FleeResult();
        for (int slot = 0; slot < encounter.Monsters.Length; ++slot)
        {
            Monster monster = encounter.Monsters[slot];
            if (monster.Health < 1)
            {
                continue;
            }
            int damage = CalculateMonsterDamage(player, monster, random);
            if (damage == 0)
            {
                continue;
            }
            if (slot == 2)
            {
                damage /= 2;
            }
            if (player.Level < 6 && player.Health < 7)
            {
                damage = Math.Max(damage - 2, 1);
            }
            if (player.Health - damage < 0)
            {
                damage /= 2;
            }
            if (damage > 10)
            {
                damage -= 3;
            }
            damage = Math.Max(damage, 1);
            result.MonsterDamageBySlot[slot] = damage;
            result.MonsterDamage += damage;
            player.AdjustHealth(-damage);
        }
        result.PlayerDefeated = player.Health < 1;
        if (result.PlayerDefeated)
        {
            ClearCombatStatus(player);
        }
        return result;
    }

    public static CombatSpellResult ResolveSpellTurn(Player player, CombatEncounter encounter,
                                         int spell, SpellPower power,
                                         MsvcRandom random)
    {
        var result = new CombatSpellResult();
        var definitions = Spells.Definitions;
        if (spell < 0 || spell >= definitions.Count)
        {
            return result;
        }
        if (player.Spells[spell] == 0)
        {
            result.Status = SpellCastStatus.Unlearned;
            return result;
        }
        if (definitions[spell].Usage == SpellUsage.NonCombat)
        {
            result.Status = SpellCastStatus.WrongContext;
            return result;
        }
        int cost = Spells.PointCost(player, spell, power);
        if (player.SpellPoints < cost)
        {
            result.Status = SpellCastStatus.InsufficientSpellPoints;
            return result;
        }
        if ((spell == 10 || spell == 11) && LivingMonsterCount(encounter) < 2)
        {
            return result;
        }
        if (player.Health < 1 || !HasLivingMonster(encounter)
            || encounter.SelectedTarget < 0
            || encounter.SelectedTarget
                >= CombatEncounter.MaximumOpponents
            || encounter.Monsters[encounter.SelectedTarget].Health < 1)
        {
            throw new InvalidOperationException("cannot cast a spell after combat has ended");
        }

        result.Status = SpellCastStatus.Cast;
        player.SpellPoints -= cost;
        int powerValue = (int)(power);
        int damage = 0;
        switch (spell)
        {
            case 1:
                {
                    int healing = (Roll(random, 5) + 8 + player.Intelligence / 2) * powerValue;
                    if (player.HasBonusHealingRing)
                    {
                        healing += powerValue * 6 + player.Intelligence;
                    }
                    result.Healing = HealPlayer(player, healing);
                    break;
                }
            case 2:
                damage = powerValue + player.Intelligence / 2
                    + (random.Next() & 3) + (random.Next() & 3);
                break;
            case 3:
                damage = powerValue + Roll(random, 6) + player.Intelligence + 14;
                break;
            case 5:
                result.Healing = HealPlayer(player, player.MaximumHealth - player.Health);
                break;
            case 6:
                {
                    int intelligenceRoll = random.Next();
                    int tenRoll = random.Next();
                    damage = player.Intelligence / 3 + tenRoll % 10 + player.Intelligence
                        + intelligenceRoll % Math.Max(player.Intelligence, 1) + 30
                        + powerValue * 2;
                    break;
                }
            case 7:
                damage = Roll(random, powerValue + player.Intelligence) + 20
                    + player.Intelligence / 2;
                result.Healing = HealPlayer(player, damage * powerValue);
                break;
            case 9:
                {
                    int healing = (Roll(random, 5) + 10 + player.Intelligence / 2) * powerValue
                        + player.Intelligence;
                    if (player.HasBonusHealingRing)
                    {
                        healing += powerValue * 2 + player.Intelligence;
                    }
                    result.Healing = HealPlayer(player, healing);
                    if (power == SpellPower.Low)
                    {
                        player.AbilityPoints = Math.Min(player.AbilityPoints + 1,
                                                         player.MaximumAbilityPoints);
                    }
                    else
                    {
                        player.AbilityPoints = player.MaximumAbilityPoints;
                    }
                    break;
                }
            case 10:
                ShuffleMonsterHealth(encounter, random);
                break;
            case 11:
                ReplaceMonsterWithRabbit(encounter);
                break;
            default:
                break;
        }

        if (damage > 0)
        {
            if (power == SpellPower.Low)
            {
                int slot = encounter.SelectedTarget;
                Monster target = encounter.Monsters[slot];
                int applied = damage;
                if (target.SpecialRating > 0)
                {
                    applied -= Math.Max(applied / target.SpecialRating, 1);
                }
                applied = Math.Max(applied, 1);
                result.DamageBySlot[slot] = applied;
                result.Turn.PlayerDamage += applied;
                target.Health -= applied;
                if (target.Health < 1)
                {
                    FinishDefeatedTarget(player, encounter, slot, random, result.Turn);
                }
            }
            else
            {
                int remainingDamage = damage;
                for (int slot = 0; slot < encounter.Monsters.Length; ++slot)
                {
                    Monster target = encounter.Monsters[slot];
                    if (target.Health < 1)
                    {
                        continue;
                    }
                    if (target.SpecialRating > 0)
                    {
                        int minimumReduction = slot == 1 ? 1 : 2;
                        remainingDamage -= Math.Max(
                            remainingDamage / target.SpecialRating, minimumReduction);
                    }
                    remainingDamage = Math.Max(remainingDamage, slot == 1 ? 2 : 1);
                    int applied = slot == 1
                        ? remainingDamage : remainingDamage + Roll(random, 3);
                    result.DamageBySlot[slot] = applied;
                    result.Turn.PlayerDamage += applied;
                    target.Health -= applied;
                    if (target.Health < 1)
                    {
                        FinishDefeatedTarget(player, encounter, slot,
                                               random, result.Turn);
                    }
                }
            }
        }

        if (result.Turn.Outcome != CombatOutcome.Victory && HasLivingMonster(encounter))
        {
            CombatTurn counterattacks = ResolveCounterattacks(player, encounter, random);
            result.Turn.MonsterDamage = counterattacks.MonsterDamage;
            result.Turn.MonsterDamageBySlot = counterattacks.MonsterDamageBySlot;
            result.Turn.Outcome = counterattacks.Outcome;
        }
        return result;
    }

    public static string EquipmentUpgradeName(EquipmentUpgrade upgrade)
    {
        switch (upgrade)
        {
            case EquipmentUpgrade.Weapon:
                return "weapon";
            case EquipmentUpgrade.Armor:
                return "armor";
            case EquipmentUpgrade.Boots:
                return "boots";
            case EquipmentUpgrade.Gloves:
                return "gloves";
            case EquipmentUpgrade.Helmet:
                return "helmet";
            case EquipmentUpgrade.Ring:
                return "ring";
            case EquipmentUpgrade.Necklace:
                return "necklace";
            case EquipmentUpgrade.None:
                return "";
        }
        return "";
    }
}
