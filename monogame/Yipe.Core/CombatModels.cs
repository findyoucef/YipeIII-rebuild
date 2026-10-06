namespace Yipe.Core;

public sealed class Monster
{
    public int TypeId;
    public string Name = "";
    public int BitmapResourceId;
    public int Health;
    public int MaximumHealth;
    public int SpecialRating;
    public int Rank;
    public int AttackRating;
    public int DefenseRating;
    public bool GatekeeperEmpowered;
}

public readonly record struct MonsterDefinition(int TypeId, string Name, int StringResourceId,
    int BitmapResourceId, int HealthBase, int RankAndHealthRange, int AttackBase, int DefenseBase,
    int SpecialRating, int DamageMinimum, int DamageMaximum);

public enum EquipmentUpgrade { None, Weapon, Armor, Boots, Gloves, Helmet, Ring, Necklace }
public record struct LootReward(EquipmentUpgrade EquipmentUpgrade = EquipmentUpgrade.None,
    ushort ItemId = 0, bool InventoryFull = false, bool WaterproofShoeSpray = false);
public record struct CombatReward(int Experience = 0, int Gold = 0,
    EquipmentUpgrade EquipmentUpgrade = EquipmentUpgrade.None, bool UnderworldKey = false);

public sealed class CombatEncounter
{
    public const int MaximumOpponents = 3;
    public Monster[] Monsters { get; } = [new(), new(), new()];
    public int SelectedTarget;
    public CombatReward PendingReward;
    public int MapEnvironment = -1;
}

public sealed class EncounterSetup
{
    public int PlayerLevel = 1;
    public int Difficulty = 2;
    public int CompanionType1;
    public int CompanionType2;
    public int GatekeeperState;
    public int MapEnvironment = -1;
}

public enum CombatOutcome { Ongoing, Victory, Defeat }
public sealed class CombatTurn
{
    public int PlayerDamage;
    public int MonsterDamage;
    public int[] MonsterDamageBySlot = new int[3];
    public CombatOutcome Outcome;
    public CombatReward Reward;
    public LevelUpResult LevelUp;
}

public sealed class FleeResult
{
    public bool Escaped;
    public int MonsterDamage;
    public bool PlayerDefeated;
    public int[] MonsterDamageBySlot = new int[3];
}

public sealed class CombatSpellResult
{
    public SpellCastStatus Status = SpellCastStatus.Unavailable;
    public int[] DamageBySlot = new int[3];
    public int Healing;
    public CombatTurn Turn = new();
}
