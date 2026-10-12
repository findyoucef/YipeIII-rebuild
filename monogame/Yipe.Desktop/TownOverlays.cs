using Microsoft.Xna.Framework;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private Player Hero => session.Player;
    private bool pendingNpcCelebration;
    private string StatusLine => $"HP {Hero.Health}/{Hero.MaximumHealth}  SP {Hero.SpellPoints}/{Hero.MaximumSpellPoints}  AP {Hero.AbilityPoints}/{Hero.MaximumAbilityPoints}\nLevel {Hero.Level}   Gold {Hero.Gold}";
    private string LocationName() => session.CurrentMap.Id switch
    {
        17 => "Maynard", 8 => "Quizilog", 27 => "Ervyn", 117 => "Yipe Hell",
        _ => session.Location.MapLayer == 0 ? "Yipe" : session.CurrentMap.Environment switch
        { 1 => "Earth Cave", 2 => "Stone Cave", 3 => "Fire Cave", 4 => "Zephyr Cave", 5 => "Water Cave", 6 => "Final Cave", _ => "Cave" },
    };

    private void StartCommand(IEnumerable<AdventureEvent> events)
    {
        if (command is not null) throw new InvalidOperationException("Finish the current interaction first.");
        command = events.GetEnumerator();
        AdvanceCommand();
    }
    private void AdvanceCommand()
    {
        overlay = null;
        if (command is null) return;
        pendingEvent = null;
        pendingNpcCelebration = false;
        var rewardsBefore = (Hero.Gold, Hero.HasLuckyRabbitFoot, Hero.QuestScore, Hero.WeaponRank, Hero.ArmorRank);
        while (command.MoveNext())
        {
            if (command.Current.Kind == AdventureEventKind.Sound) { audio.Play(command.Current.SoundId); continue; }
            pendingEvent = command.Current; break;
        }
        pendingNpcCelebration = pendingEvent?.Kind == AdventureEventKind.King
            && rewardsBefore != (Hero.Gold, Hero.HasLuckyRabbitFoot, Hero.QuestScore, Hero.WeaponRank, Hero.ArmorRank);
        if (pendingEvent is null) { command.Dispose(); command = null; }
        SynchronizeWorld(animate: true);
        if (!player.IsMoving && !world.MonstersMoving && pendingEvent is not null)
        {
            var next = pendingEvent; pendingEvent = null;
            ShowInteraction(next);
        }
    }
    private void SynchronizeWorld(bool animate)
    {
        journal.Observe(adventure);
        var changed = !ReferenceEquals(renderedMap, session.CurrentMap);
        if (changed)
        {
            audio.StopFootsteps();
            world.ClearSnowTracks();
            renderedMap = session.CurrentMap;
            if (renderedMap.Id == TownMap.Id)
            {
                map = new TownMap(renderedMap);
                var replacement = new TownRenderer(GraphicsDevice, map, contentDirectory, catalog);
                town.Dispose(); town = replacement;
            }
        }
        var position = session.Location.Position;
        if (world.SynchronizeRaft(adventure, player.Position, animate && !changed))
            player.CarryTo(position);
        if (!changed && animate && position != player.Position
            && Math.Abs(position.X - player.Position.X) <= 1 && Math.Abs(position.Y - player.Position.Y) <= 1 && !player.IsMoving)
            player.MoveTo(position, nextStride);
        else if (changed || position != player.Position || !animate)
        {
            player.SnapTo(position);
            walkingStep = false;
            iceDirection = Point.Zero;
            world.ClearSnowTracks();
            cameraPosition = CameraTarget(player.VisualPosition);
        }
        player.Update(0, world.RaftSupport(adventure, sceneTime));
        if (changed) pendingDirection = Point.Zero;
        var alerts = world.SynchronizeMonsters(adventure, animate && !changed,
            new((int)MathF.Round(cameraPosition.X), (int)MathF.Round(cameraPosition.Y), CanvasWidth, CanvasHeight));
        if (alerts.Roaming != 0) audio.MonsterVoice(alerts.Roaming);
        if (alerts.Pursuer != 0) audio.MonsterVoice(alerts.Pursuer);
        town.AlternateHero = Hero.Inventory[28] is >= 2 and <= 12 ? world.Character(Hero.Inventory[28]) : null;
    }

    private void UpdateOverlay(InputFrame actions)
    {
        var current = overlay!;
        if (actions.Back) { current.Back?.Invoke(); return; }
        if (current.Targeting && actions.Direction.X != 0)
        {
            var battle = adventure.Battle!;
            for (var distance = 1; distance <= 3; distance++)
            {
                var target = ((current.InspectedEnemy ?? battle.SelectedTarget) + actions.Direction.X * distance + 3) % 3;
                if (battle.Monsters[target].Health <= 0) continue;
                if (current.InspectedEnemy.HasValue) current.InspectedEnemy = target;
                else battle.SelectedTarget = target;
                break;
            }
            return;
        }
        if (actions.Direction.X != 0 && current.PageCount > 1)
        {
            current.Page = Math.Clamp(current.Page + actions.Direction.X, 0, current.PageCount - 1);
            return;
        }
        if (current.Options.Length == 1 && current.Page < current.PageCount - 1 && actions.Confirm)
        {
            current.Page++; return;
        }
        if (actions.Direction.Y != 0 && current.Options.Length != 0)
            current.Selected = (current.Selected + actions.Direction.Y + current.Options.Length) % current.Options.Length;
        if (actions.Confirm && current.Options.Length != 0) current.Options[current.Selected].Choose();
    }
    private void Notice(string title, string text, Action? done = null, bool combat = false, NpcActor npc = NpcActor.None, bool celebrate = false)
    {
        done ??= () => overlay = null;
        overlay = new(title, text, [new("Continue", done)], done) { Combat = combat, Npc = npc, NpcCelebrating = celebrate };
    }
    private void Choices(string title, string text, IEnumerable<OverlayOption> options, Action? back = null, bool combat = false, NpcActor npc = NpcActor.None) =>
        overlay = new(title, text, options, back) { Combat = combat, Npc = npc };

    private void ShowInteraction(AdventureEvent interaction)
    {
        audio.Play(interaction.SoundId);
        if (interaction.Kind == AdventureEventKind.Finale) RecordScores();
        switch (interaction.Kind)
        {
            case AdventureEventKind.Combat: ShowBattle(); break;
            case AdventureEventKind.Princess:
                Choices("Rescued Princess", "The Princess thanks you for rescuing her and offers you a gift.",
                    new[] { "Gold", "Experience", "Kiss", "Mystery" }.Select((name, index) => new OverlayOption(name, () =>
                    { interaction.Choice = index + 1; AdvanceCommand(); })), AdvanceCommand, npc: WorldRenderer.PrincessActor(interaction.Value));
                break;
            case AdventureEventKind.King:
                Notice(interaction.Title, interaction.Text, AdvanceCommand, npc: NpcActor.King, celebrate: pendingNpcCelebration);
                break;
            case AdventureEventKind.Message when interaction.Title == "Princess Reward":
                Notice(interaction.Title, interaction.Text, AdvanceCommand,
                    npc: WorldRenderer.PrincessActor(adventure.Map.Events.Princess.PortraitVariant), celebrate: true);
                break;
            case AdventureEventKind.WeaponShop: ShowEquipmentShop(EquipmentKind.Weapon); break;
            case AdventureEventKind.ArmorShop: ShowEquipmentShop(EquipmentKind.Armor); break;
            case AdventureEventKind.HealingShop: ShowHealingShop(); break;
            case AdventureEventKind.MiscellaneousShop:
                var increased = Shops.EnterMiscellaneous(Hero);
                if (increased) Notice("Shop", "The shop's potion prices have increased.", ShowMiscellaneousShop);
                else ShowMiscellaneousShop();
                break;
            case AdventureEventKind.MagicShop: ShowMagicShop(); break;
            case AdventureEventKind.AbilityTraining: ShowTraining(); break;
            case AdventureEventKind.Casino: dartsHighScore = 0; ShowCasino(); break;
            case AdventureEventKind.Gatekeeper:
                Choices(interaction.Title, interaction.Text + $"\nYou have {Hero.Gold} gold.", [
                    new("Pay the fee", () => { if (Hero.Gold < Quests.GatekeeperRecoveryFee(Hero)) Notice("Gatekeeper", "You do not have enough gold.", () => ShowInteraction(interaction)); else { interaction.Choice = 1; AdvanceCommand(); } }),
                    new("Fight", () => { interaction.Choice = 2; AdvanceCommand(); }),
                    new("Leave", AdvanceCommand)], AdvanceCommand);
                break;
            case AdventureEventKind.SecretTree:
                RequestText("Secret Tree", "Enter the secret code.", "", code => Notice("Secret Tree",
                    WorldActions.EnterTreeCode(Hero, code) == SecretTreeResult.RewardGranted
                        ? "You found a special ring! Your healing spells are stronger." : "Nothing happens.", AdvanceCommand), AdvanceCommand);
                break;
            default: Notice(interaction.Title, interaction.Text, AdvanceCommand); break;
        }
    }

    private void ShowActions() => Choices(Hero.Name, StatusLine, [
        new("Map", OpenMap), new("Inventory", () => ShowInventory(false)), new("Spells", () => ShowSpells(false)),
        new("Character and progress", ShowStatistics), new("Sound", ShowSoundSettings),
        new("Read sign", () => Notice("Location", adventure.LocationMessage ?? "There is no sign here.", ShowActions)), new("Other: investigate this location", () =>
        {
            var result = adventure.Other(); audio.Play(result.SoundId); SynchronizeWorld(false);
            Notice("Other", result.Message, () => overlay = null);
        }),
        new("Continue exploring", () => overlay = null), new("Pause menu", () => { overlay = null; OpenPausePage(PausePage.Main); })], () => overlay = null);

    private void ShowEquipmentShop(EquipmentKind kind)
    {
        var options = Shops.EquipmentStock(kind, session.CurrentMap.Id).Select(item => new OverlayOption(
            $"{itemNames.Equipment(Hero, kind, item.Rank)} - {item.Price}G", () =>
                Notice(kind + " Shop", Shops.PurchaseEquipment(Hero, kind, item.Rank) switch
                {
                    EquipmentTransactionStatus.Purchased => "Thank you for your purchase!",
                    EquipmentTransactionStatus.MustSellCurrent => "Sell your current equipment first.",
                    _ => "You do not have enough gold.",
                }, () => ShowEquipmentShop(kind)))).ToList();
        options.Insert(0, new($"Sell current {kind} - {Shops.SaleValue(Hero, kind)}G", () =>
        {
            void Sell() => Notice(kind + " Shop", Shops.SellEquipment(Hero, kind) == EquipmentTransactionStatus.Sold
                ? "Your equipment is sold." : "You have nothing to sell.", () => ShowEquipmentShop(kind));
            if (Shops.SaleRequiresConfirmation(Hero, kind))
                Choices("Sell equipment?", "You may not have enough gold to buy better equipment.", [new("Sell", Sell), new("Keep it", () => ShowEquipmentShop(kind))], () => ShowEquipmentShop(kind));
            else Sell();
        }));
        options.Add(new("Leave", AdvanceCommand));
        Choices(kind + " Shop", $"Gold {Hero.Gold}\nEquipped: {itemNames.Equipment(Hero, kind, kind == EquipmentKind.Weapon ? Hero.WeaponRank : Hero.ArmorRank)}", options, AdvanceCommand);
    }
    private void ShowMiscellaneousShop()
    {
        void Leave() { Shops.LeaveMiscellaneous(Hero); AdvanceCommand(); }
        var options = Shops.MiscellaneousStock(Hero, session.CurrentMap.Id).Select(item => new OverlayOption($"{item.Name} - {item.Price}G", () =>
            Notice("Shop", Shops.Purchase(Hero, item).Status switch
            {
                PurchaseStatus.Purchased => "Thank you for your purchase!", PurchaseStatus.InventoryFull => "Your inventory is full.",
                PurchaseStatus.AlreadyEquipped => "You already have this item or better equipment.", _ => "You do not have enough gold.",
            }, ShowMiscellaneousShop))).ToList();
        options.Add(new("Leave", Leave));
        Choices("General Store", $"Gold {Hero.Gold}", options, Leave);
    }
    private void ShowHealingShop()
    {
        var options = Shops.HealingServices.Select((service, index) => new OverlayOption($"{service.Name} - {service.Price}G", () =>
            ShowServiceResult(Shops.PurchaseHealing(Hero, index, session.Random), ShowHealingShop))).ToList();
        options.Add(new("Leave", AdvanceCommand));
        Choices("Healing Shop", StatusLine, options, AdvanceCommand);
    }
    private void ShowServiceResult(ServiceResult result, Action back) => Notice("Healing", result.Status switch
    {
        ServiceStatus.Completed => $"Restored {result.Amount} points.", ServiceStatus.AlreadyFull => "You are already fully restored.", _ => "You do not have enough gold.",
    }, back);
    private void ShowMagicShop()
    {
        var options = new List<OverlayOption> {
            new("Restore some SP - 50G", () => ShowServiceResult(Shops.PurchaseSpellHealing(Hero, false, session.Random), ShowMagicShop)),
            new("Restore all SP - 250G", () => ShowServiceResult(Shops.PurchaseSpellHealing(Hero, true, session.Random), ShowMagicShop)) };
        if (Shops.NextSpell(Hero) is { } offer)
            options.Add(new($"Learn {offer.Name} - {offer.Price}G", () => Choices(offer.Name, offer.Description, [
                new("Learn spell", () => Notice("Magic Shop", Shops.LearnNextSpell(Hero) == SpellLearningStatus.Learned
                    ? "You learned a new spell!" : "You do not have enough gold.", ShowMagicShop)), new("Back", ShowMagicShop)], ShowMagicShop)));
        options.Add(new("Leave", AdvanceCommand));
        Choices("Magic Shop", StatusLine, options, AdvanceCommand);
    }
    private void ShowTraining()
    {
        if (Shops.AbilityForEnvironment(adventure.Map.Environment) is not { } offer)
        { Notice("Training", "No training is available here.", AdvanceCommand, npc: NpcActor.Teacher); return; }
        Choices("Ability Training", offer.Description + $"\nGold {Hero.Gold}", [new($"Learn {offer.Name} - {offer.Price}G", () =>
        {
            var result = Shops.TrainAbility(Hero, adventure.Map.Environment);
            Notice("Training", result switch
            { AbilityTrainingStatus.Learned => "Your training is complete.", AbilityTrainingStatus.AlreadyTrained => "You already know this ability.", _ => "You do not have enough gold." },
                ShowTraining, npc: NpcActor.Teacher, celebrate: result == AbilityTrainingStatus.Learned);
        }), new("Leave", AdvanceCommand)], AdvanceCommand, npc: NpcActor.Teacher);
    }

    private void ShowInventory(bool combat)
    {
        void Back() { if (combat) ShowBattle(); else ShowActions(); }
        var options = Enumerable.Range(0, Player.CarriedInventorySize).Where(slot => Hero.Inventory[slot] != 0).Select(slot => new OverlayOption(
            ItemNames.Consumable(Hero.Inventory[slot]), () => Choices(ItemNames.Consumable(Hero.Inventory[slot]), "Use or discard this item?", [
                new("Use", () =>
                {
                    if (combat)
                    {
                        var presentation = new CombatPresentation(Hero, adventure.Battle!, CombatAction.Item);
                        var item = Hero.Inventory[slot];
                        var result = adventure.UseCombatItem(slot);
                        if (result.Turn is { } turn)
                        {
                            var healing = item is 1 or 2 ? Math.Min(result.Item.Amount,
                                presentation.Before[0].MaximumHealth - presentation.Before[0].Health) : item == 9 ? result.Item.Amount : 0;
                            presentation.Complete(Hero, adventure.Battle!, turn, healing: healing, sound: result.Item.SoundId);
                            ShowTurn(turn, presentation, result.Item.Message);
                        }
                        else Notice("Item", result.Item.Message, () => ShowInventory(true), true);
                    }
                    else
                    {
                        var previousMap = adventure.Map;
                        var result = adventure.UseExplorationItem(slot);
                        audio.Play(result.SoundId); SynchronizeWorld(false);
                        var message = result.Message + (result.Amount != 0 ? $" ({result.Amount})" : "");
                        Notice("Item", message, () =>
                        {
                            if (!ReferenceEquals(previousMap, adventure.Map)) audio.Play(201);
                            ShowInventory(false);
                        });
                    }
                }),
                new("Discard", () => { Hero.Inventory[slot] = 0; ShowInventory(combat); }),
                new("Back", () => ShowInventory(combat))], () => ShowInventory(combat), combat))).ToList();
        options.Add(new("Back", Back));
        Choices("Inventory", combat ? "" : StatusLine, options, Back, combat);
    }
    private void ShowSpells(bool combat)
    {
        void Back() { if (combat) ShowBattle(); else ShowActions(); }
        var options = Enumerable.Range(0, Spells.Definitions.Count).Where(i => Hero.Spells[i] != 0).Select(index => new OverlayOption(Spells.Definitions[index].Name, () =>
        {
            void Cast(SpellPower power)
            {
                if (combat)
                {
                    var presentation = new CombatPresentation(Hero, adventure.Battle!, CombatAction.Spell, index, power);
                    var result = adventure.CastCombat(index, power);
                    if (result.Status == SpellCastStatus.Cast)
                    {
                        presentation.Complete(Hero, adventure.Battle!, result.Turn, result.DamageBySlot, result.Healing, AudioCatalog.Spell(index));
                        ShowTurn(result.Turn, presentation, Spells.Definitions[index].Name + (result.Healing > 0 ? $": restored {result.Healing} HP." : "."));
                    }
                    else Notice("Spell", SpellFailure(result.Status), () => ShowSpells(true), true);
                }
                else
                {
                    var result = adventure.CastExploration(index, power);
                    if (result.Status == SpellCastStatus.Cast) audio.Play(AudioCatalog.Spell(index));
                    SynchronizeWorld(false);
                    Notice("Spell", result.Status == SpellCastStatus.Cast ? result.LocationChanged ? "You arrive safely." : result.Healing > 0 ? $"Restored {result.Healing} HP." : "You are surrounded by a terrible smell." : SpellFailure(result.Status), () => overlay = null);
                }
            }
            Choices(Spells.Definitions[index].Name, combat ? CombatSpellHint(index) : Spells.Definitions[index].Description, [
                new($"Low power - {Spells.PointCost(Hero, index, SpellPower.Low)} SP", () => Cast(SpellPower.Low)),
                new($"High power - {Spells.PointCost(Hero, index, SpellPower.High)} SP", () => Cast(SpellPower.High)),
                new("Back", () => ShowSpells(combat))], () => ShowSpells(combat), combat);
            if (combat) { overlay!.PreviewSpell = index; overlay.Targeting = index is 2 or 3 or 6 or 7; }
        })).ToList();
        options.Add(new("Back", Back));
        Choices("Spells", combat ? "" : StatusLine, options, Back, combat);
    }
    private static string SpellFailure(SpellCastStatus status) => status switch
    {
        SpellCastStatus.InsufficientSpellPoints => "You do not have enough spell points.",
        SpellCastStatus.WrongContext => "You cannot cast that spell here.", SpellCastStatus.Unlearned => "You have not learned that spell.",
        _ => "This spell cannot be used right now.",
    };
    private void ShowBattle()
    {
        overlay = new("Combat", "", [new("Attack", () =>
            {
                var presentation = new CombatPresentation(Hero, adventure.Battle!, CombatAction.Attack);
                var turn = adventure.Attack();
                presentation.Complete(Hero, adventure.Battle!, turn, sound: turn.PlayerDamage > 0 ? 130 : 0);
                ShowTurn(turn, presentation);
            }),
            new($"Ability: {Hero.SpecialAbility}", () =>
            {
                if (Hero.SpecialAbility == SpecialAbility.None) Notice("Ability", "You have not learned a special ability.", ShowBattle, true);
                else if (Hero.AbilityPoints < 3) Notice("Ability", "You need three ability points.", ShowBattle, true);
                else
                {
                    var before = Hero.AbilityPoints;
                    var presentation = new CombatPresentation(Hero, adventure.Battle!, CombatAction.Ability);
                    var turn = adventure.UseAbility();
                    if (Hero.AbilityPoints == before) Notice("Ability", "Your ability cannot take effect right now.", ShowBattle, true);
                    else
                    {
                        presentation.Complete(Hero, adventure.Battle!, turn, sound: AudioCatalog.Ability(Hero.SpecialAbility));
                        ShowTurn(turn, presentation, $"You use {Hero.SpecialAbility}.");
                    }
                }
            }),
            new("Spells", () => ShowSpells(true)), new("Inventory", () => ShowInventory(true)),
            new("Enemy info", ShowEnemyInfo)], () =>
            {
                var presentation = new CombatPresentation(Hero, adventure.Battle!, CombatAction.Flee);
                var result = adventure.Flee();
                presentation.Complete(Hero, adventure.Battle!, new CombatTurn { MonsterDamage = result.MonsterDamage,
                    MonsterDamageBySlot = result.MonsterDamageBySlot, Outcome = result.PlayerDefeated ? CombatOutcome.Defeat : CombatOutcome.Ongoing }, escaped: result.Escaped);
                ShowCombatResult("Flee", result.Escaped ? "You escaped!" : $"You could not escape. You took {result.MonsterDamage} damage.",
                    adventure.BattleResult == BattleResolution.Ongoing ? ShowBattle : AdvanceCommand, presentation,
                    adventure.BattleResult == BattleResolution.Ongoing);
            }) { Combat = true, Targeting = true };
    }
    private void ShowEnemyInfo()
    {
        var parent = overlay!;
        var battle = adventure.Battle!;
        var inspected = battle.SelectedTarget;
        if (battle.Monsters[inspected].Health <= 0) inspected = Array.FindIndex(battle.Monsters, monster => monster.Health > 0);
        if (inspected < 0) return;
        void Back() => overlay = parent;
        void ToggleDetails()
        {
            var current = overlay!;
            current.EnemyDetails = !current.EnemyDetails;
            current.Options[0] = new(current.EnemyDetails ? "Stats" : "Details", ToggleDetails);
        }
        overlay = new("Enemy info", "", [new("Details", ToggleDetails), new("Back", Back)], Back)
        { Combat = true, Targeting = true, InspectedEnemy = inspected };
    }

    private void ShowTurn(CombatTurn turn, CombatPresentation presentation, string text = "")
    {
        text += $"\nYou dealt {turn.PlayerDamage} damage.\nYou took {turn.MonsterDamage} damage.";
        if (turn.Outcome == CombatOutcome.Victory)
        {
            text += $"\nGold +{turn.Reward.Gold}   XP +{turn.Reward.Experience}";
            if (turn.Reward.EquipmentUpgrade != EquipmentUpgrade.None) text += $"\nYour {Combat.EquipmentUpgradeName(turn.Reward.EquipmentUpgrade)} was upgraded!";
            if (turn.Reward.UnderworldKey) text += "\nYou found the Underworld Key!";
            if (turn.LevelUp.LeveledUp) RecordScores();
            if (turn.LevelUp.LeveledUp) text += $"\nLevel {Hero.Level}! HP +{turn.LevelUp.HealthIncrease}, SP +{turn.LevelUp.SpellPointIncrease}.";
        }
        ShowCombatResult(turn.Outcome == CombatOutcome.Victory ? "Victory" : "Combat", text.Trim(),
            adventure.BattleResult == BattleResolution.Ongoing ? ShowBattle : AdvanceCommand, presentation,
            turn.Outcome == CombatOutcome.Ongoing);
    }

    private void ShowSoundSettings()
    {
        soundMenuParent = overlay;
        overlay = null;
        OpenPausePage(PausePage.Audio);
    }

    private void ShowStatistics()
    {
        var text = $"{StatusLine}\nWC {Combat.PlayerAttackEquipmentRating(Hero)}  AC {Combat.PlayerDefenseEquipmentRating(Hero)}\nCarried items {Hero.Inventory.Take(Player.CarriedInventorySize).Count(item => item != 0)}/{Player.CarriedInventorySize}\nCurrent ability: {Hero.SpecialAbility}\nStat limit {Hero.MaximumStat}\nStrength {Hero.Strength}  Dexterity {Hero.Dexterity}\nIntelligence {Hero.Intelligence}  XP {Hero.Experience}\n{itemNames.Equipment(Hero, EquipmentKind.Weapon, Hero.WeaponRank)}\n{itemNames.Equipment(Hero, EquipmentKind.Armor, Hero.ArmorRank)}\nBoots {Hero.BootsRank}  Gloves {Hero.GlovesRank}\nHelmet {Hero.HelmetRank}  Ring {Hero.RingRank}  Necklace {Hero.NecklaceRank}\nRescues {Hero.RescuedPrincessCount}/20  Stamps {Hero.StampsFound}/6\nDeaths {Hero.DeathCount}\nRainbow Key: {Hero.HasRainbowKey}\nUnderworld Key: {Hero.HasUnderworldKey}\nClimbing Shoes: {Hero.HasTreeClimbingShoes}\nWaterproof Spray: {Hero.HasWaterproofShoeSpray}\nLucky Rabbit Foot: {Hero.HasLuckyRabbitFoot}\nBonus Healing Ring: {Hero.HasBonusHealingRing}";
        Choices("Character", text, [new("Monster history", ShowMonsterHistory), new(Hero.SillyMode ? "Use classic names" : "Use silly names", () => { Hero.SillyMode = !Hero.SillyMode; ShowStatistics(); }), new("Equipment", () => Notice("Equipment", $"AC = Armor Class; WC = Weapon Class\n\nWeapon: {itemNames.Equipment(Hero, EquipmentKind.Weapon, Hero.WeaponRank)} (AC 0, WC {Hero.WeaponRank})\nBody Armor: {itemNames.Equipment(Hero, EquipmentKind.Armor, Hero.ArmorRank)} (AC {Hero.ArmorRank}, WC 0)\n" + ItemNames.Accessories(Hero), ShowStatistics)), new("High scores", () => { RecordScores(); ShowHighScores(Hero.Difficulty); }), new("Score code", ShowScoreCode), new("Back", ShowActions)], ShowActions);
    }

    private void ShowMonsterHistory()
    {
        var total = 0;
        foreach (var count in Hero.MonsterDefeats.Take(41)) total = unchecked(total + count);
        var text = $"Kills: {total}  Deaths: {Hero.DeathCount}\n\n";
        if (!Hero.MonsterDefeatHistoryComplete)
            text += "Older save: all kill counts shown start at the statistics upgrade. Earlier per-type counts are unavailable.\n\n";
        text += string.Join("\n", CombatCatalog.Monsters.Select(monster =>
        {
            var count = Hero.MonsterDefeats[monster.TypeId - 1];
            return $"{(count > 0 ? monster.Name : "?????")}: {count}";
        }));
        Notice("Monster History", text, ShowStatistics);
    }

    private bool SavePreferences()
    {
        profile.Data.Sound = audio.Enabled; profile.Data.Volume = audio.Volume;
        profile.Data.EffectsVolume = audio.EffectsVolume;
        profile.Data.Scanlines = scanlines; profile.Data.Miniature = miniatureMode;
        profile.Data.Vignette = vignette;
        profile.Data.PixelLighting = pixelLighting;
        profile.Data.SunShaftStrength = sunShaftStrength;
        profile.Data.CloudShadowStrength = cloudShadowStrength;
        profile.Data.RainStrength = rainStrength;
        profile.Data.ScreenShake = screenShake;
        profile.Data.TimeOfDayMode = timeOfDayMode;
        profile.Data.DayNightSeconds = dayNightSeconds;
        try { profile.Save(); menuMessage = "Preferences saved."; return true; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { Console.Error.WriteLine(error.Message); menuMessage = "Preferences could not be saved.\nChanges apply for this session."; return false; }
    }
    private void RecordScores()
    {
        var previous = (HighScoreEntry[])profile.Data.Scores.Clone();
        if (!HighScores.Update(profile.Data.Scores, Hero, Hero.GatekeeperState == 3)) return;
        try { profile.Save(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            profile.Data.Scores = previous;
            Console.Error.WriteLine(error.Message); menuMessage = "High scores could not be saved.";
        }
    }
    private void ShowHighScores(int difficulty)
    {
        string[] categories = ["Most Kills", "Most Princesses", "Most Cash at One Time", "Highest Level", "First Three Finishers"];
        var group = HighScores.GroupOffset(difficulty);
        var text = string.Join("\n\n", categories.Select((category, index) => category + "\n" + string.Join("\n",
            profile.Data.Scores.Skip(group + index * 3).Take(3).Select((entry, rank) => $"{rank + 1}. {entry.Name}" + (index < 4 ? $" - {entry.Value}" : "")))));
        Choices("High Scores - " + new[] { "Easy", "Medium", "Hard" }[group / 15], text, [
            new("Next difficulty", () => ShowHighScores((group / 15 + 1) % 3 + 1)),
            new("Score code", ShowScoreCode), new("Back", ShowStatistics)], ShowStatistics);
    }
    private void ShowScoreCode() => Notice("High Score Code", Hero.Name + "\n\n" + HighScores.Code(Hero)
        + (Hero.MonsterDefeatHistoryComplete ? "" : "\n\nThis older save contains only part of your defeat history. Earlier defeats are not included in this code."), ShowStatistics);

    private void DrawOverlay()
    {
        var current = overlay!;
        Fill(new(0, 0, CanvasWidth, CanvasHeight), Color.Black * .45f);
        DrawPanel(new(12, 5, 296, 170));
        if (current.Combat && adventure.Battle is { } battle) { DrawCombatOverlay(current, battle); return; }
        font.Draw(batch, current.Title, 24, 14, Accent, current.Title.Length <= 22 ? 2 : 1);
        var textY = current.Npc == NpcActor.None ? 37 : 64;
        if (current.Npc != NpcActor.None)
        {
            world.DrawNpcPortrait(batch, current.Npc, new(246, 6, 48, 48), current.NpcTime, current.NpcCelebrating);
            Fill(new(24, 56, 272, 1), Muted * .4f);
        }
        for (var i = 0; i < current.LinesPerPage; i++)
        {
            var index = current.Page * current.LinesPerPage + i;
            if (index >= current.Lines.Length) break;
            font.Draw(batch, current.Lines[index], 24, textY + i * 10, Ink);
        }
        if (current.Options.Length == 1 && current.Page < current.PageCount - 1)
            font.Draw(batch, $"Continue  {current.Page + 1}/{current.PageCount}", 24, 154, Accent);
        else
        {
            var remainingLines = Math.Min(current.LinesPerPage, current.Lines.Length - current.Page * current.LinesPerPage);
            var startY = current.Options.Length > 1 ? textY + 10 + remainingLines * 10 : 150;
            var visible = Math.Min(current.Options.Length, (159 - startY) / 11 + 1);
            var first = Math.Clamp(current.Selected - visible / 2, 0, Math.Max(0, current.Options.Length - visible));
            for (var row = 0; row < visible; row++)
            {
                var index = first + row;
                var y = startY + row * 11;
                if (index == current.Selected) Fill(new(22, y - 2, 275, 11), new Color(64, 73, 87));
                var label = current.Options[index].Label;
                // Long labels are shown in full by their confirmation/details screen.
                font.Draw(batch, (index == current.Selected ? "> " : "  ") + (label.Length > 43 ? label[..40] + "..." : label), 24, y, index == current.Selected ? Accent : Ink);
            }
            if (current.PageCount > 1 && current.Options.Length > 1) font.Draw(batch, $"< Page {current.Page + 1}/{current.PageCount} >", 24, 164, Muted);
            if (current.Options.Length > visible) font.Draw(batch, $"{current.Selected + 1}/{current.Options.Length}", 268, 164, Muted);
        }
        if (current.FirstPortrait > 0)
            world.DrawPortrait(batch, current.FirstPortrait + current.Selected, new(223, 87, 64, 64));
    }
}
