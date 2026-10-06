using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

/// <summary>Continues a save earned by the independently compared campaign, using real overlays and timing.</summary>
internal static class LateFrontendChecks
{
    public static void Run(string input, string output, StreamWriter log, bool resume)
    {
        var source = Path.Combine(input, resume ? "game.y3m" : "hell-finale-boss-ready.y3m");
        var save = Path.Combine(output, "late-game.y3m"); File.Copy(source, save);
        var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.TownGame", true)!;
        using var game = (Game)Activator.CreateInstance(type, [false, null, save])!;
        object? Get(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
        object? Call(string name, params object[] arguments) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, arguments);
        ((GraphicsDeviceManager)Get("graphics")!).SynchronizeWithVerticalRetrace = false;
        for (var i = 0; i < 5; i++) game.RunOneFrame();
        if (!game.IsActive) throw new InvalidOperationException("Late-route verification needs an active graphics window.");
        if (Get("loading") is Task loading) { loading.GetAwaiter().GetResult(); Call("FinishStorage"); }
        Set("paused", false); Set("pauseAmount", 0f);
        var audio = Get("audio")!; audio.GetType().GetProperty("Enabled")!.SetValue(audio, false);
        GameSession Session() => (GameSession)Get("session")!;
        Adventure Adventure() => (Adventure)Get("adventure")!;
        Player Hero() => Session().Player;
        var catalog = (MapCatalog)Get("catalog")!;
        var frames = 0; var victory = false;
        void Check(bool passed, string text)
        {
            if (!passed) throw new InvalidOperationException(text);
            Console.WriteLine("PASS " + text); log.WriteLine("PASS " + text);
        }
        void Draw() => Call("Draw", new GameTime());
        void Capture(string name)
        {
            Draw(); var p = game.GraphicsDevice.PresentationParameters;
            var pixels = new Color[p.BackBufferWidth * p.BackBufferHeight]; game.GraphicsDevice.GetBackBufferData(pixels);
            using var texture = new Texture2D(game.GraphicsDevice, p.BackBufferWidth, p.BackBufferHeight); texture.SetData(pixels);
            using var file = File.Create(Path.Combine(output, name)); texture.SaveAsPng(file, texture.Width, texture.Height);
        }
        T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        string Title() => Property<string>(Get("overlay")!, "Title");
        void Choose(string label)
        {
            var current = Get("overlay") ?? throw new InvalidOperationException("No overlay for " + label);
            void Control(InputFrame frame) => Call("UpdateOverlay", frame);
            while (Property<int>(current, "Page") < Property<int>(current, "PageCount") - 1)
                Control(new(new Point(1, 0), false, false, false));
            var labels = Property<Array>(current, "Options").Cast<object>().Select(o => Property<string>(o, "Label")).ToArray();
            var index = Array.IndexOf(labels, label);
            if (index < 0) throw new InvalidOperationException($"{label} is missing from {Title()}: {string.Join(", ", labels)}");
            for (var i = 0; Property<int>(current, "Selected") != index && i < labels.Length; i++)
                Control(new(new Point(0, 1), false, false, false));
            Draw(); Control(new(Point.Zero, true, false, false));
        }
        void Cast(int spell, bool combat)
        {
            if (!combat) Call("ShowActions");
            Choose("Spells"); Choose(Spells.Definitions[spell].Name);
            Choose($"Low power - {Spells.PointCost(Hero(), spell, SpellPower.Low)} SP"); Choose("Continue");
            if (combat && Get("overlay") is not null && Title() == "Spells") Choose("Back");
        }
        void Fight()
        {
            var battleMenu = Get("overlay");
            Call("ShowNewGameConfirmation"); Choose("Keep playing");
            Check(ReferenceEquals(battleMenu, Get("overlay")), "Cancelling New adventure restores the active combat interaction");
            Capture("late-gatekeeper-battle.png");
            var opening = true;
            for (var action = 0; action < 300 && Adventure().Battle is not null; action++)
            {
                if (Get("overlay") is null) throw new InvalidOperationException("Battle lost its overlay.");
                if (Title() != "Combat") { Choose("Continue"); continue; }
                if (Hero().SpecialAbility == SpecialAbility.Backstab && Hero().AbilityPoints >= 3)
                { Choose("Ability: Backstab"); Choose("Continue"); }
                else if (opening) { opening = false; Cast(11, true); }
                else if (Hero().Health <= Hero().MaximumHealth * 2 / 3 && Hero().SpellPoints >= Spells.PointCost(Hero(), 5, SpellPower.Low)) Cast(5, true);
                else if (Hero().Health <= Hero().MaximumHealth * 2 / 3 && Hero().SpellPoints >= Spells.PointCost(Hero(), 1, SpellPower.Low)) Cast(1, true);
                else if (Hero().SpellPoints < 63 && Hero().Inventory.Take(Player.CarriedInventorySize).Contains((ushort)3))
                { Choose("Inventory"); Choose(ItemNames.Consumable(3)); Choose("Use"); Choose("Continue"); }
                else if (Hero().SpellPoints >= Spells.PointCost(Hero(), 6, SpellPower.Low)) Cast(6, true);
                else if (Hero().SpellPoints >= Spells.PointCost(Hero(), 3, SpellPower.Low)) Cast(3, true);
                else { Choose("Attack"); Choose("Continue"); }
            }
            Check(Adventure().Battle is null && Hero().Health > 0 && Hero().GatekeeperState == 2,
                $"The earned late save defeats the empowered Gatekeeper through combat overlays (HP {Hero().Health}, Gatekeeper {Hero().GatekeeperState}, battle {Adventure().BattleResult})");
        }
        void Dismiss()
        {
            for (var i = 0; Get("overlay") is not null && i < 100; i++)
            {
                if (Title() == "Combat") Fight();
                else if (Title() == "Rescued Princess") Choose("Mystery");
                else
                {
                    if (Title() == "Victory") { victory = true; Capture("late-victory.png"); }
                    Choose("Continue");
                }
            }
            if (Get("overlay") is not null) throw new InvalidOperationException("Late route left an unresolved interaction.");
        }
        void Frame(double seconds = .05)
        {
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds))); frames++;
            Dismiss();
            if (frames % 100 == 0) Draw();
        }
        void Walk(string directions)
        {
            foreach (var key in directions)
            {
                var d = key switch { '1' => new Point(-1, 1), '2' => new(0, 1), '3' => new(1, 1), '4' => new(-1, 0),
                    '6' => new(1, 0), '7' => new(-1, -1), '8' => new(0, -1), '9' => new(1, -1), _ => throw new InvalidOperationException("Unknown direction") };
                Set("pendingDirection", d); Frame(0);
                for (var i = 0; i < 5; i++) Frame();
                while (((PlayerMotion)Get("player")!).IsMoving || Get("pendingEvent") is not null) Frame();
            }
        }
        void Until(Func<bool> done)
        {
            for (var i = 0; i < 200000; i++) { if (done()) return; Frame(); }
            throw new InvalidOperationException("Late-route wait did not complete.");
        }
        void Rest()
        {
            Until(() => Hero().SpellPoints >= 33);
            if (Hero().Health < Hero().MaximumHealth) Cast(5, false);
            Until(() => Hero().SpellPoints == Hero().MaximumSpellPoints);
        }
        void Save()
        {
            Set("paused", true); Set("selected", 1); Call("ActivateMenu");
            ((Task)Get("saving")!).GetAwaiter().GetResult(); Call("FinishStorage");
            Check(File.ReadAllBytes(save).SequenceEqual(GameSaveCodec.Encode(Session(), catalog)),
                "Production pause-menu save preserves the complete earned late-game state");
            Set("paused", false);
        }
        Check(Hero().StampsFound == 6 && Hero().HasUnderworldKey && Hero().HasRainbowKey
            && Hero().HasWaterproofShoeSpray && Hero().HasTreeClimbingShoes && Hero().Strength >= 40,
            "The late frontend starts with the campaign's earned movement and quest prerequisites");
        if (!resume)
        {
            Check(Session().Location.MapId == 127 && Session().Location.Position == new MapPosition(4, 6)
                && Hero().RescuedPrincessCount == 18 && Hero().GatekeeperState == 1, "The compared C# campaign save opens before the final boss");
            Cast(4, false); Walk("2"); Dismiss(); Rest();
            Walk("214");
            Check(Hero().RescuedPrincessCount == 19 && Hero().GatekeeperState == 2, "The boss passage reaches the nineteenth princess with earned progress");
            Rest(); Cast(4, false); Walk("66666622"); Walk("1122222");
            Check(Session().Location.MapId == 133 && Session().Location.Position == new MapPosition(6, 8), "The earned late route reaches the final fire-wall landing");
            Capture("late-final-princess-bank.png"); Save();
            var restart = Path.Combine(output, "restart"); Directory.CreateDirectory(restart); File.Copy(save, Path.Combine(restart, "game.y3m"));
        }
        else
        {
            Check(Session().Location.MapId == 133 && Hero().RescuedPrincessCount == 19 && Hero().GatekeeperState == 2,
                "A fresh graphics process reopens the earned final-princess checkpoint");
            Until(() => Adventure().Map.Tile(new(5, 8)) == 0x27); Walk("44441");
            Check(Hero().RescuedPrincessCount == 20 && Session().Location.Position == new MapPosition(1, 9),
                "At 250 ms per step, the reopened adventure earns the twentieth rescue through the fire opening");
            Capture("late-final-princess-rescued.png"); Walk("32");
            Check(Session().Location.MapId == 17, "The final Hell exit returns the earned adventure to Maynard");
            // Westward return uses the town's walkable northern edge; the river blocks row five.
            Walk("497744");
            Check(Session().Location.MapId == 16 && Session().Location.Position == new MapPosition(11, 2), "The hero leaves town through its normal map edge");
            Walk("6");
            Check(victory && Hero().GatekeeperState == 3 && Hero().RescuedPrincessCount == 20 && Hero().StampsFound == 6,
                "Normal town entry displays the live finale after all earned rescues, stamps and Gatekeeper progression");
            Capture("late-completed-town.png"); Save();
        }
        log.WriteLine($"PASS earned late frontend {(resume ? "restart and finale" : "boss and saved continuation")}; {frames} normal-clock update frames.");
        Console.WriteLine($"PASS earned late frontend {(resume ? "restart and finale" : "boss and saved continuation")}; {frames} update frames.");
        game.Exit();
    }
}
