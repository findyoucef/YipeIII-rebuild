using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

/// <summary>Earned reference inputs translated to the new overlay controls, without editing gameplay state.</summary>
internal static class OpeningFrontendChecks
{
    private sealed record ResumeInput(int Commands, int Checkpoints, int Actions, int Victories, int Chests, int Rescues, int ChoicesConsumed);

    public static void Run(string output, StreamWriter log, string? resumeDirectory = null)
    {
        var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.TownGame", true)!;
        var savePath = Path.Combine(output, "opening.y3m");
        var resume = resumeDirectory is null ? null : JsonSerializer.Deserialize<ResumeInput>(File.ReadAllText(Path.Combine(resumeDirectory, "input.json")))!;
        if (resume is not null)
        {
            File.Copy(Path.Combine(resumeDirectory!, "game.y3m"), savePath);
            File.Copy(Path.Combine(resumeDirectory!, "game.map.json"), Path.ChangeExtension(savePath, ".map.json"));
        }
        using var game = (Game)Activator.CreateInstance(type, [false, null, savePath])!;
        object? Get(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
        object? Call(string method, params object[] arguments) => type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, arguments);
        var graphics = (GraphicsDeviceManager)Get("graphics")!;
        graphics.SynchronizeWithVerticalRetrace = false;
        for (var i = 0; i < 5; i++) game.RunOneFrame();
        if (!game.IsActive) throw new InvalidOperationException("The opening replay needs an active graphics window.");
        if (Get("loading") is Task load)
        {
            try { load.GetAwaiter().GetResult(); }
            catch (FileNotFoundException) { /* A new isolated adventure has no save yet. */ }
            Call("FinishStorage");
        }
        Set("paused", false); Set("pauseAmount", 0f);
        var audio = Get("audio")!; audio.GetType().GetProperty("Enabled")!.SetValue(audio, false);
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Content", "Opening");
        using var script = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureDirectory, "inputs.json")));
        var legacyChoices = new Queue<(string Title, int Button)>(script.RootElement.GetProperty("choices").EnumerateArray()
            .Select(value => (value[0].GetString()!, value[1].GetInt32())));
        var commands = script.RootElement.GetProperty("commands").EnumerateArray().ToArray();
        var catalog = (MapCatalog)Get("catalog")!;
        if (resume is not null)
        {
            var previous = MapJournal.Decode(File.ReadAllBytes(Path.ChangeExtension(savePath, ".map.json")), File.ReadAllBytes(savePath), catalog);
            var restored = (MapJournal)Get("journal")!;
            if (restored.Regions.Count != previous.Regions.Count || !restored.Links.SequenceEqual(previous.Links)
                || previous.Regions.Any(r => restored.Find(r.Id) is not { } region
                    || Enumerable.Range(0, Map.TileCount).Any(i => r.Seen[i] && !region.Seen[i])))
                throw new InvalidOperationException("Fresh-process map discoveries differ from the earned save.");
            var message = $"PASS fresh-process map journal: {previous.Regions.Count} earned regions and {previous.Links.Count} travel links restored.";
            Console.WriteLine(message); log.WriteLine(message);
        }
        var totalChoices = legacyChoices.Count;
        if (resume is not null) for (var i = 0; i < resume.ChoicesConsumed; i++) legacyChoices.Dequeue();
        var commandIndex = 0; var checkpoints = resume?.Checkpoints ?? 0; var actions = resume?.Actions ?? 0;
        var victories = resume?.Victories ?? 0; var chests = resume?.Chests ?? 0; var rescues = resume?.Rescues ?? 0; var frames = 0;
        GameSession Session() => (GameSession)Get("session")!;
        Adventure Adventure() => (Adventure)Get("adventure")!;
        Player Hero() => Session().Player;
        object Overlay() => Get("overlay") ?? throw new InvalidOperationException($"No overlay at command {commandIndex}.");
        T Property<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
        string Title() => Property<string>(Overlay(), "Title");
        string[] Labels() => Property<Array>(Overlay(), "Options").Cast<object>().Select(option => Property<string>(option, "Label")).ToArray();
        void Draw()
        {
            Call("Draw", new GameTime()); frames++;
        }
        void Capture(string name)
        {
            Draw();
            var parameters = game.GraphicsDevice.PresentationParameters;
            var pixels = new Color[parameters.BackBufferWidth * parameters.BackBufferHeight];
            game.GraphicsDevice.GetBackBufferData(pixels);
            using var texture = new Texture2D(game.GraphicsDevice, parameters.BackBufferWidth, parameters.BackBufferHeight);
            texture.SetData(pixels);
            using var file = File.Create(Path.Combine(output, name)); texture.SaveAsPng(file, texture.Width, texture.Height);
        }
        void Control(InputFrame frame) => Call("UpdateOverlay", frame);
        void Pick(int index)
        {
            var current = Overlay();
            while (Property<int>(current, "Page") < Property<int>(current, "PageCount") - 1)
                Control(new(new Point(1, 0), false, false, false));
            var count = Labels().Length;
            if (index < 0 || index >= count) throw new InvalidOperationException($"Invalid choice {index} in {Title()}.");
            for (var step = 0; Property<int>(current, "Selected") != index && step < count; step++)
                Control(new(new Point(0, 1), false, false, false));
            Draw();
            Control(new(Point.Zero, true, false, false));
        }
        void Choose(string label)
        {
            var labels = Labels(); var index = Array.IndexOf(labels, label);
            if (index < 0) throw new InvalidOperationException($"Opening command {commandIndex}: '{label}' not in {Title()}: {string.Join(", ", labels)}");
            Pick(index);
        }
        void Continue() => Choose("Continue");
        int Legacy(string title)
        {
            if (!legacyChoices.TryDequeue(out var choice) || choice.Title != title)
                throw new InvalidOperationException($"Opening command {commandIndex}: expected legacy {choice}, encountered {title}.");
            return choice.Button;
        }
        void Update(double seconds)
        {
            // Reference input waits author the simulation clock. Rendered walking still takes 250 ms.
            Set("tickRemaining", double.MaxValue);
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));
        }
        void OpenActions() { Call("ShowActions"); }
        void UseItem(int slot, bool combat)
        {
            if (!combat) OpenActions();
            Choose("Inventory");
            var occupied = Enumerable.Range(0, Player.CarriedInventorySize).Where(i => Hero().Inventory[i] != 0).ToArray();
            Pick(Array.IndexOf(occupied, slot)); Choose("Use"); Continue();
            if (!combat) { Choose("Back"); Choose("Continue exploring"); }
        }
        int? LegacyInventory()
        {
            var occupied = Enumerable.Range(0, Player.CarriedInventorySize).Where(i => Hero().Inventory[i] != 0).ToArray();
            if (occupied.Length == 0) return null;
            var selected = 0;
            while (true)
            {
                switch (Legacy("Inventory"))
                {
                    case 1: return occupied[selected];
                    case 2: selected = (selected + 1) % occupied.Length; break;
                    default: return null;
                }
            }
        }
        void Cast(int index, SpellPower power, bool combat)
        {
            if (!combat) OpenActions();
            Choose("Spells"); Choose(Spells.Definitions[index].Name);
            Choose($"{(power == SpellPower.Low ? "Low" : "High")} power - {Spells.PointCost(Hero(), index, power)} SP");
            Continue();
        }
        void LegacySpell()
        {
            var available = Enumerable.Range(0, 12).Where(i => Hero().Spells[i] != 0 && Spells.Definitions[i].Usage != SpellUsage.Combat).ToArray();
            var selected = 0;
            while (available.Length > 0)
            {
                var choice = Legacy("Spells");
                if (choice is 1 or 2) { Cast(available[selected], (SpellPower)choice, false); return; }
                if (choice != 3) return;
                selected = (selected + 1) % available.Length;
            }
        }
        void Fight()
        {
            if (victories == 0)
            {
                Call("OpenMap");
                if ((bool)Get("mapOpen")!) throw new InvalidOperationException("Map interrupted an active battle.");
                log.WriteLine("PASS map cannot interrupt an earned combat interaction.");
                Capture("opening-first-battle.png");
            }
            while (Adventure().Battle is not null && Adventure().BattleResult == BattleResolution.Ongoing)
            {
                if (++actions > 2000) throw new InvalidOperationException("Opening combat input budget exceeded.");
                var p = Hero();
                if (p.SpecialAbility == SpecialAbility.Backstab && p.AbilityPoints >= 3)
                { Choose("Ability: Backstab"); Continue(); continue; }
                int? spell = null; int? food = null;
                if (p.Spells[1] != 0 && p.Health <= p.MaximumHealth / 2)
                {
                    if (p.SpellPoints >= Spells.PointCost(p, 1, SpellPower.Low)) spell = 1;
                    else for (var slot = 0; slot < Player.CarriedInventorySize; slot++) if (p.Inventory[slot] is 1 or 2) { food = slot; break; }
                }
                if (spell is null && food is null && p.Spells[3] != 0 && p.SpellPoints >= Spells.PointCost(p, 3, SpellPower.Low)) spell = 3;
                if (spell is { } index) Cast(index, SpellPower.Low, true);
                else if (food is { } slot) UseItem(slot, true);
                else if (actions is 4 or 11 or 58)
                {
                    if (LegacyInventory() is { } scriptedSlot) UseItem(scriptedSlot, true);
                }
                else { Choose("Attack"); Continue(); }
            }
            if (Adventure().BattleResult != BattleResolution.Victory) throw new InvalidOperationException($"Opening battle failed at action {actions}.");
            victories++;
        }
        void Equipment(EquipmentKind kind)
        {
            var legacyTitle = kind == EquipmentKind.Weapon ? "Weapon Shop" : "Armor Shop";
            var stock = Shops.EquipmentStock(kind, Adventure().Map.Id); var selected = 0;
            while (true)
            {
                switch (Legacy(legacyTitle))
                {
                    case 1: Pick(selected + 1); Continue(); break;
                    case 2: selected = (selected + 1) % stock.Length; break;
                    case 3:
                        Pick(0);
                        if (Title() == "Sell equipment?") Choose(Legacy(legacyTitle) == 1 ? "Sell" : "Keep it");
                        if (Labels().SequenceEqual(new[] { "Continue" })) Continue();
                        break;
                    default: Choose("Leave"); return;
                }
            }
        }
        void Miscellaneous()
        {
            var selected = 0;
            while (true)
            {
                switch (Legacy("Fred's Miscellaneous Shop"))
                {
                    case 1: Pick(selected); Continue(); break;
                    case 2: selected = (selected + 1) % 11; break;
                    case 3: selected = (selected + 10) % 11; break;
                    default: Choose("Leave"); return;
                }
            }
        }
        void Heal()
        {
            var selected = 0;
            while (true)
            {
                switch (Legacy("Heal Shop"))
                {
                    case 1: Pick(selected); Continue(); break;
                    case 2: selected = (selected + 1) % 4; break;
                    case 3: selected = (selected + 3) % 4; break;
                    default: Choose("Leave"); return;
                }
            }
        }
        void Magic()
        {
            while (true)
            {
                var choice = Legacy("Magic Shop");
                if (choice == 0) { Choose("Leave"); return; }
                if (choice == 3) { Pick(2); Choose("Learn spell"); Continue(); }
                else { Pick(choice == 2 ? 1 : 0); Continue(); }
            }
        }
        void Drain()
        {
            for (var count = 0; Get("overlay") is not null; count++)
            {
                if (count > 200) throw new InvalidOperationException("Opening interaction did not finish.");
                if (Property<bool>(Overlay(), "Combat")) { Fight(); continue; }
                if (Labels().SequenceEqual(new[] { "Continue" }))
                {
                    if (Title() == "Treasure Chest") chests++;
                    Continue(); continue;
                }
                switch (Title())
                {
                    case "Weapon Shop": Equipment(EquipmentKind.Weapon); break;
                    case "Armor Shop": Equipment(EquipmentKind.Armor); break;
                    case "General Store": Miscellaneous(); break;
                    case "Healing Shop": Heal(); break;
                    case "Magic Shop": Magic(); break;
                    case "Rescued Princess": rescues++; Pick(Legacy("Rescued Princess") - 1); break;
                    case "Ability Training":
                        if (Legacy("Special Ability Training Center") == 1) { Pick(0); Continue(); }
                        Choose("Leave"); break;
                    default: throw new InvalidOperationException($"Unexpected opening overlay: {Title()}");
                }
            }
        }
        void Move(char key)
        {
            var direction = key switch
            {
                '1' => new Point(-1, 1), '2' => new(0, 1), '3' => new(1, 1), '4' => new(-1, 0),
                '6' => new(1, 0), '7' => new(-1, -1), '8' => new(0, -1), '9' => new(1, -1),
                _ => throw new InvalidOperationException("Invalid opening direction."),
            };
            Set("pendingDirection", direction); Update(0); Update(PlayerMotion.StepDuration); Drain(); Draw();
        }
        void Compare(string fixture)
        {
            var expected = SaveImporter.Convert(File.ReadAllBytes(Path.Combine(fixtureDirectory, "Reference", fixture)), ".y3r", catalog);
            // The retained C++ route uses default portrait 0. The new creation UI explicitly selects
            // portrait 1 (the same knight). Normalize only that cosmetic field in the EXPECTED copy.
            var normalized = (byte[])expected.Clone();
            using (var bytes = new MemoryStream(normalized, writable: true))
            {
                bytes.Position = 46;
                using var reader = new BinaryReader(bytes, Encoding.UTF8, leaveOpen: true);
                var p = (Player)typeof(GameSaveCodec).GetMethod("ReadPlayer", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [reader])!;
                if (p.Inventory[28] != 0 || Hero().Inventory[28] != 1) throw new InvalidOperationException("Unexpected portrait normalization.");
                p.Inventory[28] = 1; bytes.Position = 46;
                using var writer = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true);
                typeof(GameSaveCodec).GetMethod("WritePlayer", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [writer, p]);
            }
            var changed = Enumerable.Range(46, expected.Length - 46).Where(i => expected[i] != normalized[i]).ToArray();
            if (changed.Length != 1 || expected[changed[0]] != 0 || normalized[changed[0]] != 1)
                throw new InvalidOperationException("Expected normalization changed more than the portrait selection.");
            SHA256.HashData(normalized.AsSpan(46)).CopyTo(normalized, 14);
            var actual = GameSaveCodec.Encode(Session(), catalog);
            if (!actual.SequenceEqual(normalized))
            {
                File.WriteAllBytes(Path.Combine(output, "actual.y3m"), actual);
                File.WriteAllBytes(Path.Combine(output, "expected.y3m"), normalized);
                throw new InvalidOperationException($"Frontend checkpoint {fixture} differs at command {commandIndex}, map {Adventure().Map.Id} {Adventure().Location.Position}, RNG {Session().Random.State}.");
            }
            var message = $"PASS frontend checkpoint {fixture}: map {Adventure().Map.Id}, level {Hero().Level}, gold {Hero().Gold}, RNG {Session().Random.State}";
            Console.WriteLine(message); log.WriteLine(message);
        }

        void Save()
        {
            Set("paused", true); Set("selected", 1); Call("ActivateMenu");
            ((Task)Get("saving")!).GetAwaiter().GetResult(); Call("FinishStorage");
            if (!File.ReadAllBytes(savePath).SequenceEqual(GameSaveCodec.Encode(Session(), catalog)))
                throw new InvalidOperationException("Frontend pause-menu save differs from the earned session.");
            var notes = File.ReadAllBytes(Path.ChangeExtension(savePath, ".map.json"));
            if (!notes.SequenceEqual(((MapJournal)Get("journal")!).Encode(File.ReadAllBytes(savePath))))
                throw new InvalidOperationException("Frontend pause-menu save lost map discoveries.");
            Set("paused", false);
        }

        if (resume is null)
        { Call("BeginCharacterCreation"); Choose("Combat"); Choose("Use these attributes"); Choose("Done"); Choose("Medium"); Choose("Classic"); Choose("Knight"); Continue(); }
        else Compare("39.y3r");
        foreach (var command in commands)
        {
            commandIndex++;
            if (resume is not null && commandIndex <= resume.Commands) continue;
            var key = command[0].GetString()!;
            switch (key)
            {
                case "checkpoint": Compare($"{checkpoints}.y3r");
                    if (checkpoints is 0 or 10 or 20 or 30 or 39 or 41) Capture($"opening-checkpoint-{checkpoints}.png");
                    checkpoints++;
                    if (checkpoints == 40 && resume is null)
                    {
                        Save();
                        var directory = Path.Combine(output, "restart"); Directory.CreateDirectory(directory);
                        File.Copy(savePath, Path.Combine(directory, "game.y3m"));
                        File.Copy(Path.ChangeExtension(savePath, ".map.json"), Path.Combine(directory, "game.map.json"));
                        File.WriteAllText(Path.Combine(directory, "input.json"), JsonSerializer.Serialize(new ResumeInput(
                            commandIndex, checkpoints, actions, victories, chests, rescues, totalChoices - legacyChoices.Count)));
                    }
                    break;
                case "wait":
                    for (var tick = 0; tick < command[1].GetInt32(); tick++)
                    {
                        Set("tickRemaining", 0d); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.Zero));
                        Update(PlayerMotion.StepDuration); Drain();
                    }
                    break;
                case "other": OpenActions(); Choose("Other: investigate this location"); Continue(); break;
                case "inventory": if (LegacyInventory() is { } slot) UseItem(slot, false); break;
                case "spells": LegacySpell(); break;
                default: Move(key[0]); break;
            }
        }
        if (checkpoints != 42 || legacyChoices.Count != 0 || victories != 116 || chests != 14 || rescues != 2)
            throw new InvalidOperationException($"Opening totals differ: {checkpoints} checkpoints, {legacyChoices.Count} choices remaining, {victories} victories, {chests} chests, {rescues} rescues.");
        Cast(0, SpellPower.Low, false);
        foreach (var key in "3666666669") Move(key);
        Move('8'); Move('8'); Move('8');
        Compare("king-return.y3r"); Capture("opening-earned-king-reward.png");
        Save();
        var summary = $"PASS earned frontend opening{(resume is null ? "" : " fresh-process continuation")}: 42 C++ checkpoints + King return; {victories} victories, {chests} chests, {rescues} rescues; {frames} rendered frames; pause-menu save verified. Only default/selected portrait metadata is normalized in the expected fixtures.";
        Console.WriteLine(summary); log.WriteLine(summary);
        game.Exit();
    }
}
