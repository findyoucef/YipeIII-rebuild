using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class HelpControlChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        var type = game.GetType();
        object Get(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
        void Set(string name, object? value)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(game, field.FieldType.IsEnum && value is string page ? Enum.Parse(field.FieldType, page) : value);
        }
        void Call(string name, params object[] args) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, args);
        T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        object Overlay() => Get("overlay");
        string Text() => string.Join("\n", Property<string[]>(Overlay(), "Lines"));
        void Control(InputFrame frame) => Call("UpdateOverlay", frame);
        void Choose(int index)
        {
            var overlay = Overlay();
            overlay.GetType().GetProperty("Selected")!.SetValue(overlay, index);
            Control(new(Point.Zero, true, false, false));
        }
        void Back() => Control(new(Point.Zero, false, true, false));
        var profile = Get("profile");
        var data = Property<object>(profile, "Data");
        var controls = Property<ControlBindings>(data, "Controls");
        var session = (GameSession)Get("session");
        var catalog = (MapCatalog)Get("catalog");
        var before = GameSaveCodec.Encode(session, catalog);
        var scene = (double)Get("sceneTime");
        var graphics = (GraphicsDeviceManager)Get("graphics");
        var width = graphics.PreferredBackBufferWidth;
        var height = graphics.PreferredBackBufferHeight;
        var paused = (bool)Get("paused");
        var previousPage = Get("pausePage");
        var selected = (int)Get("selected");
        var message = (string)Get("menuMessage");
        var directory = Path.Combine(output, "help-controls");
        Directory.CreateDirectory(directory);
        try
        {
            var menus = (string[][])type.GetField("PauseMenus", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            check(menus[0].Length == 5 && menus.All(m => m.Length <= 7)
                && menus[1].Contains("New adventure") && menus[2].Contains("Sound"),
                "Short pause pages group adventure actions, display, atmosphere and audio");
            Set("paused", true); Set("pauseAmount", 1f); Set("pausePage", "Main");
            Set("selected", 2); Set("selectionY", 24f); Set("menuMessage", "");
            Capture("pause-menu.png");
            Call("UpdatePauseMenu", new InputFrame(Point.Zero, true, false, false));
            check(Get("pausePage").ToString() == "Options" && (bool)Get("paused") && (int)Get("selected") == 0,
                "Confirm opens Options while play stays paused");
            Capture("options-menu.png");
            Call("UpdatePauseMenu", new InputFrame(Point.Zero, true, false, false));
            check(Get("pausePage").ToString() == "Display" && (int)Get("selected") == 0, "Display opens at its first setting");
            Capture("display-menu.png");
            Call("UpdatePauseMenu", new InputFrame(new(0,-1), false, false, false));
            check((int)Get("selected") == 5, "Up wraps to the visible Back entry");
            Call("UpdatePauseMenu", new InputFrame(new(0,1), false, false, false));
            check((int)Get("selected") == 0, "Down wraps to the first setting");
            Call("UpdatePauseMenu", new InputFrame(new(0,1), false, false, false));
            var scanlines = (bool)Get("scanlines");
            Call("ActivateMenu");
            check((bool)Get("scanlines") != scanlines && (bool)Get("paused") && Get("pausePage").ToString() == "Display",
                "Settings apply without leaving their paused category");
            Call("ActivateMenu");
            Call("UpdatePauseMenu", new InputFrame(Point.Zero,false,true,true));
            check(Get("pausePage").ToString() == "Options" && (int)Get("selected") == 0 && (float)Get("selectionY") == 0,
                "Escape returns one level to the highlighted parent category");
            Set("selected",1); Call("ActivateMenu"); Capture("atmosphere-menu.png");
            Set("selected",5); Call("ActivateMenu");
            check(Get("pausePage").ToString() == "Options" && (int)Get("selected") == 1, "Visible Back behaves like Escape");
            Set("selected",2); Call("ActivateMenu"); Capture("audio-menu.png");
            var audio = Get("audio"); var enabled = Property<bool>(audio,"Enabled"); var volume = Property<float>(audio,"Volume");
            Call("ActivateMenu");
            check(Property<bool>(audio,"Enabled") != enabled, "Audio settings toggle the existing master sound gate");
            Call("ActivateMenu"); Set("selected",1); Call("UpdatePauseMenu", new InputFrame(new(1,0),false,false,false));
            check(Property<float>(audio,"Volume") != volume, "Audio volume changes through the existing preference path");
            audio.GetType().GetProperty("Volume")!.SetValue(audio,volume); Call("SavePreferences");
            Call("UpdatePauseMenu",new InputFrame(Point.Zero,false,false,true));
            Call("UpdatePauseMenu",new InputFrame(Point.Zero,false,false,true));
            check(Get("pausePage").ToString() == "Main" && (bool)Get("paused") && (int)Get("selected") == 2,
                "Start returns through Options to the highlighted root entry");
            Set("selected",1); Call("ActivateMenu"); Capture("adventure-menu.png");
            Call("UpdatePauseMenu",new InputFrame(Point.Zero,false,true,false));
            Call("UpdatePauseMenu",new InputFrame(Point.Zero,false,false,true));
            check(!(bool)Get("paused") && before.SequenceEqual(GameSaveCodec.Encode(session,catalog)) && (double)Get("sceneTime") == scene,
                "Leaving the root resumes without consuming gameplay, scene time or RNG");
            Set("pauseAmount", 0f); Set("selected", 0); Set("selectionY", 0f);
            Set("paused", false);
            Set("overlay", null); Set("studioSplashRemaining", 3.2);
            Call("UpdateStudioSplash", new InputFrame(), .6);
            check(Math.Abs((double)Get("studioSplashRemaining") - 2.6) < .001,
                "The studio ident advances on its own display clock");
            Capture("icosium-labs-splash.png", 4);
            check(before.SequenceEqual(GameSaveCodec.Encode(session, catalog)) && (double)Get("sceneTime") == scene,
                "Studio branding preserves gameplay state and RNG before the title screen");
            Call("UpdateStudioSplash", new InputFrame(), 3.0);
            check((double)Get("studioSplashRemaining") == 0 && (bool)Get("titleScreen") && Overlay() is null,
                "The studio ident automatically opens the title screen after 3.2 seconds");
            foreach (var skip in new[] { new InputFrame(Point.Zero, true, false, false),
                new InputFrame(Point.Zero, false, true, false), new InputFrame(Point.Zero, false, false, true) })
            {
                Set("overlay", null); Set("studioSplashRemaining", 3.2);
                Call("UpdateStudioSplash", skip, .016);
                check((double)Get("studioSplashRemaining") == 0 && (bool)Get("titleScreen") && Overlay() is null
                    && (int)Get("titleSelected") == 0,
                    "Confirm, Back and Pause skip studio branding without activating a title choice");
            }
            Capture("title-screen.png");
            Set("titleScreen", false);
            Call("ShowHelp");
            Choose(0);
            check(Text().Contains("Actions: Enter") && Text().Contains("South") && !Text().Contains("ENTER, E"),
                "Current help describes the actual default keyboard and physical controller bindings");
            Capture("help-default.png");
            var count = Property<int>(Overlay(), "PageCount");
            for (var page = 1; page < count; page++) Control(new(Point.Zero, true, false, false));
            check(Property<int>(Overlay(), "Page") == count - 1, "Long controls help advances through every page");
            Back();
            check(Property<string>(Overlay(), "Title") == "Help & credits", "Help Back returns to its contents");
            Choose(1);
            Control(new(Point.Zero, true, false, false));
            check(Property<Array>(Overlay(), "Options").Length == 22, "All 21 gameplay guide topics are reachable");
            Choose(0);
            check(Property<string>(Overlay(), "Title") == "Background" && Text().Contains("Quest of Yipe! III"),
                "The recovered original manual opens its original first topic");
            Capture("original-manual.png");
            Back();
            Choose(20);
            check(Property<string>(Overlay(), "Title") == "Keyboard Shortcuts", "The gameplay guide ends with controls rather than commercial pages");
            Back(); Back(); Back();
            Call("ShowAbout");
            check(Text().Contains("Kevin Kinell") && Text().Contains("2002-03") && Text().Contains("1996")
                && Text().Contains("icosium labs") && !Text().Contains("Johnston"),
                "About retains original attribution and credits icosium labs for the port/remaster");
            Capture("about.png"); Back();
            check(Property<string>(Overlay(),"Title") == "Help & credits", "About returns to Help and credits");
            Back();
            Set("paused", false);
            Call("EditControls", 0);
            check(Property<Array>(Overlay(), "Options").Length == 13, "Keyboard, controller and reset options are always reachable");
            Choose(4);
            Set("previousKeyboardState", new KeyboardState(Keys.Enter));
            Call("UpdateBinding", new KeyboardState(Keys.Enter));
            check((int)Get("bindingAction") == 4, "The key that opened capture is not immediately rebound");
            Set("previousKeyboardState", default(KeyboardState));
            Call("UpdateBinding", new KeyboardState(Keys.M));
            check(controls.Keyboard[4] == Keys.Enter && Text().Contains("already assigned"), "A conflicting captured key is rejected visibly");
            Back(); Choose(4);
            Call("UpdateBinding", new KeyboardState(Keys.LeftControl, Keys.E));
            check(controls.Keyboard[4] == Keys.Enter && Text().Contains("single key"), "A modifier chord cannot silently become a single-key binding");
            Back(); Choose(4);
            Call("UpdateBinding", new KeyboardState(Keys.Escape));
            check((int)Get("bindingAction") == -1 && Property<string>(Overlay(), "Title") == "Edit controls",
                "Escape cancels keyboard capture without changing controls");
            Choose(4); Back();
            check((int)Get("bindingAction") == -1 && controls.Keyboard[4] == Keys.Enter,
                "The standard controller Back action can cancel keyboard capture");
            Choose(4); Call("UpdateBinding", new KeyboardState(Keys.E));
            check(controls.Keyboard[4] == Keys.E && (string)Get("menuMessage") == "Preferences saved.",
                "A valid captured key is applied and saved through the production preference path");
            Call("ShowCurrentControls");
            check(Text().Contains("Actions: E"), "Help immediately reflects a changed gameplay binding");
            Call("EditControls", 7); Choose(7); Choose(1);
            check(controls.Controller[0] == Buttons.A && Text().Contains("already assigned"), "Controller conflicts are rejected through the real chooser");
            Back(); Choose(2);
            check(controls.Controller[0] == Buttons.X, "The controller chooser applies a physical West button remap");
            var reopened = Activator.CreateInstance(profile.GetType(), [Path.ChangeExtension(Path.Combine(output, "test-saves", "startup-import.y3m"), ".profile.json")])!;
            var restored = Property<ControlBindings>(Property<object>(reopened, "Data"), "Controls");
            check(restored.Keyboard[4] == Keys.E && restored.Controller[0] == Buttons.X, "UI-applied remaps reopen from the isolated profile");
            Capture("remapped-controls.png");
            foreach (var size in new[] { new Point(640, 360), new Point(1024, 768), new Point(1920, 1080) })
            {
                graphics.PreferredBackBufferWidth = size.X; graphics.PreferredBackBufferHeight = size.Y; graphics.ApplyChanges();
                // A resize can deactivate the native window and pause the game. Capture the editor itself.
                Set("paused", false); Set("pauseAmount", 0f);
                Call("EditControls", 12);
                Capture($"controls-{size.X}x{size.Y}.png");
                Back();
                Set("pausePage", "Main"); Set("pauseAmount", 1f); Set("selected", 4); Set("selectionY", 48f);
                Capture($"pause-{size.X}x{size.Y}.png");
                Set("selected", 2); Call("ActivateMenu"); Set("selected", 1); Call("ActivateMenu"); Set("selected", 4); Set("selectionY", 48f);
                Capture($"graphics-{size.X}x{size.Y}.png");
                Call("UpdatePauseMenu", new InputFrame(Point.Zero, false, true, false));
                Set("paused", false);
                Call("EditControls", 12);
                check(!(bool)Get("paused") && Property<string>(Overlay(), "Title") == "Edit controls",
                    $"Controls can be reopened and canceled at {size.X}x{size.Y}");
            }
            Call("EditControls", 11); Choose(11); Back();
            check(controls.Keyboard[4] == Keys.E, "Canceling reset retains gameplay bindings");
            Choose(11); Choose(1);
            check(Property<ControlBindings>(data, "Controls").Keyboard[4] == Keys.Enter, "Confirmed reset restores standard gameplay bindings");
            // An unreadable profile must not be overwritten or crash the control editor.
            var rejectedPath = Path.Combine(directory, "unreadable.profile.json");
            File.WriteAllText(rejectedPath, "{");
            Set("profile", Activator.CreateInstance(profile.GetType(), [rejectedPath]));
            Call("EditControls", 4); Choose(4); Call("UpdateBinding", new KeyboardState(Keys.E));
            check(((string)Get("menuMessage")).StartsWith("Preferences could not be saved") && File.ReadAllText(rejectedPath) == "{",
                "Failed control-preference writes retain the session and preserve the unreadable file");
            check(before.SequenceEqual(GameSaveCodec.Encode(session, catalog)) && (double)Get("sceneTime") == scene,
                "Help, About, remapping, display changes and preference failures preserve complete gameplay state/RNG");
            Console.WriteLine("Physical controllers connected: " + Enumerable.Range(0, 4).Count(i => GamePad.GetState((PlayerIndex)i).IsConnected));
        }
        finally
        {
            Set("profile", profile); data.GetType().GetProperty("Controls")!.SetValue(data, new ControlBindings());
            Call("SavePreferences");
            Set("bindingAction", -1); Set("overlay", null); Set("paused", paused); Set("selected", selected); Set("pausePage", previousPage);
            Set("studioSplashRemaining", 0.0); Set("titleScreen", false); Set("restorePauseOverlay", null);
            Set("menuMessage", message); Set("pauseAmount", 0f); Set("selectionY", 0f);
            graphics.PreferredBackBufferWidth = width; graphics.PreferredBackBufferHeight = height; graphics.ApplyChanges();
        }

        void Capture(string name, int minimumColors = 16)
        {
            Call("Draw", new GameTime());
            var display = game.GraphicsDevice.PresentationParameters;
            var pixels = new Color[display.BackBufferWidth * display.BackBufferHeight];
            game.GraphicsDevice.GetBackBufferData(pixels);
            using var texture = new Texture2D(game.GraphicsDevice, display.BackBufferWidth, display.BackBufferHeight);
            texture.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name));
            texture.SaveAsPng(file, texture.Width, texture.Height);
            check(pixels.Distinct().Take(minimumColors).Count() == minimumColors, "UI capture renders at " + name);
        }
    }
}
