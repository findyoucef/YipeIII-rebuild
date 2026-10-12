using Yipe.Core;

namespace Yipe.Desktop;

internal enum CombatAction { Attack, Spell, Ability, Item, Flee }
internal readonly record struct CombatActor(int Type, string Name, int Health, int MaximumHealth, bool GatekeeperEmpowered = false);
internal readonly record struct CombatResources(int SP, int MaximumSP, int AP, int MaximumAP);

/// <summary>A display-only snapshot of one resolved action. Never owns or advances combat.</summary>
internal sealed class CombatPresentation
{
    public const double Impact = .18;
    public CombatAction Action { get; }
    public int Target { get; }
    public int Spell { get; }
    public SpecialAbility Ability { get; }
    public CombatActor[] Before { get; }
    public CombatActor[] After { get; private set; } = [];
    public CombatResources ResourcesBefore { get; }
    public CombatResources ResourcesAfter { get; private set; }
    public int SpellCost { get; }
    public int[] DamageToMonsters { get; private set; } = new int[3];
    public int[] DamageFromMonsters { get; private set; } = new int[3];
    public int Healing { get; private set; }
    public int Sound { get; private set; }
    public bool Counters { get; private set; }
    public CombatTurn Turn { get; private set; } = new();
    public double LastImpact
    {
        get
        {
            var last = Impact;
            for (var slot = 0; slot < 3; slot++)
                if (double.IsFinite(CounterAt(slot))) last = CounterAt(slot);
            return last;
        }
    }
    public double RewardsAt => LastImpact + .55;
    public double Duration => LastImpact + .9;

    public CombatPresentation(Player hero, CombatEncounter battle, CombatAction action,
        int spell = -1, SpellPower power = SpellPower.Low)
    {
        Action = action; Spell = spell; Ability = hero.SpecialAbility; Target = battle.SelectedTarget;
        Before = Actors(hero, battle); ResourcesBefore = Resources(hero);
        SpellCost = spell >= 0 ? Spells.PointCost(hero, spell, power) : 0;
    }

    public void Complete(Player hero, CombatEncounter battle, CombatTurn turn, int[]? damage = null,
        int healing = 0, int sound = 0, bool escaped = false)
    {
        After = Actors(hero, battle); ResourcesAfter = Resources(hero); Turn = turn;
        DamageFromMonsters = (int[])turn.MonsterDamageBySlot.Clone();
        if (damage is not null) DamageToMonsters = (int[])damage.Clone();
        else if (Action is CombatAction.Attack or CombatAction.Ability) DamageToMonsters[Target] = turn.PlayerDamage;
        Healing = healing; Sound = sound;
        Counters = Action != CombatAction.Ability && !escaped && turn.Outcome != CombatOutcome.Victory;
    }

    public double CounterAt(int slot)
    {
        if (!Counters || After[slot + 1].Health <= 0) return double.PositiveInfinity;
        var time = .7;
        for (var earlier = 0; earlier < slot; earlier++)
            if (After[earlier + 1].Health > 0) time += .48;
        return time;
    }

    public float HealthAt(int actor, double time)
    {
        if (actor > 0) return Between(Before[actor].Health, After[actor].Health, time - Impact);
        var healed = Math.Min(Before[0].Health + Healing, After[0].MaximumHealth);
        var health = Between(Before[0].Health, healed, time - Impact);
        var remaining = healed;
        for (var slot = 0; slot < 3; slot++)
        {
            var next = Math.Max(0, remaining - DamageFromMonsters[slot]);
            if (time >= CounterAt(slot)) health = Between(remaining, next, time - CounterAt(slot));
            remaining = next;
        }
        return time >= RewardsAt ? Between(remaining, After[0].Health, time - RewardsAt) : health;
    }

    public CombatResources ResourcesAt(double time)
    {
        if (time < Impact) return ResourcesBefore;
        if (time >= RewardsAt) return ResourcesAfter;
        return new(Action == CombatAction.Spell ? Math.Max(0, ResourcesBefore.SP - SpellCost)
                : ResourcesAfter.SP - Turn.LevelUp.SpellPointIncrease,
            ResourcesAfter.MaximumSP - Turn.LevelUp.SpellPointIncrease,
            Action == CombatAction.Ability ? Math.Max(0, ResourcesBefore.AP - 3) : ResourcesAfter.AP, ResourcesAfter.MaximumAP);
    }

    private static float Between(int start, int end, double age) =>
        start + (end - start) * (float)Math.Clamp(age / .2, 0, 1);
    private static CombatResources Resources(Player hero) =>
        new(hero.SpellPoints, hero.MaximumSpellPoints, hero.AbilityPoints, hero.MaximumAbilityPoints);
    private static CombatActor[] Actors(Player hero, CombatEncounter battle) =>
        new[] { new CombatActor(0, "YOU", hero.Health, hero.MaximumHealth) }
            .Concat(battle.Monsters.Select(monster => new CombatActor(monster.TypeId, monster.Name, monster.Health,
                monster.MaximumHealth, monster.GatekeeperEmpowered))).ToArray();
}
