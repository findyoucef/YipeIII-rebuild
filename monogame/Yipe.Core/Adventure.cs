namespace Yipe.Core;

public enum AdventureEventKind
{
    Sound, Message, Princess, Combat, WeaponShop, ArmorShop, HealingShop, MiscellaneousShop,
    MagicShop, Casino, AbilityTraining, King, Gatekeeper, SecretTree, Finale,
}

/// <summary>An interaction pauses the command iterator until the frontend has handled it.</summary>
public sealed record AdventureEvent(AdventureEventKind Kind, string Title, string Text = "", int SoundId = 0, int Value = 0)
{
    public int Choice { get; set; }
}
public enum BattleResolution { Ongoing, Victory, Defeat, Fled }

/// <summary>Original app event ordering, independent of rendering and wall-clock time.</summary>
public sealed class Adventure
{
    private readonly List<ushort> temporaryDefeats = new();
    private readonly ExplorationTimer timer = new();
    private Map preparedMap = null!;
    private RoamingMonster? roaming;
    public GameSession Session { get; }
    public Player Player => Session.Player;
    public Map Map => Session.CurrentMap;
    public WorldLocation Location => Session.Location;
    public MapVisibility Visibility { get; } = new();
    public RoamingMonster? Roaming => roaming;
    public PursuingMonster? Pursuer { get; private set; }
    public CombatEncounter? Battle { get; private set; }
    public BattleResolution BattleResult { get; private set; }
    public IReadOnlyList<MonsterPlacement> ActivePlacements => MonsterPlacements.ForMap(Map.Id, Player.Difficulty, Player.Level)
        .Where(p => !temporaryDefeats.Contains(p.PlacementIndex)
            && (p.RespawnsAfterMapReload || !Session.DefeatedMonsterPlacements.Contains(p.PlacementIndex))).ToArray();

    public Adventure(GameSession session)
    {
        Session = session;
        PrepareMap();
    }
    private void PrepareMap()
    {
        if (ReferenceEquals(preparedMap, Map)) return;
        preparedMap = Map;
        temporaryDefeats.Clear();
        roaming = null;
        Pursuer = Encounters.PursuerForMap(Map, Player.Level);
        Visibility.Initialize(Map, Location.Position);
    }
    private static AdventureEvent Notice(string title, string text, int sound = 0) => new(AdventureEventKind.Message, title, text, sound);
    public IEnumerable<AdventureEvent> Resume()
    {
        if (Map.Id == 0x11 && Quests.CompleteGatekeeperFinale(Player)) yield return Finale();
    }
    private static AdventureEvent Finale() => new(AdventureEventKind.Finale, "Victory",
        "You've rescued all the princesses!!\n\nYou are the hero of the entire world!!", 129);

    public IEnumerable<AdventureEvent> Move(int dx, int dy, int localHour = 12)
    {
        if (Battle is not null) throw new InvalidOperationException("Finish the battle before moving.");
        var previous = Location.Copy();
        var result = Session.Move(dx, dy);
        var changedMap = result.Transition == MapTransition.Changed;
        PrepareMap();
        if (result.OriginalCode == 0x12 && result.PositionChanged) yield return new(AdventureEventKind.Sound, "", SoundId: 131);
        else if (result.OriginalCode == 0x1b && result.PositionChanged) yield return new(AdventureEventKind.Sound, "", SoundId: 142);
        else if (result.OriginalCode == 0x16 && changedMap) yield return new(AdventureEventKind.Sound, "", SoundId: 201);
        if (result.PositionChanged && result.OriginalCode is not (0x16 or 0x1b))
        {
            Visibility.RevealFrom(Map, Location.Position);
            if (changedMap && Map.Id == 0x11 && Quests.CompleteGatekeeperFinale(Player)) yield return Finale();
            if (result.OriginalCode == 3)
            {
                var tile = Map.Tile(Location.Position);
                if (tile == 0x26) yield return Notice("Water Hole", "You are standing on a water hole. Use Other to drink.");
                else if (tile == 0x42) yield return Notice("Underworld Tunnel", "You find a special tunnel leading down into the depths of the underworld. Use Other to travel to Yipe Hell.");
                else if (tile is 0x56 or 0x57)
                {
                    yield return Notice("Tree", "You climb over the tree using your special shoes.");
                    if (Map.Id == 1 && !Player.HasBonusHealingRing) yield return new(AdventureEventKind.SecretTree, "Secret Tree");
                }
                else if (WorldActions.MovementHint(Player, Map.Id) is { Length: > 0 } hint)
                    yield return Notice("Location", hint + (Map.Id is 3 or 0x12 or 0x45 ? "\n\nUse Other to investigate." : ""));
            }
            if (Map.Events.Message is { HasPosition: true } message && message.Position == Location.Position)
                yield return Notice("Location", LocationMessages[message.Value - 1]);
            if (Map.Events.Princess is { HasPosition: true } princess && princess.Position == Location.Position
                && Player.InventoryValues[princess.RescueIndex] == 0)
            {
                var choice = new AdventureEvent(AdventureEventKind.Princess, "Princess", Value: princess.PortraitVariant);
                yield return choice;
                if (choice.Choice is >= 1 and <= 4)
                {
                    var reward = Quests.RescuePrincess(Player, princess.RescueIndex, (PrincessGift)choice.Choice, Session.Random);
                    yield return Notice("Princess Reward", reward.Message);
                }
            }
            foreach (var placement in ActivePlacements)
                if (placement.Contains(Location.Position))
                {
                    foreach (var e in Fight(placement, previous, authored: true)) yield return e;
                    yield break; // An encounter replaces the chest and roaming phases on this visit.
                }
            if (roaming?.Position != Location.Position && Pursuer?.Position != Location.Position)
                for (var slot = 0; slot < Map.Events.Chests.Length; slot++)
                {
                    var reward = Quests.OpenChest(Player, Map, Location.Position, slot, Session.Random);
                    if (!reward.Opened) continue;
                    var text = $"You found {reward.Gold} gold and {reward.Experience} experience.";
                    if (reward.Loot.WaterproofShoeSpray) text += "\nWaterproof Shoe Spray";
                    else if (reward.Loot.EquipmentUpgrade != EquipmentUpgrade.None) text += $"\nYou found a {Combat.EquipmentUpgradeName(reward.Loot.EquipmentUpgrade)} upgrade.";
                    else if (reward.Loot.ItemId != 0) text += $"\n{ItemNames.Consumable(reward.Loot.ItemId)}";
                    else if (reward.Loot.InventoryFull) text += "\nYour inventory is full.";
                    yield return Notice("Treasure Chest", text);
                }
        }
        else
        {
            switch (result.OriginalCode)
            {
                case 5:
                case 8:
                    if (Map.Id == 8 && !Player.HasTreeClimbingShoes)
                    {
                        if (ActivePlacements.Count != 0)
                        {
                            yield return Notice("Quizilog", "The townspeople are fighting the monsters surrounding their city. Free the town so its shops can reopen.");
                            break;
                        }
                        Player.HasTreeClimbingShoes = true;
                        yield return Notice("Quizilog Reward", "The townspeople thank you for freeing the town and give you special tools for climbing certain trees around Yipe!");
                    }
                    yield return new(result.OriginalCode == 5 ? AdventureEventKind.WeaponShop : AdventureEventKind.MiscellaneousShop, "Shop");
                    break;
                case 6: yield return new(AdventureEventKind.ArmorShop, "Armor Shop"); break;
                case 7: yield return new(AdventureEventKind.HealingShop, "Healing Shop"); break;
                case 9: yield return new(AdventureEventKind.MagicShop, "Magic Shop"); break;
                case 0x0c:
                    if (result.Restored) yield return Notice("Restored", "Your health, spell points, and ability points are fully restored.", 206);
                    break;
                case 0x0d: yield return new(AdventureEventKind.Casino, "Casino"); break;
                case 0x11: yield return new(AdventureEventKind.AbilityTraining, "Ability Training"); break;
                case 0x13: yield return Notice("Locked Door", "You need a rainbow key to open this door."); break;
                case 0x14: yield return Notice("Locked Door", "You need an underworld key to open this door."); break;
                case 0x15:
                    var king = Quests.TalkToKing(Player, Session.Random, localHour);
                    yield return new(AdventureEventKind.King, "The King", king.Greeting + "\n\n" + king.Message + "\n\n" + king.Footer, 140);
                    break;
                case 0x16:
                    if (Map.Id == 0x75 && Player.HellGatekeeperState == HellGatekeeperState.Waiting)
                    {
                        var offer = new AdventureEvent(AdventureEventKind.Gatekeeper, "Gatekeeper", $"Pay {Quests.GatekeeperRecoveryFee(Player)} gold to return fully healed, or challenge the Gatekeeper.");
                        yield return offer;
                        if (offer.Choice == 1)
                        {
                            Quests.PayGatekeeperRecovery(Player);
                            yield return Notice("Gatekeeper", "After paying the Gatekeeper the small fee, he vanishes and leaves a path back to Yipe! before you.");
                        }
                        else if (offer.Choice == 2)
                            foreach (var e in Fight(Transient(41, new(6, 0)) with { EncounterSize = 3 }, gatekeeper: true)) yield return e;
                    }
                    yield break;
                case 0x17: yield return Notice("Fire Wall", $"OUCH! You burn your hand slightly on the fire wall and lose {result.Damage} health.", 144); break;
                case 0x1b: Visibility.RevealFrom(Map, Location.Position); break;
            }
        }
        if (changedMap) yield break;
        if (roaming?.Position == Location.Position)
        {
            foreach (var e in Fight(Transient(roaming.TypeId, roaming.Position), previous)) yield return e;
            yield break;
        }
        if (Pursuer?.Position == Location.Position)
        {
            foreach (var e in Fight(Transient(Pursuer.TypeId, Pursuer.Position), previous, pursuer: true)) yield return e;
            yield break;
        }
        foreach (var e in AdvanceMonsters(true, result.OriginalCode)) yield return e;
    }

    private IEnumerable<AdventureEvent> Fight(MonsterPlacement placement, WorldLocation? previous = null,
        bool authored = false, bool pursuer = false, bool gatekeeper = false)
    {
        Battle = Combat.StartMonsterEncounter(placement, Player, Session.Random, Map.Environment);
        BattleResult = BattleResolution.Ongoing;
        yield return new(AdventureEventKind.Combat, "Combat");
        if (BattleResult == BattleResolution.Ongoing) throw new InvalidOperationException("The combat interaction has not finished.");
        Battle = null;
        if (!authored && !pursuer && !gatekeeper) roaming = null;
        if (BattleResult == BattleResolution.Victory)
        {
            Map.RelocateOverlayAfterCombat(Location.Position);
            if (authored) (placement.RespawnsAfterMapReload ? temporaryDefeats : Session.DefeatedMonsterPlacements).Add(placement.PlacementIndex);
            if (pursuer) Pursuer = null;
            if (gatekeeper) Player.HellGatekeeperState = HellGatekeeperState.Defeated;
        }
        else if (BattleResult == BattleResolution.Fled && previous is not null)
        {
            Session.RestoreLocation(previous);
            PrepareMap();
        }
        else if (BattleResult == BattleResolution.Defeat)
        {
            var defeat = Quests.ResolveCombatDefeat(Player, Location, placement.TypeId, Session.Random);
            yield return Notice("Defeat", defeat.Message);
            Session.LoadLocation(Location);
            PrepareMap();
        }
    }
    private static MonsterPlacement Transient(int type, MapPosition position) => new(0, type, position, 0, 0, 0, 0, false, false);
    private IEnumerable<AdventureEvent> AdvanceMonsters(bool allowSpawn, int movementCode)
    {
        var placements = ActivePlacements;
        var update = RoamingMonsterUpdate.None;
        if (allowSpawn) update = Encounters.Update(ref roaming, Map, Player, Location, (ushort)movementCode, placements, Visibility, 33, Session.Random, Pursuer?.Position);
        else if (roaming is not null && Encounters.Move(roaming, Map, Location.Position, placements, Session.Random, Pursuer?.Position) == RoamingMonsterMove.EncounteredPlayer)
            update = RoamingMonsterUpdate.EncounteredPlayer;
        if (update == RoamingMonsterUpdate.EncounteredPlayer)
        {
            foreach (var e in Fight(Transient(roaming!.TypeId, roaming.Position))) yield return e;
            yield break;
        }
        if (update == RoamingMonsterUpdate.Spawned) yield break;
        if (Pursuer is not null && Encounters.MovePursuer(Pursuer, Map, Location.Position, placements, roaming, Visibility, Session.Random) == RoamingMonsterMove.EncounteredPlayer)
            foreach (var e in Fight(Transient(Pursuer.TypeId, Pursuer.Position), pursuer: true)) yield return e;
    }

    /// <summary>Called once after 500 ms initially, then 512 ms. Never catch up paused time.</summary>
    public IEnumerable<AdventureEvent> Tick()
    {
        if (Battle is not null) throw new InvalidOperationException("Exploration pauses during combat.");
        var tick = timer.Advance(Player, Map, Location.Position);
        if (tick % 4 == 0)
        {
            var hazard = Exploration.AdvanceHazard(Map, Visibility, Player, Location.Position, roaming?.Position, Session.Random);
            if (hazard.RemoveRoaming) roaming = null;
            if (hazard.SoundId != 0 && hazard.SoundId != 144) yield return new(AdventureEventKind.Sound, "", SoundId: hazard.SoundId);
            if (hazard.Fell)
            {
                var next = Location.Copy();
                if (!next.ChangeMapDepth(0x10)) throw new InvalidOperationException("Timed fall has no depth action.");
                Session.LoadLocation(next); PrepareMap();
            }
            else if (hazard.SoundId == 144) yield return Notice("Fire Wall", "The fire from a nearby wall spreads over where you are standing and burns you!", 144);
        }
        if (tick % 3 == 0)
        {
            var position = Location.Position;
            if (Map.AdvanceOverlay(Visibility, ref position, Session.Random, roaming?.Position))
            {
                Location.Position = position; Visibility.RevealFrom(Map, position);
                foreach (var e in AdvanceMonsters(false, 0)) yield return e;
            }
        }
        if (Exploration.AdvanceRepellent(Player)) yield return Notice("Monster Repellent", "The terrible smell that was around you seems to have faded away.", 199);
    }
    public OtherActionResult Other()
    {
        var result = WorldActions.Other(Player, Map, Location.Position, Session.Random);
        Session.ApplyPendingRelocation(); PrepareMap(); Visibility.RevealFrom(Map, Location.Position);
        return result;
    }
    public ExplorationSpellResult CastExploration(int spell, SpellPower power)
    {
        var result = Spells.CastExploration(Player, Location, spell, power, Session.Random);
        Session.ApplyPendingRelocation(); PrepareMap(); return result;
    }
    public ItemUseResult UseExplorationItem(int slot)
    {
        var result = Session.UseInventoryItem(slot); PrepareMap(); return result;
    }
    private CombatEncounter RequireBattle() => Battle is { } battle && BattleResult == BattleResolution.Ongoing
        ? battle : throw new InvalidOperationException("No active battle.");
    private CombatTurn RecordTurn(CombatTurn turn)
    {
        BattleResult = turn.Outcome switch { CombatOutcome.Victory => BattleResolution.Victory, CombatOutcome.Defeat => BattleResolution.Defeat, _ => BattleResolution.Ongoing };
        return turn;
    }
    public CombatTurn Attack() => RecordTurn(Combat.ResolveAttackTurn(Player, RequireBattle(), Session.Random));
    public CombatTurn UseAbility() => RecordTurn(Combat.ResolveSpecialAbilityTurn(Player, RequireBattle(), Session.Random));
    public CombatSpellResult CastCombat(int spell, SpellPower power)
    {
        var result = Combat.ResolveSpellTurn(Player, RequireBattle(), spell, power, Session.Random);
        RecordTurn(result.Turn); return result;
    }
    public (ItemUseResult Item, CombatTurn? Turn) UseCombatItem(int slot)
    {
        var battle = RequireBattle();
        var item = Player.UseInventoryItem(slot, Session.Random, inCombat: true, currentMapId: Map.Id, alternateMapSet: Location.AlternateMapSet);
        return (item, item.Consumed ? RecordTurn(Combat.ResolveMonsterTurn(Player, battle, Session.Random)) : null);
    }
    public FleeResult Flee()
    {
        var result = Combat.TryFlee(Player, RequireBattle(), Session.Random);
        if (result.Escaped) BattleResult = BattleResolution.Fled;
        else if (result.PlayerDefeated) BattleResult = BattleResolution.Defeat;
        return result;
    }
    public void NextTarget()
    {
        var battle = RequireBattle();
        for (var offset = 1; offset <= 3; offset++)
        {
            var slot = (battle.SelectedTarget + offset) % 3;
            if (battle.Monsters[slot].Health > 0) { battle.SelectedTarget = slot; return; }
        }
    }

    public string? LocationMessage => Map.Events.Message is { HasPosition: true } message && message.Position == Location.Position
        ? LocationMessages[message.Value - 1] : null;

    private static readonly string[] LocationMessages = [
        "Welcome to the town of Maynard.",
        "A princess has blessed these waters.",
        "Notice:     Anyone who can rescue all my daughters will be rewarded.      -The King",
        "In a language only you seem to be able to understand:    Skill shop moved secretly north.",
        "Welcome to Yipe Hell!                                          Gatekeeper ahead.",
        "Only experienced adventurers should travel beyond this sign.",
        "Beware:  Unstable floor ahead.",
        "Monster breeding grounds.",
        "I left everything behind the tree.  Make sure to know the co...  Ahhhhhhhh....",
        "Beware:   This lake has been known to have strange and random currents that leave adventurers stranded for days.",
        "Stone Cave",
        "Zephyr Cave",
        "Fire Cave",
        "Welcome to the bizarre final cave!",
        "Stay out of my lair!",
        "From the top:   2, 3, 1, 3, 2",
        "Turn back while you can!",
        "I'll be in Hell...",
        "Exit back to Yipe!",
        "The sign says 'Yipe!'.",
        "Princess Holding Cell:  West",
    ];
}
