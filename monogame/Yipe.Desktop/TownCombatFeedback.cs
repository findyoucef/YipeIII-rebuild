using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private static readonly Color CombatGreen = new(132, 194, 139);
    private static readonly Color CombatCoral = new(237, 126, 91);
    private static readonly Color CombatBlue = new(121, 174, 207);

    private void ShowCombatResult(string title, string text, Action done, CombatPresentation presentation, bool autoAdvance)
    {
        void Continue()
        {
            // Remember an early confirmation and finish the animation quickly, including death cues.
            if (overlay!.CombatTime < presentation.Duration)
            {
                overlay.ContinueRequested = true;
                return;
            }
            done();
        }
        overlay = new(title, text, [new("Continue", Continue)], Continue)
        { Combat = true, Presentation = presentation, AutoAdvance = autoAdvance };
    }

    private bool UpdateCombatPresentation(double elapsed)
    {
        if (overlay is not { Combat: true } current) return false;
        if (current.Presentation is not { } presentation)
        {
            if (!current.InspectedEnemy.HasValue) current.CombatTime += elapsed;
            return false;
        }
        var before = current.CombatTime;
        current.CombatTime = Math.Min(presentation.Duration + 1, before + elapsed * (current.ContinueRequested ? 3 : 1));
        bool Crossed(double time) => before < time && current.CombatTime >= time;
        if (Crossed(CombatPresentation.Impact)) audio.Play(presentation.Sound, overlap: true);
        for (var slot = 0; slot < 3; slot++)
        {
            if (Crossed(presentation.CounterAt(slot)) && presentation.DamageFromMonsters[slot] > 0) audio.Play(134, overlap: true);
            if (presentation.Before[slot + 1].Health > 0 && presentation.After[slot + 1].Health <= 0
                && presentation.After[slot + 1].Type > 0 && Crossed(.4 + slot * .08))
                audio.MonsterVoice(presentation.Before[slot + 1].Type);
        }
        if (Crossed(presentation.RewardsAt))
        {
            if (presentation.Turn.Outcome == CombatOutcome.Defeat) audio.Play(128, overlap: true);
            if (presentation.Turn.Outcome == CombatOutcome.Victory) audio.Play(presentation.Turn.LevelUp.LeveledUp ? 129 : 145, overlap: true);
        }
        var confirmed = current.ContinueRequested && current.CombatTime >= presentation.Duration;
        var automatic = profile.Data.AutoBattle && current.AutoAdvance && current.PageCount == 1
            && current.CombatTime >= presentation.Duration + .35;
        if (!confirmed && !automatic) return false;
        current.Options[0].Choose();
        return true; // Do not apply this frame's confirm or direction to the newly opened menu.
    }

    private static string CombatSpellHint(int spell) => spell switch
    {
        2 or 3 or 6 or 7 => "Low: selected enemy. High: all enemies.\nLeft/right changes target.",
        1 => "Restore HP. High power heals more.",
        5 => "Restore all HP.", 9 => "Restore HP and AP. High restores all AP.",
        10 => "Shuffle health between living enemies.",
        11 => "Replace the last companion with a rabbit.",
        _ => "This spell is used outside combat.",
    };

    private bool CombatPreview(Overlay current, CombatEncounter battle, int actor)
    {
        if (current.Presentation is not null) return false;
        if (current.PreviewSpell >= 0)
        {
            if (current.Selected > 1) return false;
            return current.PreviewSpell switch
            {
                1 or 5 or 9 => actor == 0,
                2 or 3 or 6 or 7 => actor > 0 && battle.Monsters[actor - 1].Health > 0
                    && (current.Selected == 1 || actor == battle.SelectedTarget + 1),
                10 => actor > 0 && battle.Monsters[actor - 1].Health > 0,
                11 => actor == (battle.Monsters[2].Health > 0 ? 3 : 2) && battle.Monsters.Count(m => m.Health > 0) >= 2,
                _ => false,
            };
        }
        return current.Targeting && actor == battle.SelectedTarget + 1;
    }

    private void DrawCombatOverlay(Overlay current, CombatEncounter battle)
    {
        if (current.InspectedEnemy is { } inspected)
        {
            DrawEnemyInfo(current, battle, inspected);
            return;
        }
        var presentation = current.Presentation;
        var time = current.CombatTime;
        font.Draw(batch, current.Title, 24, 13, Accent, current.Title.Length <= 8 ? 2 : 1);
        if (current.Targeting && current.PreviewSpell < 0)
            font.Draw(batch, battle.Monsters[battle.SelectedTarget].Name, 118, 18, Ink);
        DrawCombatActors(current, battle);
        var resources = presentation?.ResourcesAt(time) ?? new CombatResources(Hero.SpellPoints,
            Hero.MaximumSpellPoints, Hero.AbilityPoints, Hero.MaximumAbilityPoints);
        var health = presentation?.HealthAt(0, time) ?? Hero.Health;
        var maximum = presentation is null ? Hero.MaximumHealth : time < CombatPresentation.Impact
            ? presentation.Before[0].MaximumHealth : time < presentation.RewardsAt
                ? presentation.After[0].MaximumHealth - presentation.Turn.LevelUp.HealthIncrease : Hero.MaximumHealth;
        DrawCombatMeter(24, "HP", health, maximum, presentation?.HealthAt(0, time - .2) ?? health, CombatGreen);
        DrawCombatMeter(116, "SP", resources.SP, resources.MaximumSP, resources.SP, CombatBlue);
        DrawCombatMeter(208, "AP", resources.AP, resources.MaximumAP, resources.AP, Accent);

        var text = current.Lines;
        var lines = text.Length == 1 && text[0].Length == 0 ? 0
            : Math.Min(current.LinesPerPage, text.Length - current.Page * current.LinesPerPage);
        for (var row = 0; row < lines; row++)
            font.Draw(batch, text[current.Page * current.LinesPerPage + row], 24, 101 + row * 10, Ink);
        if (current.Options.Length == 1 && current.Page < current.PageCount - 1)
        {
            font.Draw(batch, $"Continue  {current.Page + 1}/{current.PageCount}", 24, touchEnabled ? 144 : 151, Accent);
            TouchTarget(new(22, 140, 275, 18), TouchConfirm);
        }
        else
        {
            var startY = current.Options.Length == 1 ? touchEnabled ? 143 : 151 : lines == 0 ? 100 : 105 + lines * 10;
            var rowHeight = touchEnabled ? 18 : 11;
            var visible = Math.Min(current.Options.Length, ((touchEnabled ? 157 - rowHeight + 2 : 159) - startY) / rowHeight + 1);
            var first = Math.Clamp(current.Selected - visible / 2, 0, Math.Max(0, current.Options.Length - visible));
            for (var row = 0; row < visible; row++)
            {
                var index = first + row; var y = startY + row * rowHeight;
                if (index == current.Selected) Fill(new(22, y - 2, 275, rowHeight), new Color(64, 73, 87));
                TouchChoice(new(22, y - 2, 275, rowHeight), index);
                var label = current.ContinueRequested ? "Continuing..." : current.Options[index].Label;
                font.Draw(batch, (index == current.Selected ? "> " : "  ") + (label.Length > 43 ? label[..40] + "..." : label),
                    24, y, index == current.Selected ? Accent : Ink);
            }
            if (current.Options.Length > visible) font.Draw(batch, $"{current.Selected + 1}/{current.Options.Length}", 268, 164, Muted);
        }
        if (current.Targeting) font.Draw(batch, current.PreviewSpell < 0
            ? "LEFT/RIGHT: TARGET   ESC: FLEE" : "LEFT/RIGHT: TARGET", 24, 164, Muted);
        else if (current.PageCount > 1) font.Draw(batch, $"PAGE {current.Page + 1}/{current.PageCount}", 24, 164, Muted);
        else if (current.AutoAdvance && profile.Data.AutoBattle) font.Draw(batch, "AUTO - CONTINUE TO SPEED UP", 24, 164, Muted);
        if (touchEnabled) DrawTouchNavigation(current.Targeting || current.PageCount > 1);
    }

    private void DrawEnemyInfo(Overlay current, CombatEncounter battle, int inspected)
    {
        var monster = battle.Monsters[inspected];
        var position = battle.Monsters.Take(inspected + 1).Count(enemy => enemy.Health > 0);
        var count = battle.Monsters.Count(enemy => enemy.Health > 0);
        font.Draw(batch, current.EnemyDetails ? "Enemy details" : "Enemy info", 24, 16, Accent);
        font.Draw(batch, $"{position}/{count}", 272, 16, Muted);
        font.Draw(batch, monster.Name, 24, 33, Ink);
        Fill(new(24, 46, 272, 1), Muted * .3f);
        if (monster.TypeId == 41 && monster.GatekeeperEmpowered)
            world.DrawEmpoweredGatekeeper(batch, new(20, 48, 96, 96), 0);
        else world.DrawAnimatedMonster(batch, monster.TypeId, new(20, 48, 96, 96), 0);

        if (!current.EnemyDetails)
        {
            Stat("HP", $"{monster.Health}/{monster.MaximumHealth}", 56);
            DrawCombatBar(new(134, 68, 156, 4), monster.Health, monster.MaximumHealth, monster.Health, CombatGreen);
            Stat("Rank", monster.Rank.ToString(), 85);
            Stat("Attack rating", monster.AttackRating.ToString(), 104);
            Stat("Defense rating", monster.DefenseRating.ToString(), 123);
        }
        else
        {
            font.Draw(batch, "Attack spells", 134, 56, Muted);
            font.Draw(batch, monster.SpecialRating > 0 ? "Damage reduced" : "No resistance", 134, 68, Ink);
            var defeats = monster.TypeId >= 1 && monster.TypeId <= Hero.MonsterDefeats.Length ? Hero.MonsterDefeats[monster.TypeId - 1] : 0;
            font.Draw(batch, Hero.MonsterDefeatHistoryComplete ? "Defeated" : "Recorded defeats", 134, 87, Muted);
            font.Draw(batch, defeats.ToString(), 134, 99, Ink);
            font.Draw(batch, monster.GatekeeperEmpowered ? "Empowered" : "Ratings are not", 134, 118,
                monster.GatekeeperEmpowered ? Accent : Muted);
            font.Draw(batch, monster.GatekeeperEmpowered ? "Attack / defense boosted" : "damage per hit.", 134, 130, Muted);
        }

        Fill(new(24, 147, 272, 1), Muted * .3f);
        if (touchEnabled)
        {
            for (var row = 0; row < current.Options.Length; row++)
            {
                var bounds = new Rectangle(24 + row * 138, 141, 130, 17);
                DrawTouchButton(bounds, current.Options[row].Label.ToUpperInvariant(), TouchConfirm);
                TouchChoice(bounds, row);
            }
            DrawTouchNavigation(horizontal: true);
            return;
        }
        for (var row = 0; row < current.Options.Length; row++)
        {
            var selected = current.Selected == row;
            font.Draw(batch, (selected ? "> " : "  ") + current.Options[row].Label,
                24, 152 + row * 12, selected ? Accent : Ink);
        }
        font.Draw(batch, "LEFT/RIGHT: ENEMY", 194, 164, Muted);

        void Stat(string label, string value, int y)
        {
            font.Draw(batch, label, 134, y, Muted);
            font.Draw(batch, value, 296 - value.Length * 6, y, Ink);
        }
    }

    private void DrawCombatMeter(int x, string label, float value, int maximum, float trailing, Color color)
    {
        font.Draw(batch, $"{label} {Math.Max(0, (int)MathF.Round(value))}/{maximum}", x, 83, Ink);
        DrawCombatBar(new(x, 93, 84, 3), value, maximum, trailing, color);
    }
    private void DrawCombatBar(Rectangle bounds, float value, int maximum, float trailing, Color color)
    {
        Fill(bounds, new Color(52, 59, 73));
        if (maximum <= 0) return;
        var trailWidth = (int)MathF.Round(bounds.Width * Math.Clamp(Math.Max(value, trailing) / maximum, 0, 1));
        var width = (int)MathF.Round(bounds.Width * Math.Clamp(value / maximum, 0, 1));
        if (trailWidth > 0) Fill(new(bounds.X, bounds.Y, trailWidth, bounds.Height), CombatCoral);
        if (width > 0) Fill(new(bounds.X, bounds.Y, width, bounds.Height), color);
    }
    private static Rectangle CombatBounds(int actor) => actor == 0 ? new(32, 31, 32, 32) : new(90 + (actor - 1) * 68, 21, 48, 48);

    private void DrawCombatActors(Overlay current, CombatEncounter battle)
    {
        var p = current.Presentation; var time = current.CombatTime;
        for (var actor = 0; actor < 4; actor++)
        {
            var before = p?.Before[actor] ?? (actor == 0 ? new CombatActor(0, "YOU", Hero.Health, Hero.MaximumHealth)
                : new CombatActor(battle.Monsters[actor - 1].TypeId, battle.Monsters[actor - 1].Name,
                    battle.Monsters[actor - 1].Health, battle.Monsters[actor - 1].MaximumHealth,
                    battle.Monsters[actor - 1].GatekeeperEmpowered));
            var after = p?.After[actor] ?? before;
            var shown = p is not null && time >= CombatPresentation.Impact + .12 ? after : before;
            var leaving = actor > 0 && before.Type > 0 && after.Type == 0;
            if (leaving && time < .65) shown = before;
            if (actor > 0 && shown.Type is < 1 or > 44) continue;
            var bounds = CombatBounds(actor);
            var frame = WorldRenderer.MonsterIdleFrame(shown.Type,
                Math.Min(time, p?.Duration ?? time) + actor * .37);
            if (actor > 0 && p is not null)
            {
                var counterAge = time - p.CounterAt(actor - 1);
                var damageAge = time - CombatPresentation.Impact;
                if (before.Health > 0 && after.Health <= 0 && damageAge >= 0)
                    frame = 12 + Math.Clamp((int)(damageAge / .14), 0, 3);
                else if (p.DamageToMonsters[actor - 1] > 0 && damageAge is >= 0 and < .28) frame = 11;
                else if (counterAge is >= -.18 and < .3)
                    frame = counterAge < -.04 ? 8 : counterAge < .1 ? 9 : 10;
            }
            var alpha = 1f;
            var hitAge = double.PositiveInfinity;
            if (p is not null)
            {
                if (actor == 0)
                {
                    if (p.Action == CombatAction.Attack || p.Action == CombatAction.Ability && p.Ability == SpecialAbility.Backstab)
                        bounds.X += Lunge(time);
                    for (var slot = 0; slot < 3; slot++)
                        if (p.DamageFromMonsters[slot] > 0 && time >= p.CounterAt(slot)) hitAge = time - p.CounterAt(slot);
                }
                else
                {
                    bounds.X -= Lunge(time - p.CounterAt(actor - 1) + CombatPresentation.Impact);
                    if (p.DamageToMonsters[actor - 1] > 0) hitAge = time - CombatPresentation.Impact;
                }
                if (hitAge is >= 0 and < .18) bounds.X += (actor == 0 ? -1 : 1) * (1 + (int)(3 * (1 - hitAge / .18)));
                if (leaving) { bounds.X += (int)(Math.Clamp(time - .18, 0, .5) * 28); alpha = (float)Math.Clamp((.65 - time) / .35, 0, 1); }
                if (before.Health > 0 && after.Health <= 0 && time > (actor == 0 ? p.LastImpact : CombatPresentation.Impact) + .14)
                    alpha = (float)Math.Clamp(1 - (time - (actor == 0 ? p.LastImpact : CombatPresentation.Impact) - .14)
                        / (world.HasMonsterAnimation(before.Type) ? .65 : .4), .12, 1);
                if (actor == 0 && p.Action == CombatAction.Ability && p.Ability == SpecialAbility.Invisibility && time < .65)
                    alpha *= .35f + .65f * (float)Math.Clamp(time / .65, 0, 1);
            }
            if (before.Health <= 0) alpha = .12f;
            DrawActor(Color.White * alpha);
            if (hitAge is >= 0 and < .14)
            {
                batch.End(); batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
                DrawActor(Color.White * (.8f * (1 - (float)hitAge / .14f)));
                batch.End(); batch.Begin(samplerState: SamplerState.PointClamp);
            }
            var fixedBounds = CombatBounds(actor);
            var hp = p?.HealthAt(actor, time) ?? shown.Health;
            var label = actor == 0 ? "YOU" : $"{Math.Max(0, (int)MathF.Round(hp))}/{shown.MaximumHealth}";
            font.Draw(batch, label, fixedBounds.Center.X - label.Length * 3, 65, Ink);
            if (actor > 0) DrawCombatBar(new(fixedBounds.Center.X - 25, 74, 50, 3), hp, shown.MaximumHealth,
                p?.HealthAt(actor, time - .2) ?? hp, CombatGreen);
            if (CombatPreview(current, battle, actor)) CombatBrackets(new(fixedBounds.Center.X - 22, 30, 44, 32), Accent);
            if (p is null) continue;
            if (actor > 0 && p.DamageToMonsters[actor - 1] > 0)
                DrawCombatNumber(p.DamageToMonsters[actor - 1].ToString(), fixedBounds.Center.X, time - CombatPresentation.Impact, Ink, true);
            if (actor == 0)
            {
                for (var slot = 0; slot < 3; slot++)
                    if (double.IsFinite(p.CounterAt(slot))) DrawCombatNumber(p.DamageFromMonsters[slot] > 0
                        ? p.DamageFromMonsters[slot].ToString() : "NO DAMAGE", fixedBounds.Center.X, time - p.CounterAt(slot), CombatCoral, p.DamageFromMonsters[slot] > 0);
                if (p.Healing > 0) DrawCombatNumber("+" + p.Healing, fixedBounds.Center.X, time - CombatPresentation.Impact, CombatGreen, false);
            }
            void DrawActor(Color tint)
            {
                if (actor == 0) town.DrawCombatHero(batch, bounds, tint);
                else if (shown.Type == 41 && shown.GatekeeperEmpowered)
                    world.DrawEmpoweredGatekeeper(batch, bounds, frame, facingLeft: true, tint);
                else world.DrawAnimatedMonster(batch, shown.Type, bounds, frame, facingLeft: true, tint);
            }
        }
        if (p is not null) DrawCombatAction(p, time);
    }

    private static int Lunge(double time) => time is >= 0 and < .36 ? (int)(Math.Sin(time / .36 * Math.PI) * 6) : 0;
    private void CombatBrackets(Rectangle bounds, Color color)
    {
        foreach (var x in new[] { bounds.Left, bounds.Right })
            foreach (var y in new[] { bounds.Top, bounds.Bottom })
            {
                Fill(new(x == bounds.Left ? x : x - 4, y, 5, 1), color);
                Fill(new(x, y == bounds.Top ? y : y - 4, 1, 5), color);
            }
    }
    private void DrawCombatNumber(string number, int center, double age, Color color, bool sparks)
    {
        if (age is < 0 or >= .46) return;
        var fade = (float)Math.Clamp((.46 - age) / .13, 0, 1);
        if (sparks && age < .22)
        {
            var spread = 3 + (int)(age * 45);
            for (var dx = -1; dx <= 1; dx += 2)
                for (var dy = -1; dy <= 1; dy += 2)
                    Fill(new(center + dx * spread, 46 + dy * spread / 2, 1, 1), color * fade);
        }
        var x = Math.Max(24, center - number.Length * 3); var y = 46 - (int)(age * 24);
        font.Draw(batch, number, x - 1, y, Background * fade); font.Draw(batch, number, x + 1, y, Background * fade);
        font.Draw(batch, number, x, y - 1, Background * fade); font.Draw(batch, number, x, y + 1, Background * fade);
        font.Draw(batch, number, x, y, color * fade);
    }

    private Point CombatShake()
    {
        if (!screenShake || paused || overlay is not { Presentation: { } p } current) return Point.Zero;
        var age = p.DamageToMonsters.Any(d => d > 0) ? current.CombatTime - CombatPresentation.Impact : 1;
        for (var slot = 0; slot < 3; slot++)
            if (p.DamageFromMonsters[slot] > 0 && current.CombatTime >= p.CounterAt(slot)) age = current.CombatTime - p.CounterAt(slot);
        if (age is < 0 or >= .16) return Point.Zero;
        return ((int)(age * 40) % 4) switch
        { 0 => new(1, 0), 1 => new(-1, 1), 2 => new(1, -1), _ => new(-1, 0) };
    }
}
