using System.Text.Json;
using Yipe.Core;

internal static class OpeningRouteChecks
{
    public static void Run(Action<bool, string> check, string? operation = null, string? restartDirectory = null, Action<Adventure>? continuation = null)
    {
        using var maps = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
        var catalog = MapCatalog.Load(maps);
        var directory = Path.Combine(AppContext.BaseDirectory, "Content", "Opening");
        using var input = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "inputs.json")));
        var choices = new Queue<(string Title, int Button, string Text)>(input.RootElement.GetProperty("choices").EnumerateArray()
            .Select(x => (x[0].GetString()!, x[1].GetInt32(), x[2].GetString()!)));
        var resume = operation == "--read-opening-fixture"
            ? JsonSerializer.Deserialize<ResumeInput>(File.ReadAllText(Path.Combine(restartDirectory!, "input.json")))! : null;
        var adventure = new Adventure(resume is null ? new GameSession(catalog)
            : GameSaveCodec.Decode(File.ReadAllBytes(Path.Combine(restartDirectory!, "game.y3m")), catalog));
        if (resume is not null) for (var i = 0; i < resume.ChoicesConsumed; i++) choices.Dequeue();
        var p = adventure.Player;
        var checkpoint = resume?.Checkpoint ?? 0; var actions = resume?.Actions ?? 0; var victories = resume?.Victories ?? 0;
        var chests = resume?.Chests ?? 0; var rescues = resume?.Rescues ?? 0; var commandIndex = 0;
        string[] items = ["French Fries", "Juicy Cheeseburger", "Spell Point Potion", "Monster Repellent", "Return to Surface Potion", "Strength Potion", "Dexterity Potion", "Intelligence Potion", "Potion of HP Increase", "Potion of SP Increase"];
        int Choice(string title, string text)
        {
            if (!choices.TryDequeue(out var choice) || choice.Title != title || !text.Contains(choice.Text, StringComparison.Ordinal))
                throw new InvalidOperationException($"Opening command {commandIndex}: expected {choice}, saw {title}: {text}");
            return choice.Button;
        }
        int? Inventory()
        {
            var occupied = Enumerable.Range(0, Player.CarriedInventorySize).Where(i => p.Inventory[i] != 0).ToArray();
            if (occupied.Length == 0) return null;
            var selected = 0;
            while (true)
            {
                var slot = occupied[selected]; var item = p.Inventory[slot];
                switch (Choice("Inventory", item is >= 1 and <= 10 ? items[item - 1] : $"Item {item}"))
                {
                    case 1: return slot;
                    case 2: selected = (selected + 1) % occupied.Length; break;
                    default: return null;
                }
            }
        }
        void Spell()
        {
            var available = Enumerable.Range(0, 12).Where(i => p.Spells[i] != 0 && Spells.Definitions[i].Usage != SpellUsage.Combat).ToArray();
            var selected = 0;
            while (available.Length > 0)
            {
                var index = available[selected]; var choice = Choice("Spells", Spells.Definitions[index].Name);
                if (choice is 1 or 2)
                {
                    var result = adventure.CastExploration(index, (SpellPower)choice);
                    if (result.Status != SpellCastStatus.Cast) throw new InvalidOperationException($"Opening exploration spell failed: {result.Status}");
                    return;
                }
                if (choice != 3) return;
                selected = (selected + 1) % available.Length;
            }
        }
        void Fight()
        {
            while (adventure.BattleResult == BattleResolution.Ongoing)
            {
                if (++actions > 2000) throw new InvalidOperationException("Opening combat exceeded reference input budget.");
                if (p.SpecialAbility == SpecialAbility.Backstab && p.AbilityPoints >= 3) { adventure.UseAbility(); continue; }
                int? spell = null; int? food = null;
                if (p.Spells[1] != 0 && p.Health <= p.MaximumHealth / 2)
                {
                    if (p.SpellPoints >= Spells.PointCost(p, 1, SpellPower.Low)) spell = 1;
                    else
                        for (var slot = 0; slot < Player.CarriedInventorySize; slot++)
                            if (p.Inventory[slot] is 1 or 2) { food = slot; break; }
                }
                if (spell is null && food is null && p.Spells[3] != 0 && p.SpellPoints >= Spells.PointCost(p, 3, SpellPower.Low)) spell = 3;
                if (spell is { } index) adventure.CastCombat(index, SpellPower.Low);
                else if (food is { } slot) adventure.UseCombatItem(slot);
                else if (actions is 4 or 11 or 58)
                {
                    if (Inventory() is { } scriptedSlot) adventure.UseCombatItem(scriptedSlot);
                }
                else adventure.Attack();
            }
            if (adventure.BattleResult != BattleResolution.Victory) throw new InvalidOperationException($"Opening route failed at combat action {actions}.");
            victories++;
        }
        void Equipment(EquipmentKind kind)
        {
            var title = kind == EquipmentKind.Weapon ? "Weapon Shop" : "Armor Shop";
            var stock = Shops.EquipmentStock(kind, adventure.Map.Id); var selected = 0;
            while (true)
            {
                switch (Choice(title, $"(rank {stock[selected].Rank})"))
                {
                    case 1:
                        if (Shops.PurchaseEquipment(p, kind, stock[selected].Rank) != EquipmentTransactionStatus.Purchased) throw new InvalidOperationException("Unfunded opening equipment purchase.");
                        break;
                    case 2: selected = (selected + 1) % stock.Length; break;
                    case 3:
                        if (!Shops.SaleRequiresConfirmation(p, kind) || Choice(title, "You will not be able to afford anything better. Sell anyway?") == 1) Shops.SellEquipment(p, kind);
                        break;
                    default: return;
                }
            }
        }
        void Miscellaneous()
        {
            Shops.EnterMiscellaneous(p); var stock = Shops.MiscellaneousStock(p, adventure.Map.Id); var selected = 0;
            while (true)
            {
                switch (Choice("Fred's Miscellaneous Shop", stock[selected].Name))
                {
                    case 1:
                        if (Shops.Purchase(p, stock[selected]).Status != PurchaseStatus.Purchased) throw new InvalidOperationException("Opening supply purchase failed.");
                        break;
                    case 2: selected = (selected + 1) % stock.Length; break;
                    case 3: selected = (selected + stock.Length - 1) % stock.Length; break;
                    default: Shops.LeaveMiscellaneous(p); return;
                }
            }
        }
        void Heal()
        {
            var selected = 0;
            while (true)
            {
                switch (Choice("Heal Shop", Shops.HealingServices[selected].Name))
                {
                    case 1:
                        if (Shops.PurchaseHealing(p, selected, adventure.Session.Random).Status == ServiceStatus.InsufficientGold) throw new InvalidOperationException("Unfunded opening healing.");
                        break;
                    case 2: selected = (selected + 1) % 4; break;
                    case 3: selected = (selected + 3) % 4; break;
                    default: return;
                }
            }
        }
        void Magic()
        {
            while (true)
            {
                var choice = Choice("Magic Shop", Shops.NextSpell(p)?.Name ?? "");
                if (choice == 0) return;
                if (choice == 3)
                {
                    if (Shops.LearnNextSpell(p) != SpellLearningStatus.Learned) throw new InvalidOperationException("Opening spell learning failed.");
                }
                else Shops.PurchaseSpellHealing(p, choice == 2, adventure.Session.Random);
            }
        }
        void Execute(IEnumerable<AdventureEvent> events)
        {
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case AdventureEventKind.Sound: break;
                    case AdventureEventKind.Combat: Fight(); break;
                    case AdventureEventKind.WeaponShop: Equipment(EquipmentKind.Weapon); break;
                    case AdventureEventKind.ArmorShop: Equipment(EquipmentKind.Armor); break;
                    case AdventureEventKind.MiscellaneousShop: Miscellaneous(); break;
                    case AdventureEventKind.HealingShop: Heal(); break;
                    case AdventureEventKind.MagicShop: Magic(); break;
                    case AdventureEventKind.Princess: e.Choice = Choice("Rescued Princess", ""); rescues++; break;
                    case AdventureEventKind.AbilityTraining:
                        var offer = Shops.AbilityForEnvironment(adventure.Map.Environment);
                        if (Choice("Special Ability Training Center", $"{offer?.Name}\nPrice: {offer?.Price} gold") == 1)
                            if (Shops.TrainAbility(p, adventure.Map.Environment) != AbilityTrainingStatus.Learned) throw new InvalidOperationException("Opening ability training failed.");
                        break;
                    case AdventureEventKind.Message:
                        if (e.Title == "Treasure Chest") chests++;
                        break;
                    default: throw new InvalidOperationException($"Unexpected opening interaction: {e.Kind}");
                }
            }
        }
        foreach (var command in input.RootElement.GetProperty("commands").EnumerateArray())
        {
            commandIndex++;
            if (commandIndex <= (resume?.CommandIndex ?? 0)) continue;
            var key = command[0].GetString();
            switch (key)
            {
                case "checkpoint":
                    var expected = SaveImporter.Convert(File.ReadAllBytes(Path.Combine(directory, "Reference", $"{checkpoint}.y3r")), ".y3r", catalog);
                    var actual = GameSaveCodec.Encode(adventure.Session, catalog);
                    if (!actual.SequenceEqual(expected))
                    {
                        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "yipe-opening-actual.y3m"), actual);
                        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "yipe-opening-expected.y3m"), expected);
                        throw new InvalidOperationException($"Earned opening checkpoint {checkpoint} differs at command {commandIndex}: map {adventure.Map.Id} {adventure.Location.Position}, level {p.Level}, gold {p.Gold}, RNG {adventure.Session.Random.State}.");
                    }
                    checkpoint++;
                    if (operation == "--write-opening-fixture" && checkpoint == 40)
                    {
                        Directory.CreateDirectory(restartDirectory!);
                        File.WriteAllBytes(Path.Combine(restartDirectory!, "game.y3m"), actual);
                        var position = new ResumeInput(commandIndex, checkpoint, input.RootElement.GetProperty("choices").GetArrayLength() - choices.Count,
                            actions, victories, chests, rescues);
                        File.WriteAllText(Path.Combine(restartDirectory!, "input.json"), JsonSerializer.Serialize(position));
                        Console.WriteLine("Earned opening saved before Wormoid; writer exiting.");
                        return;
                    }
                    break;
                case "wait": for (var i = 0; i < command[1].GetInt32(); i++) Execute(adventure.Tick()); break;
                case "other": adventure.Other(); break;
                case "inventory": if (Inventory() is { } slot) adventure.UseExplorationItem(slot); break;
                case "spells": Spell(); break;
                default:
                    var direction = key switch
                    {
                        "1" => new MapPosition(-1, 1), "2" => new(0, 1), "3" => new(1, 1), "4" => new(-1, 0),
                        "6" => new(1, 0), "7" => new(-1, -1), "8" => new(0, -1), "9" => new(1, -1),
                        _ => throw new InvalidOperationException($"Unknown opening input: {key}"),
                    };
                    Execute(adventure.Move(direction.X, direction.Y)); break;
            }
        }
        check(checkpoint == 42 && choices.Count == 0 && victories == 116 && chests == 14 && rescues == 2,
            "Earned C++ opening route: all 42 complete persistent-state/RNG checkpoints, 116 victories, 14 chests and two rescues match");
        if (continuation is not null) { continuation(adventure); return; }
        var returnSpell = adventure.CastExploration(0, SpellPower.Low);
        check(returnSpell.Status == SpellCastStatus.Cast, "Earned return has enough SP for Return to Surface");
        Execute(adventure.Move(1, 1));
        for (var i = 0; i < 8; i++) Execute(adventure.Move(1, 0));
        Execute(adventure.Move(1, -1));
        for (var i = 0; i < 3; i++)
            foreach (var e in adventure.Move(0, -1))
                if (e.Kind != AdventureEventKind.King) throw new InvalidOperationException("Unexpected King return interaction.");
        var returned = SaveImporter.Convert(File.ReadAllBytes(Path.Combine(directory, "Reference", "king-return.y3r")), ".y3r", catalog);
        check(GameSaveCodec.Encode(adventure.Session, catalog).SequenceEqual(returned) && p.Gold == 2238 && p.QuestFlags[2],
            "Earned King return matches complete C++ state/RNG and awards 500 gold");
        if (operation is not null) Console.WriteLine("Fresh process earned Wormoid, second rescue and King's reward with matching C++ state.");
    }

    private sealed record ResumeInput(int CommandIndex, int Checkpoint, int ChoicesConsumed, int Actions, int Victories, int Chests, int Rescues);

    public static async Task Restart(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "yipe-earned-opening-" + Guid.NewGuid().ToString("N"));
        try
        {
            await GameSaveChecks.RunChild("--write-opening-fixture", directory);
            await GameSaveChecks.RunChild("--read-opening-fixture", directory);
            check(true, "Separate writer/reader processes preserve the earned opening and finish its reward route");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
