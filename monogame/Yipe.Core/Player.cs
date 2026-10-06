namespace Yipe.Core;

public enum CharacterType : byte { Warrior = 1, Rogue = 2, Wizard = 3 }
public enum SpecialAbility : byte { None, Backstab, Frighten, Invisibility, Enrage }
public enum HellGatekeeperState : short { Absent, Waiting, Paid = -1, Defeated = -2 }
public enum StatIncrease : byte { None, Strength, Intelligence, Dexterity }

public readonly record struct LevelUpResult(
    bool LeveledUp, int HealthIncrease, int SpellPointIncrease, StatIncrease StatIncrease);
public readonly record struct ItemUseResult(bool Consumed, int Amount, int SoundId, string Message);

/// <summary>Character state and rules translated from include/yipe/player.hpp and src/player.cpp.</summary>
public sealed class Player
{
    public const int InventorySize = 30;
    public const int CarriedInventorySize = 13;
    public const int SpellCount = 15;
    public const int QuestFlagCount = 30;
    public const int MonsterHistorySize = 42;
    public const int MonsterDefeatTableSize = 50;

    public string Name { get; set; } = "Yipie";
    public int Dexterity { get; set; }
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Health { get; set; }
    public int MaximumHealth { get; set; }
    public int SpellPoints { get; set; }
    public int MaximumSpellPoints { get; set; }
    public int AbilityPoints { get; set; }
    public int MaximumAbilityPoints { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int Gold { get; set; }
    public int WeaponRank { get; set; } = 2;
    public int ArmorRank { get; set; } = 2;
    public ushort BootsRank { get; set; }
    public ushort GlovesRank { get; set; }
    public ushort HelmetRank { get; set; } = 1;
    public ushort RingRank { get; set; }
    public ushort NecklaceRank { get; set; }
    public int Difficulty { get; set; } = 2;
    // Original +0x67c: level-up bias, separate from the chosen specialty.
    public int CombatStyle { get; set; }
    // Original +0x6cc: 0 Quickness, 1 Magic, 2 Random, 3 Combat.
    public int Specialty { get; set; } = 3;
    public bool SillyMode { get; set; }
    public int EquipmentUpgradeCount { get; set; }
    public int LootRollPenalty { get; set; } = 2;
    public int RestorationUses { get; set; }
    public int DeathCount { get; set; }
    public int QuestScore { get; set; }
    // Includes the unattributed aggregate retained by older native saves.
    public int QuestProgressTotal { get; set; }
    public ushort GatekeeperState { get; set; }
    public ushort StampsFound { get; set; }
    public ushort PrincessesFound { get; set; }
    public ushort PrincessMysteryUses { get; set; }
    // 0 initial, 1 higher prices, 2 key offered, 3 bought, 4 shop exited.
    public ushort MiscellaneousShopStage { get; set; }
    // Only value 5 has an identified meaning: a High Score Code suffix.
    public ushort Value06a4 { get; set; }
    public bool HasLuckyRabbitFoot { get; set; }
    public bool HasWaterproofShoeSpray { get; set; }
    public bool HasRainbowKey { get; set; }
    public bool HasUnderworldKey { get; set; }
    public bool HasTreeClimbingShoes { get; set; }
    public SpecialAbility SpecialAbility { get; set; }
    public int SpecialAbilityDuration { get; set; }
    public int EnrageBaseDexterity { get; set; }
    public int EnrageBaseIntelligence { get; set; }
    public int EnrageBaseStrength { get; set; }

    // Only slots 0..12 are carried items. Later slots also hold original metadata.
    public ushort[] Inventory { get; } = new ushort[InventorySize];
    // Rescue slots retain their raw bits but are tested as signed 16-bit values.
    public ushort[] InventoryValues { get; } = new ushort[InventorySize];
    public bool[] QuestFlags { get; } = new bool[QuestFlagCount];
    // Four one-time chest flags per map, for maps 1..128.
    public byte[] OpenedChests { get; } = new byte[128];
    public ushort[] Spells { get; } = new ushort[SpellCount];
    public bool[] EncounteredMonsterTypes { get; } = new bool[MonsterHistorySize];
    public int[] MonsterDefeats { get; } = new int[MonsterDefeatTableSize];
    public bool MonsterDefeatHistoryComplete { get; set; } = true;

    public bool AlternateMapSet { get; set; }
    public bool HasBonusHealingRing { get; set; }
    public int CurrentMapId { get; set; } = 0x11;
    // Inherited D8 adaptation: new characters use cap 50; saved modes retain their caps.
    public int StatCapMode { get; set; } = 2;
    public short SurfaceReturnMapId { get; set; }
    public short SurfaceReturnRegionX { get; set; }
    public short SurfaceReturnRegionY { get; set; }
    public ushort RandomEncounterThreshold { get; set; } = 85;
    public ushort SavedRandomEncounterThreshold { get; set; } = 85;
    // Historical name; this counts exploration timer ticks, not footsteps.
    public ushort MonsterRepellentSteps { get; set; }
    public ushort PendingRelocation { get; set; }
    public bool EnteredHellThroughSecretPassage { get; set; }
    public HellGatekeeperState HellGatekeeperState { get; set; }
    public bool BridgeTreasureAvailable { get; set; } = true;
    public bool HealingWaterAvailable { get; set; } = true;
    public bool CaveCoinsCollected { get; set; }
    public ushort SnowSearchCount { get; set; }
    public bool HasDefeatedWormoid { get; set; }

    public Player(CharacterType type = CharacterType.Warrior)
    {
        Inventory[0] = 1;
        Spells[0] = 1;
        switch (type)
        {
            case CharacterType.Warrior:
                Specialty = 3;
                Strength = 16;
                Dexterity = 13;
                Intelligence = 10;
                Health = 13;
                SpellPoints = 10;
                AbilityPoints = 5;
                break;
            case CharacterType.Rogue:
                Specialty = 0;
                Strength = 11;
                Dexterity = 15;
                Intelligence = 12;
                Health = 13;
                SpellPoints = 12;
                AbilityPoints = 6;
                break;
            case CharacterType.Wizard:
                Specialty = 1;
                Strength = 11;
                Dexterity = 12;
                Intelligence = 16;
                Health = 10;
                SpellPoints = 18;
                AbilityPoints = 5;
                break;
        }
        MaximumHealth = Health;
        MaximumSpellPoints = SpellPoints;
        MaximumAbilityPoints = AbilityPoints;
    }

    public void RerollRandomCharacter(MsvcRandom random)
    {
        Specialty = 2;
        Dexterity = Math.Max(10, RandomStartingStat(random));
        Strength = Math.Max(10, RandomStartingStat(random));
        Intelligence = Math.Max(10, RandomStartingStat(random));
        var total = Dexterity + Strength + Intelligence;
        Health = (total < 34 ? 14 : total < 40 ? 12 : 10) + random.Next() % 2;
        SpellPoints = Health + 1 + random.Next() % 2;
        AbilityPoints = 4 + random.Next() % 2;
        MaximumHealth = Health;
        MaximumSpellPoints = SpellPoints;
        MaximumAbilityPoints = AbilityPoints;
    }

    private static int RandomStartingStat(MsvcRandom random)
    {
        int[] dice = [3, 3, 3, 0, 0, 0, 0, 0];
        for (var index = 3; index < dice.Length; index++) dice[index] = random.Next() % 3 + 2;
        Array.Sort(dice);
        return dice[7] + dice[6] + dice[5] + random.Next() % 3
            + (random.Next() % 100 > 90 ? 1 : 0);
    }

    public bool AdjustHealth(int delta)
    {
        Health = Math.Min(Health + delta, MaximumHealth);
        return Health > 0;
    }

    public int MaximumStat => StatCapMode switch { 1 => 40, 2 => 50, _ => 30 };

    public int RescuedPrincessCount => InventoryValues.Count(value => unchecked((short)value) > 0);

    public void RecordMonsterDefeat(int typeId)
    {
        if (typeId < 1 || typeId > MonsterDefeats.Length)
            throw new ArgumentOutOfRangeException(nameof(typeId));
        var count = MonsterDefeats[typeId - 1] = unchecked(MonsterDefeats[typeId - 1] + 1);
        QuestProgressTotal = unchecked(QuestProgressTotal + 1);
        if (typeId == 35) HasDefeatedWormoid = count != 0;
    }

    public static int? ExperienceRequiredForNextLevel(int level)
    {
        ReadOnlySpan<int> thresholds = [
            0, 200, 500, 1000, 1500, 2000, 2500, 3000, 4000, 5000,
            6000, 7100, 8200, 9300, 10400, 11600, 13250, 15100, 17200,
            19200, 21750, 23250, 25000, 27000, 30000, 33500, 37500, 42000,
        ];
        if (level < 1 || level >= 100) return null;
        if (level < thresholds.Length) return thresholds[level] + 1;
        return level switch
        {
            28 => 47001,
            29 => 52001,
            30 => 57001,
            31 => 63001,
            32 => 75001,
            33 => 90001,
            < 55 => level * 3500 + 1,
            < 80 => level * 4000 + 1,
            _ => level * 5000 + 1,
        };
    }

    // A completed encounter grants at most one level. The caller controls when this runs.
    public LevelUpResult TryLevelUp(MsvcRandom random)
    {
        var required = ExperienceRequiredForNextLevel(Level);
        if (required is null || Experience < required.Value) return default;

        var oldLevel = Level;
        var healthRoll = random.Next() % 4;
        var healthIncrease = healthRoll + 5;
        if (oldLevel > 10 && Intelligence > 20) healthIncrease = healthRoll + 6;
        if (oldLevel > 8) healthIncrease++;
        if (oldLevel < 3 && Difficulty == 1) healthIncrease++;
        if (oldLevel > 20) healthIncrease--;
        if (oldLevel > 40) healthIncrease--;
        healthIncrease = Math.Max(healthIncrease, 3);
        if (oldLevel > 35) healthIncrease = Math.Min(healthIncrease, 4);
        Health += healthIncrease;
        MaximumHealth += healthIncrease;
        Level++;

        var spellPointIncrease = random.Next() % 4 + 3;
        if (Level > 40) spellPointIncrease = Math.Min(spellPointIncrease, 5);
        SpellPoints += spellPointIncrease;
        MaximumSpellPoints += spellPointIncrease;
        if (RestorationUses > 0) RestorationUses--;

        var result = new LevelUpResult(true, healthIncrease, spellPointIncrease, StatIncrease.None);
        var statChance = random.Next() % 100 - Difficulty;
        if (Level is 5 or 10 or 15 or 20 or 30) statChance = 100;
        else if (statChance < 73) return result;

        var statRoll = random.Next() % 8;
        if (statRoll is 4 or 5 || statChance == 100)
            statRoll = CombatStyle == 1 ? 2 : CombatStyle == 3 ? 3 : 1;
        if (statRoll == 1 && Strength < MaximumStat)
        {
            Strength++;
            result = result with { StatIncrease = StatIncrease.Strength };
        }
        else if (statRoll == 2 && Intelligence < MaximumStat)
        {
            Intelligence++;
            result = result with { StatIncrease = StatIncrease.Intelligence };
        }
        else if (statRoll == 3 && Dexterity < MaximumStat)
        {
            Dexterity++;
            result = result with { StatIncrease = StatIncrease.Dexterity };
        }
        return result;
    }

    // Map/combat context is supplied by the caller, as in the original function.
    // This can address all 30 stored slots; a carried-item UI must restrict itself to 13.
    public ItemUseResult UseInventoryItem(int slot, MsvcRandom random, bool playSound = true,
        bool inCombat = false, int currentMapId = 0x11, bool alternateMapSet = false)
    {
        if (slot < 0 || slot >= Inventory.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        switch (Inventory[slot])
        {
            case 1:
                {
                    if (Health == MaximumHealth) return new(false, 0, 0, "You are already at full health");
                    var healing = random.Next() % Intelligence + 10;
                    if (HasBonusHealingRing) healing += random.Next() % Intelligence + 12;
                    Inventory[slot] = 0;
                    Health = Math.Min(Health + healing, MaximumHealth);
                    return new(true, healing, playSound ? 0x8a : 0, "Health restored");
                }
            case 2:
                {
                    if (Health == MaximumHealth) return new(false, 0, 0, "You are already at full health");
                    var healing = random.Next() % Intelligence + 50 + Intelligence;
                    if (HasBonusHealingRing) healing += 12 + Intelligence;
                    Inventory[slot] = 0;
                    Health = Math.Min(Health + healing, MaximumHealth);
                    return new(true, healing, playSound ? 0x8a : 0, "Health restored");
                }
            case 3:
                {
                    if (SpellPoints == MaximumSpellPoints)
                        return new(false, 0, 0, "Your spell points cannot be regenerated");
                    var restored = random.Next() % Intelligence + 11 + Intelligence;
                    if (HasBonusHealingRing) restored += 2 + Intelligence;
                    Inventory[slot] = 0;
                    SpellPoints = Math.Min(SpellPoints + restored, MaximumSpellPoints);
                    return new(true, restored, 0, "Spell points restored");
                }
            case 4:
                {
                    if (RandomEncounterThreshold < 100) SavedRandomEncounterThreshold = RandomEncounterThreshold;
                    var firstRoll = random.Next();
                    var duration = Math.Min(random.Next() % Intelligence + 25 + firstRoll % (Level / 2 + 1), 80);
                    MonsterRepellentSteps = unchecked((ushort)(MonsterRepellentSteps + duration));
                    RandomEncounterThreshold = 125;
                    Inventory[slot] = 0;
                    return new(true, duration, playSound ? 0x0e : 0, "Roaming monsters will now stay away");
                }
            case 5:
                if (SurfaceReturnMapId == 1)
                    return new(false, 0, 0, "You have already used a potion to return to the surface");
                if (currentMapId < 40) return new(false, 0, 0, "You are already on the surface");
                if (alternateMapSet) return new(false, 0, 0, "The surface potion has no effect here");
                if (inCombat) return new(false, 0, 0, "You cannot use this potion while fighting");
                PendingRelocation = (ushort)Yipe.Core.PendingRelocation.Surface;
                Inventory[slot] = 0;
                return new(true, 0, playSound ? 0x0e : 0, "The surface potion will take effect");
            case 6:
                if (Strength >= MaximumStat) return new(false, 0, 0, "Strength is already at its maximum");
                Strength++;
                Inventory[slot] = 0;
                return new(true, 1, playSound ? 0x0e : 0, "Strength increased");
            case 7:
                if (Dexterity >= MaximumStat) return new(false, 0, 0, "Dexterity is already at its maximum");
                Dexterity++;
                Inventory[slot] = 0;
                return new(true, 1, playSound ? 0x0e : 0, "Dexterity increased");
            case 8:
                if (Intelligence >= MaximumStat) return new(false, 0, 0, "Intelligence is already at its maximum");
                Intelligence++;
                Inventory[slot] = 0;
                return new(true, 1, playSound ? 0x0e : 0, "Intelligence increased");
            case 9:
                {
                    var increase = random.Next() % 3 + 2;
                    Health += increase;
                    MaximumHealth += increase;
                    Inventory[slot] = 0;
                    return new(true, increase, playSound ? 0x0e : 0, "Maximum health increased");
                }
            case 10:
                {
                    var increase = random.Next() % 4 + 2;
                    SpellPoints += increase;
                    MaximumSpellPoints += increase;
                    Inventory[slot] = 0;
                    return new(true, increase, playSound ? 0x0e : 0, "Maximum spell points increased");
                }
            default:
                if (slot < CarriedInventorySize) Inventory[slot] = 0;
                return new(false, 0, 0, "Invalid item; item removed");
        }
    }
}
