using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private void DrawCombatAction(CombatPresentation p, double time)
    {
        var age = time - CombatPresentation.Impact;
        var fade = (float)Math.Clamp((.6 - age) / .25, 0, 1);
        if (age is >= 0 and < .6)
        {
            for (var actor = 0; actor < 4; actor++)
            {
                var center = CombatBounds(actor).Center.X;
                var target = actor == p.Target + 1;
                if (actor > 0 && p.Before[actor].Health <= 0) continue;
                if (actor > 0 && p.After[actor].Health <= 0 && p.Before[actor].Health > 0)
                    Burst(center, age, Ink * fade);
                if (p.Action == CombatAction.Attack && target || p.Action == CombatAction.Ability && p.Ability == SpecialAbility.Backstab && target)
                {
                    if (p.DamageToMonsters[actor - 1] > 0) Slash(center, age, Ink * fade, p.Action == CombatAction.Ability);
                    else DrawCombatNumber("NO DAMAGE", center, age, Muted, false);
                }
                if (p.Action == CombatAction.Spell)
                {
                    if (actor == 0 && p.Spell is 1 or 5 or 7 or 9)
                        Crosses(center, age, CombatGreen * fade, p.Spell is 5 or 9 ? 6 : 3);
                    if (actor == 0 && p.Spell == 9) Crosses(center + 8, age, Accent * fade, 2);
                    if (actor > 0 && p.DamageToMonsters[actor - 1] > 0)
                    {
                        switch (p.Spell)
                        {
                            case 2: // A pair of closing, toothed jaws.
                                var gap = 4 + (int)(Math.Abs(Math.Cos(age * 12)) * 7);
                                for (var tooth = -2; tooth <= 2; tooth++)
                                {
                                    Fill(new(center + tooth * 5, 45 - gap, 3, 2), CombatGreen * fade);
                                    Fill(new(center + tooth * 5, 51 + gap / 2, 3, 2), CombatGreen * fade);
                                }
                                break;
                            case 3:
                                for (var insect = 0; insect < 8; insect++)
                                {
                                    var phase = insect * 2.4 + age * 18;
                                    var x = center + (int)(Math.Cos(phase) * (10 + insect % 3));
                                    var y = 47 + (int)(Math.Sin(phase * 1.3) * 12);
                                    Fill(new(x, y, 2, 1), Accent * fade);
                                    Fill(new(x - 1, y - 1 + insect % 2, 1, 1), Ink * fade);
                                }
                                break;
                            case 6:
                                var size = 28 - (int)(age * 25);
                                CombatBrackets(new(center - size / 2, 31 + (28 - size) / 2, size, size), new Color(187, 142, 211) * fade);
                                break;
                            case 7:
                                for (var mote = 0; mote < 5; mote++)
                                {
                                    var travel = Math.Clamp(age * 2.2 - mote * .09, 0, 1);
                                    Fill(new((int)(center + (48 - center) * travel), 48 + (int)(Math.Sin(travel * Math.PI) * -12) + mote % 3,
                                        2, 1), CombatGreen * fade);
                                }
                                break;
                        }
                    }
                    if (p.Spell == 10 && actor > 0)
                    {
                        var phase = age * 12 + actor;
                        for (var dot = 0; dot < 5; dot++)
                            Fill(new(center - 20 + dot * 9, 33 + (int)(Math.Sin(phase + dot) * 3), 2, 1), CombatBlue * fade);
                    }
                    if (p.Spell == 11 && actor > 0 && p.Before[actor].Type != p.After[actor].Type)
                        Burst(center, age, new Color(187, 142, 211) * fade);
                }
                if (p.Action == CombatAction.Ability)
                {
                    switch (p.Ability)
                    {
                        case SpecialAbility.Frighten when actor == 3:
                            CombatBrackets(new(center - 10 - (int)(age * 15), 33, 20 + (int)(age * 30), 26), CombatCoral * fade);
                            break;
                        case SpecialAbility.Invisibility when actor == 0:
                            Burst(center, age, CombatBlue * fade); break;
                        case SpecialAbility.Enrage when actor == 0:
                            Crosses(center, age, CombatCoral * fade, 5); break;
                    }
                }
                if (p.Action == CombatAction.Item && actor == 0) Crosses(center, age, (p.Healing > 0 ? CombatGreen : CombatBlue) * fade, 3);
            }
        }
        for (var slot = 0; slot < 3; slot++)
            if (p.DamageFromMonsters[slot] > 0) Slash(48, time - p.CounterAt(slot), CombatCoral, false);
        if (p.Turn.Outcome != CombatOutcome.Victory || time < p.RewardsAt) return;
        Fill(new(83, 31, 213, 32), Background);
        font.Draw(batch, $"GOLD +{p.Turn.Reward.Gold}", 99, 35, Accent);
        font.Draw(batch, $"XP   +{p.Turn.Reward.Experience}", 99, 47, CombatBlue);
        if (p.Turn.LevelUp.LeveledUp)
        {
            font.Draw(batch, $"LEVEL {Hero.Level}!", 210, 41, CombatGreen);
            if (time < p.RewardsAt + .75) Crosses(48, time - p.RewardsAt, Accent * (float)(1 - (time - p.RewardsAt) / .75), 6);
        }
    }

    private void Slash(int center, double age, Color color, bool doubled)
    {
        if (age is < 0 or >= .2) return;
        color *= (float)(1 - age / .2);
        for (var pixel = 0; pixel < 13; pixel++)
        {
            Fill(new(center + 14 - pixel / 2, 32 + pixel, 2, 1), color);
            if (doubled) Fill(new(center - 14 + pixel / 2, 32 + pixel, 2, 1), color);
        }
    }
    private void Burst(int center, double age, Color color)
    {
        for (var chip = 0; chip < 10; chip++)
        {
            var direction = chip * Math.PI / 5;
            var radius = 6 + age * 25;
            var x = center + (int)(Math.Cos(direction) * radius);
            var y = Math.Clamp(46 + (int)(Math.Sin(direction) * radius * .65), 30, 61);
            Fill(new(x, y, 2, 2), color);
        }
    }
    private void Crosses(int center, double age, Color color, int count)
    {
        for (var mote = 0; mote < count; mote++)
        {
            var x = center - 15 + (mote * 13 % 31);
            var y = 59 - (int)(age * 30) - mote % 3 * 5;
            Fill(new(x - 1, y, 3, 1), color); Fill(new(x, y - 1, 1, 3), color);
        }
    }
}
