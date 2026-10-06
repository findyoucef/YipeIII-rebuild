namespace Yipe.Core;

public enum SpellUsage { NonCombat, Combat, Either }
public enum SpellPower { Low = 1, High = 2 }
public enum SpellCastStatus { Cast, Unlearned, WrongContext, InsufficientSpellPoints, Unavailable }
public readonly record struct SpellDefinition(string Name, string Description, int Price, int RequiredLevel,
    int HardRequiredLevel, SpellUsage Usage, int MagicCost, int RandomCost, int OtherCost, bool HighPowerCostsDouble);
public readonly record struct ExplorationSpellResult(SpellCastStatus Status, int Healing = 0,
    int RepellentDuration = 0, bool LocationChanged = false);

public static class Spells
{
    public static IReadOnlyList<SpellDefinition> Definitions { get; } = Array.AsReadOnly<SpellDefinition>([
        new("Return to Surface", "This spell returns you from an underground cave or dungeon back to the surface of Yipe!", 50, 1, 1, SpellUsage.NonCombat, 5, 5, 5, false),
        new("Small Heal", "You can use this spell to heal some of your health points. Using the spell on high power increases the healing amount.", 100, 2, 2, SpellUsage.Either, 6, 10, 8, true),
        new("Gobbler Attack", "This spell gobbles up some of your enemies' health. Use on high power to gobble all enemies present.", 200, 3, 3, SpellUsage.Combat, 10, 14, 12, true),
        new("Insect Attack", "This spell creates an alien insect cloud that attacks your enemy. Use on high power to attack all enemies present at once.", 300, 7, 7, SpellUsage.Combat, 15, 20, 18, true),
        new("Stinky Smell", "Upon casting this spell a very stinky smell will surround you and keep monsters from appearing for a short time.", 250, 10, 10, SpellUsage.NonCombat, 8, 14, 12, true),
        new("Full Heal", "This spell will completely heal your health points.", 500, 13, 13, SpellUsage.Either, 30, 38, 33, false),
        new("Brain Crush", "This spell further shrinks a monster's already small brain, causing massive damage.", 800, 16, 18, SpellUsage.Combat, 27, 34, 30, true),
        new("Health Suction", "This bizarre spell will suck health out of your enemy and heal you by that same amount. Use on high power for higher damage and healing.", 1000, 20, 22, SpellUsage.Combat, 22, 28, 25, true),
        new("Return to Yipe", "After casting this spell you will end up back in the town of Yipe! It can only be cast on the surface of the world.", 500, 23, 24, SpellUsage.NonCombat, 12, 15, 15, false),
        new("Super Heal", "This powerful healing spell restores health and ability points, but costs many spell points to cast.", 1000, 30, 32, SpellUsage.Either, 50, 60, 55, true),
        new("HP Shuffle", "This spell will cause the health points of all monsters you are currently fighting to randomly switch around.", 750, 34, 35, SpellUsage.Combat, 10, 14, 12, false),
        new("Rabbit Replacement", "If there are two or three monsters one of them will be turned into a rabbit. The spell won't work with only one monster.", 1000, 40, 42, SpellUsage.Combat, 30, 36, 33, false),
    ]);

    public static int RequiredLevel(SpellDefinition spell, int difficulty) => difficulty == 3 ? spell.HardRequiredLevel : spell.RequiredLevel;
    public static int PointCost(Player p, int index, SpellPower power)
    {
        if (index < 0 || index >= Definitions.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var spell = Definitions[index];
        var cost = p.Specialty == 1 ? spell.MagicCost : p.Specialty == 2 ? spell.RandomCost : spell.OtherCost;
        return cost * (power == SpellPower.High && spell.HighPowerCostsDouble ? 2 : 1);
    }

    public static ExplorationSpellResult CastExploration(Player p, WorldLocation location, int index, SpellPower power, MsvcRandom random)
    {
        if (index < 0 || index >= Definitions.Count) return new(SpellCastStatus.Unavailable);
        if (p.Spells[index] == 0) return new(SpellCastStatus.Unlearned);
        if (Definitions[index].Usage == SpellUsage.Combat) return new(SpellCastStatus.WrongContext);
        if (p.SpellPoints < PointCost(p, index, power)) return new(SpellCastStatus.InsufficientSpellPoints);
        if (index == 0 && (location.MapLayer == 0 || location.AlternateMapSet) || index == 8 && location.MapLayer != 0)
            return new(SpellCastStatus.Unavailable);
        var multiplier = (int)power;
        p.SpellPoints -= PointCost(p, index, power);
        switch (index)
        {
            case 0:
                p.PendingRelocation = (ushort)PendingRelocation.Surface;
                return new(SpellCastStatus.Cast, LocationChanged: true);
            case 1:
                var small = (random.Next() % 5 + 8 + p.Intelligence / 2) * multiplier;
                if (p.HasBonusHealingRing) small += multiplier * 6 + p.Intelligence;
                return new(SpellCastStatus.Cast, Heal(small));
            case 4:
                if (p.RandomEncounterThreshold < 100) p.SavedRandomEncounterThreshold = p.RandomEncounterThreshold;
                var levelRoll = random.Next(); var intelligenceRoll = random.Next();
                var duration = Math.Min((levelRoll % (p.Level / 2 + 1) + 9 + intelligenceRoll % (p.Intelligence + 1)) * multiplier, 75);
                p.MonsterRepellentSteps = unchecked((ushort)(p.MonsterRepellentSteps + duration));
                p.RandomEncounterThreshold = 125;
                return new(SpellCastStatus.Cast, RepellentDuration: duration);
            case 5: return new(SpellCastStatus.Cast, Heal(p.MaximumHealth - p.Health));
            case 8:
                p.PendingRelocation = (ushort)PendingRelocation.Yipe;
                return new(SpellCastStatus.Cast, LocationChanged: true);
            case 9:
                var large = (random.Next() % 5 + 10 + p.Intelligence / 2) * multiplier + p.Intelligence;
                if (p.HasBonusHealingRing) large += multiplier * 2 + p.Intelligence;
                var healing = Heal(large);
                p.AbilityPoints = power == SpellPower.Low ? Math.Min(p.AbilityPoints + 1, p.MaximumAbilityPoints) : p.MaximumAbilityPoints;
                return new(SpellCastStatus.Cast, healing);
            default: return new(SpellCastStatus.WrongContext);
        }
        int Heal(int amount)
        {
            var actual = Math.Min(amount, p.MaximumHealth - p.Health);
            p.AdjustHealth(actual);
            return actual;
        }
    }
}
