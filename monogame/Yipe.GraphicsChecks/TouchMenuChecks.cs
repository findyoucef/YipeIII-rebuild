using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class TouchMenuChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value)
        {
            var field = type.GetField(name, flags)!;
            field.SetValue(game, field.FieldType.IsEnum && value is string page ? Enum.Parse(field.FieldType, page) : value);
        }
        object? Call(string name, params object?[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "touchEnabled", "touchContext", "touchInsets", "titleScreen", "titleSelected", "studioSplashRemaining",
            "paused", "pausePage", "pauseAmount", "selected", "selectionY", "overlay", "mapOpen", "worldMapOpen",
            "controllerConnected", "textReceiver", "session", "adventure", "menuMessage", "restorePauseOverlay", "pendingDirection" }.ToDictionary(n => n, Get);
        var input = (TouchInput)Get("touchInput")!;
        var profile = Get("profile")!;
        var data = profile.GetType().GetProperty("Data")!.GetValue(profile)!;
        float Value(string name) => (float)data.GetType().GetProperty(name)!.GetValue(data)!;
        var oldMusic = Value("MusicVolume");
        var directory = Path.Combine(output, "touch"); Directory.CreateDirectory(directory);
        try
        {
            Set("touchEnabled", true); Set("studioSplashRemaining", 0d); Set("controllerConnected", false);
            Call("ShowWelcome"); Draw("title.png");
            Tap(input.Regions.Single(r => r.Row == 2));
            check((bool)Get("paused")! && Get("pausePage")!.ToString() == "Options", "Touch title Options routes to the existing pause categories");
            Draw("options.png"); Tap(input.Regions.Single(r => r.Row == 2));
            Draw("sound.png");
            var slider = input.Regions.Single(r => r.Slider && r.Row == 4);
            var center = slider.Bounds.Center.ToVector2();
            Call("ReadTouch", new TouchCollection([]));
            Call("ReadTouch", new TouchCollection([new(1, TouchLocationState.Pressed, center)]));
            Call("ReadTouch", new TouchCollection([new(1, TouchLocationState.Released, new(slider.Bounds.X + slider.Bounds.Width * .8f, center.Y))]));
            check(Math.Abs(Value("MusicVolume") - .8f) < .01 && (int)Get("selected")! == 4,
                "Touch sound slider sets the requested volume and retains its row");
            Call("BackPausePage"); Set("selected", 6); Draw("options-scrolled.png");
            Tap(input.Regions.Single(r => r.Row == 6)); Draw("touch-settings.png");
            check(Get("pausePage")!.ToString() == "Touch" && input.Regions.Count(r => r.Slider) == 2,
                "Touch options expose size and opacity sliders without changing desktop categories");
            Call("ShowWelcome"); Call("BeginCharacterCreation"); Draw("characters.png");
            Tap(input.Regions.First(r => r.Row == 0)); Draw("attributes.png");
            Tap(input.Regions.First(r => r.Row == 0)); Draw("name.png");
            Tap(input.Regions.First(r => r.Row == 2)); Draw("keyboard.png");
            check(input.Regions.Count(r => r.Row >= 2) == 40, "Touch name keyboard renders every supported character as a direct target");
            Tap(input.Regions.First(r => r.Row == 2)); Draw("keyboard-letter.png");
            var current = Get("overlay")!;
            check(((string[])current.GetType().GetProperty("Lines")!.GetValue(current)!)[0].StartsWith("q"),
                "Tapping a keyboard cell enters that character through the existing name flow");
            Call("ShowWelcome"); Set("titleScreen", false); Set("paused", false); Set("pauseAmount", 0f);
            Call("SetTouchInsets", 40, 0, 55, 24); Draw("controls.png");
            var pad = input.Pad;
            check(pad.Left >= 40 && pad.Bottom <= game.GraphicsDevice.Viewport.Height - 24 && input.Regions.Count == 4,
                "Touch world controls respect display insets and expose action, back, map and menu");
            Set("controllerConnected", true); Draw("controller.png");
            check(input.Pad.IsEmpty && input.Regions.Count == 0, "Connecting a controller hides all virtual world controls");
            Set("controllerConnected", false); Draw("controls-return.png");
            check(!input.Pad.IsEmpty, "Touch controls return after controller disconnect");

            var catalog = (MapCatalog)Get("catalog")!;
            var session = new GameSession(catalog, new Player { Name = "Touch Check", Health = 600, MaximumHealth = 600,
                Level = 15, Strength = 40, Dexterity = 1, WeaponRank = 6 }, randomState: 1);
            var adventure = new Adventure(session); var battle = new CombatEncounter();
            for (var i = 0; i < 3; i++) battle.Monsters[i] = new Monster { TypeId = i + 2, Name = CombatCatalog.Monster(i + 2).Name,
                Health = 240, MaximumHealth = 240, AttackRating = 40, DefenseRating = 1, Rank = 1 };
            typeof(Adventure).GetProperty("Battle")!.SetValue(adventure, battle);
            Set("session", session); Set("adventure", adventure); Call("ShowBattle"); Draw("battle.png");
            Tap(input.Regions.First(r => r.Row == 0)); Draw("hit.png");
            current = Get("overlay")!;
            check(current.GetType().GetProperty("Presentation")!.GetValue(current) is not null, "Touch Attack starts the real combat presentation");
            Tap(input.Regions.First(r => r.Row == 0)); Call("UpdateCombatPresentation", .03d);
            check(ReferenceEquals(current, Get("overlay")) && (bool)current.GetType().GetProperty("ContinueRequested")!.GetValue(current)!,
                "Rapid touch confirmation buffers Continue without cutting off the readable combat beat");
        }
        finally
        {
            data.GetType().GetProperty("MusicVolume")!.SetValue(data, oldMusic);
            foreach (var pair in saved) Set(pair.Key, pair.Value);
            input.Reset(); input.Regions.Clear(); Call("SavePreferences");
        }

        void Tap(TouchRegion region)
        {
            Call("ReadTouch", new TouchCollection([]));
            var point = region.Bounds.Center.ToVector2();
            Call("ReadTouch", new TouchCollection([new(1, TouchLocationState.Pressed, point)]));
            var action = (InputFrame)Call("ReadTouch", new TouchCollection([new(1, TouchLocationState.Released, point)]))!;
            if ((bool)Get("paused")!) Call("UpdatePauseMenu", action);
            else if (Get("overlay") is not null) Call("UpdateOverlay", action);
            else Call("UpdateTitleMenu", action);
        }
        void Draw(string name)
        {
            Call("Draw", new GameTime());
            var display = game.GraphicsDevice.PresentationParameters;
            var pixels = new Color[display.BackBufferWidth * display.BackBufferHeight]; game.GraphicsDevice.GetBackBufferData(pixels);
            using var image = new Texture2D(game.GraphicsDevice, display.BackBufferWidth, display.BackBufferHeight); image.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); image.SaveAsPng(file, image.Width, image.Height);
        }
    }
}
