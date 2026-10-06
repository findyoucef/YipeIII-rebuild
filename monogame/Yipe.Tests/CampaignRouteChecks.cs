using System.Text.Json;
using System.IO.Compression;
using Yipe.Core;

// Replays retained input only. The same fresh Adventure continues from the earned
// opening; observations are compared, never loaded into the live session.
internal static class CampaignRouteChecks
{
    public static void Run(string directory, bool requireCompletion, string? output = null)
    {
        OpeningRouteChecks.Run((passed, label) =>
        {
            if (!passed) throw new InvalidOperationException(label);
            Console.WriteLine("PASS " + label);
        }, continuation: adventure => Continue(adventure, directory, requireCompletion, output));
    }

    private static void Continue(Adventure game, string directory, bool requireCompletion, string? output)
    {
        if (output is not null) Directory.CreateDirectory(output);
        var lines = File.ReadAllLines(Path.Combine(directory, "commands.txt"));
        var statePath = Path.Combine(directory, "states.jsonl");
        string[] states;
        if (File.Exists(statePath)) states = File.ReadAllLines(statePath);
        else
        {
            using var compressed = File.OpenRead(statePath + ".gz");
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            states = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        using var mapInput = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        var catalog = MapCatalog.Load(mapInput);
        var checkpointPath = Path.Combine(directory, "checkpoints.jsonl");
        var checkpoints = File.Exists(checkpointPath) ? File.ReadAllLines(checkpointPath)
            .Select(line => JsonSerializer.Deserialize<Checkpoint>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .ToLookup(c => c.Observation) : Array.Empty<Checkpoint>().ToLookup(c => c.Observation);
        var p = game.Player;
        var cursor = 0; var observation = 0; var actions = 0;
        var attackSpell = 3; var attackPower = SpellPower.Low; var attackEnabled = true;
        var healSpell = 5; var healPower = SpellPower.Low; var foodBeforeSmallHeal = false;
        var useMana = false; var flee = false; var fleeAbove = 0; var targetSteps = 0;
        int? openingSpell = null; var openingPower = SpellPower.Low;
        var typed = ""; var finale = false; var quit = false;
        int Choice()
        {
            if (cursor >= lines.Length || !lines[cursor].StartsWith("choice ", StringComparison.Ordinal))
                throw new InvalidOperationException($"Expected a dialog choice at input {cursor + 1}.");
            return int.Parse(lines[cursor++].AsSpan(7));
        }
        void Compare()
        {
            var actual = JsonSerializer.SerializeToElement(new
            {
                map = game.Map.Id, x = game.Location.Position.X, y = game.Location.Position.Y,
                level = p.Level, xp = p.Experience, gold = p.Gold,
                hp = new[] { p.Health, p.MaximumHealth }, sp = new[] { p.SpellPoints, p.MaximumSpellPoints },
                ap = new[] { p.AbilityPoints, p.MaximumAbilityPoints }, stats = new[] { p.Strength, p.Dexterity, p.Intelligence },
                equipment = new[] { p.WeaponRank, p.ArmorRank, p.HelmetRank, p.GlovesRank, p.RingRank, p.BootsRank, p.NecklaceRank },
                rescues = p.RescuedPrincessCount, stamps = p.StampsFound, gatekeeper = p.GatekeeperState,
                rainbow = p.HasRainbowKey ? 1 : 0, underworld = p.HasUnderworldKey ? 1 : 0,
                spray = p.HasWaterproofShoeSpray ? 1 : 0, shoes = p.HasTreeClimbingShoes ? 1 : 0,
                deaths = p.DeathCount, healing_ring = p.HasBonusHealingRing ? 1 : 0, shop_stage = p.MiscellaneousShopStage,
                rng = game.Session.Random.State, inventory = p.Inventory.Take(Player.CarriedInventorySize), spells = p.Spells,
            });
            if (observation >= states.Length) throw new InvalidOperationException("Missing native observation.");
            using var expected = JsonDocument.Parse(states[observation]);
            foreach (var field in expected.RootElement.EnumerateObject())
                if (!actual.TryGetProperty(field.Name, out var value) || !JsonElement.DeepEquals(field.Value, value))
                    throw new InvalidOperationException($"Campaign observation {observation}, input {cursor}: {field.Name} differs. Expected {field.Value}; actual {value}. Map {game.Map.Id} {game.Location.Position}");
            foreach (var checkpoint in checkpoints[observation])
            {
                var native = File.ReadAllBytes(Path.Combine(directory, checkpoint.Label + ".y3r"));
                if (!GameSaveCodec.Encode(game.Session, catalog).SequenceEqual(SaveImporter.Convert(native, ".y3r", catalog)))
                    throw new InvalidOperationException($"Complete persistent state differs at {checkpoint.Label}.");
                Console.WriteLine($"PASS full native save checkpoint {checkpoint.Label} at observation {observation}");
                if (output is not null)
                    File.WriteAllBytes(Path.Combine(output, checkpoint.Label + ".y3m"), GameSaveCodec.Encode(game.Session, catalog));
            }
            observation++;
        }
        bool CanCast(int spell, SpellPower power) => p.Spells[spell] != 0 && p.SpellPoints >= Spells.PointCost(p, spell, power);
        void Fight()
        {
            var pendingTarget = targetSteps;
            var pendingOpening = true;
            var run = fleeAbove > 0 ? game.Battle!.Monsters.Any(m => m.TypeId != 0 && m.Health > fleeAbove) : flee;
            while (game.BattleResult == BattleResolution.Ongoing)
            {
                if (++actions > 100000) throw new InvalidOperationException("Campaign combat input budget exceeded.");
                if (run) { game.Flee(); continue; }
                if (pendingTarget > 0) { pendingTarget--; game.NextTarget(); continue; }
                if (p.SpecialAbility == SpecialAbility.Backstab && p.AbilityPoints >= 3) { game.UseAbility(); continue; }
                if (pendingOpening)
                {
                    pendingOpening = false;
                    if (openingSpell is { } first && CanCast(first, openingPower)) { game.CastCombat(first, openingPower); continue; }
                }
                int? spell = null; int? item = null; var power = SpellPower.Low;
                if (p.Spells[1] != 0 && p.Health <= (useMana ? p.MaximumHealth * 2 / 3 : p.MaximumHealth / 2))
                {
                    if (CanCast(healSpell, healPower)) { spell = healSpell; power = healPower; }
                    else if (!foodBeforeSmallHeal && CanCast(1, SpellPower.Low)) spell = 1;
                    else
                    {
                        for (var slot = 0; slot < Player.CarriedInventorySize; slot++)
                            if (p.Inventory[slot] is 1 or 2) { item = slot; break; }
                        if (item is null && CanCast(1, SpellPower.Low)) spell = 1;
                    }
                }
                if (attackEnabled)
                {
                    if (useMana && spell is null && item is null && p.SpellPoints < Spells.PointCost(p, attackSpell, attackPower) + Spells.PointCost(p, healSpell, healPower))
                        for (var slot = 0; slot < Player.CarriedInventorySize; slot++)
                            if (p.Inventory[slot] == 3) { item = slot; break; }
                    if (spell is null && item is null && CanCast(attackSpell, attackPower)) { spell = attackSpell; power = attackPower; }
                    if (spell is null && item is null && attackSpell > 3 && CanCast(3, SpellPower.Low)) spell = 3;
                }
                if (spell is { } cast) game.CastCombat(cast, power);
                else if (item is { } use) game.UseCombatItem(use);
                else game.Attack();
            }
        }
        void Equipment(EquipmentKind kind)
        {
            var stock = Shops.EquipmentStock(kind, game.Map.Id); var selected = 0;
            while (true)
                switch (Choice())
                {
                    case 1: Shops.PurchaseEquipment(p, kind, stock[selected].Rank); break;
                    case 2: selected = (selected + 1) % stock.Length; break;
                    case 3:
                        if (!Shops.SaleRequiresConfirmation(p, kind) || Choice() == 1) Shops.SellEquipment(p, kind);
                        break;
                    default: return;
                }
        }
        void Miscellaneous()
        {
            Shops.EnterMiscellaneous(p); var stock = Shops.MiscellaneousStock(p, game.Map.Id); var selected = 0;
            while (true)
                switch (Choice())
                {
                    case 1: Shops.Purchase(p, stock[selected]); break;
                    case 2: selected = (selected + 1) % stock.Length; break;
                    case 3: selected = (selected + stock.Length - 1) % stock.Length; break;
                    default: Shops.LeaveMiscellaneous(p); return;
                }
        }
        void Healing()
        {
            var selected = 0;
            while (true)
                switch (Choice())
                {
                    case 1: Shops.PurchaseHealing(p, selected, game.Session.Random); break;
                    case 2: selected = (selected + 1) % 4; break;
                    case 3: selected = (selected + 3) % 4; break;
                    default: return;
                }
        }
        void Magic()
        {
            while (true)
            {
                var choice = Choice();
                if (choice == 0) return;
                if (choice == 3) Shops.LearnNextSpell(p);
                else Shops.PurchaseSpellHealing(p, choice == 2, game.Session.Random);
            }
        }
        void Execute(IEnumerable<AdventureEvent> events)
        {
            foreach (var e in events)
                switch (e.Kind)
                {
                    case AdventureEventKind.Combat: Fight(); break;
                    case AdventureEventKind.WeaponShop: Equipment(EquipmentKind.Weapon); break;
                    case AdventureEventKind.ArmorShop: Equipment(EquipmentKind.Armor); break;
                    case AdventureEventKind.HealingShop: Healing(); break;
                    case AdventureEventKind.MiscellaneousShop: Miscellaneous(); break;
                    case AdventureEventKind.MagicShop: Magic(); break;
                    case AdventureEventKind.Princess:
                    case AdventureEventKind.Gatekeeper: e.Choice = Choice(); break;
                    case AdventureEventKind.AbilityTraining:
                        if (Choice() == 1) Shops.TrainAbility(p, game.Map.Environment);
                        break;
                    case AdventureEventKind.SecretTree: WorldActions.EnterTreeCode(p, typed); typed = ""; break;
                    case AdventureEventKind.Finale: finale = true; break;
                    case AdventureEventKind.Sound:
                    case AdventureEventKind.Message:
                    case AdventureEventKind.King: break;
                    default: throw new InvalidOperationException($"Unsupported campaign interaction: {e.Kind}");
                }
        }
        void Inventory()
        {
            var slots = Enumerable.Range(0, Player.CarriedInventorySize).Where(i => p.Inventory[i] != 0).ToArray();
            var selected = 0;
            while (slots.Length > 0)
                switch (Choice())
                {
                    case 1: game.UseExplorationItem(slots[selected]); return;
                    case 2: selected = (selected + 1) % slots.Length; break;
                    default: return;
                }
        }
        Compare();
        while (cursor < lines.Length)
        {
            var input = lines[cursor++].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var power = input.Length > 2 && input[2] == "high" ? SpellPower.High : SpellPower.Low;
            switch (input[0])
            {
                case "walk":
                    foreach (var key in input[1])
                    {
                        var d = key switch { '1' => new MapPosition(-1, 1), '2' => new(0, 1), '3' => new(1, 1), '4' => new(-1, 0),
                            '6' => new(1, 0), '7' => new(-1, -1), '8' => new(0, -1), '9' => new(1, -1), _ => throw new InvalidOperationException("Unknown direction") };
                        Execute(game.Move(d.X, d.Y));
                    }
                    break;
                case "wait": for (var i = 0; i < int.Parse(input[1]); i++) Execute(game.Tick()); break;
                case "spell": game.CastExploration(int.Parse(input[1]), SpellPower.Low); break;
                case "combat": attackEnabled = input[1] != "off"; if (attackEnabled) { attackSpell = int.Parse(input[1]); attackPower = power; } break;
                case "heal": healSpell = int.Parse(input[1]); healPower = power; foodBeforeSmallHeal = true; break;
                case "target": targetSteps = int.Parse(input[1]); break;
                case "opening": openingSpell = input[1] == "off" ? null : int.Parse(input[1]); openingPower = power; break;
                case "flee": fleeAbove = input[1] == "above" ? int.Parse(input[2]) : 0; if (input[1] != "above") flee = input[1] == "on"; break;
                case "mana": useMana = input[1] == "on"; break;
                case "type": typed = input[1]; break;
                case "key":
                    if (input[1] == "i") Inventory();
                    else if (input[1] == "other") game.Other();
                    else throw new InvalidOperationException("Unsupported campaign key");
                    break;
                case "snapshot": break;
                case "quit": quit = true; break;
                default: throw new InvalidOperationException($"Unknown campaign input: {string.Join(' ', input)}");
            }
            if (quit) break;
            Compare();
        }
        if (quit) Compare();
        if (observation != states.Length || cursor != lines.Length) throw new InvalidOperationException("Unconsumed native inputs or observations.");
        if (requireCompletion && (!quit || !finale || p.RescuedPrincessCount != 20 || p.StampsFound != 6 || p.GatekeeperState != 3
            || !p.HasRainbowKey || !p.HasUnderworldKey || !p.HasWaterproofShoeSpray || !p.HasTreeClimbingShoes
            || p.Strength < 40 || game.Map.Id != 17 || p.Health <= 0))
            throw new InvalidOperationException("Campaign did not earn every prerequisite and the live finale.");
        if (output is not null)
        {
            File.WriteAllBytes(Path.Combine(output, "last-state.y3m"), GameSaveCodec.Encode(game.Session, catalog));
            File.WriteAllText(Path.Combine(output, "last-map.json"), JsonSerializer.Serialize(new
            {
                id = game.Map.Id, location = game.Location,
                tiles_by_x = Enumerable.Range(0, Map.Width).Select(x => Enumerable.Range(0, Map.Height).Select(y => game.Map.Tile(new(x, y))).ToArray()).ToArray(),
                events = game.Map.Events,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"PASS {observation} native campaign observations; {p.RescuedPrincessCount} rescues, {p.StampsFound} stamps, {p.DeathCount} deaths, RNG {game.Session.Random.State}. "
            + (requireCompletion ? "Earned completion verified." : "Prefix only; no campaign completion claimed."));
    }
    private sealed record Checkpoint(string Label, int Observation);
}
