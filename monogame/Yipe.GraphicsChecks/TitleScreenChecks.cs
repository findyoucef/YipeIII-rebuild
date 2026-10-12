using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class TitleScreenChecks
{
    internal static void Run(Game host, string output, Action<bool,string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = host.GetType();
        object? Get(string name) => type.GetField(name,flags)!.GetValue(host);
        void Set(string name, object? value)
        {
            var field = type.GetField(name,flags)!;
            field.SetValue(host,field.FieldType.IsEnum && value is string page ? Enum.Parse(field.FieldType,page) : value);
        }
        void Call(string name, params object[] args) => type.GetMethod(name,flags)!.Invoke(host,args);
        T Property<T>(object value,string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        var saved = new[]{"session","adventure","journal","cameraPosition","sceneTime","paused","pauseAmount","pausePage",
            "selected","selectionY","menuMessage","overlay","mapOpen","worldMapOpen","titleScreen","titleSelected","titleTime",
            "textReceiver","restorePauseOverlay","saves","loadingStore","adventureDirectory","command","pendingEvent","tickRemaining",
            "bindingAction","saving","loading","timeOfDayMode","dayNightSeconds","dayNightUpdatedAt"}.ToDictionary(n=>n,Get);
        var catalog = (MapCatalog)Get("catalog")!;
        var original = (GameSession)Get("session")!;
        var snapshot = GameSaveCodec.Encode(original,catalog);
        var player = (PlayerMotion)Get("player")!; var facing = player.FacingRow;
        var directory = Path.Combine(output,"title-menu"); Directory.CreateDirectory(directory);
        var library = Path.Combine(directory,"saved-adventures-test"); Directory.CreateDirectory(library);
        var graphics = (GraphicsDeviceManager)Get("graphics")!;
        var width = graphics.PreferredBackBufferWidth; var height = graphics.PreferredBackBufferHeight;
        try
        {
            Set("adventureDirectory",library);
            var originalPath = Path.Combine(library,"game.y3m"); File.WriteAllBytes(originalPath,snapshot);
            Set("saves",new GameSaveStore(originalPath,catalog));
            Call("ShowWelcome");
            var platform = typeof(Game).GetField("Platform",flags)!.GetValue(host)!;
            var active = platform.GetType().GetProperty("IsActive")!;
            active.SetValue(platform,false);
            check(!(bool)Get("paused")! && (bool)Get("titleScreen")!, "Title focus loss never exposes the in-game pause menu");
            active.SetValue(platform,true);
            var a = Capture("title.png"); Set("titleTime",8d); var b = Capture("title-later.png");
            check(!a.SequenceEqual(b) && b.SequenceEqual(Capture("title-frozen.png")),
                "Title scenery animates on its own clock and renders deterministically when frozen");
            var scene = (double)Get("sceneTime")!; var displayTime = (double)Get("titleTime")!;
            Call("Update",new GameTime(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(.2)));
            check((double)Get("titleTime")! > displayTime && (double)Get("sceneTime")! == scene
                && snapshot.SequenceEqual(GameSaveCodec.Encode(original,catalog)), "Title update advances only presentation, not gameplay or RNG");
            for (var frame=0;frame<96;frame++) { Set("titleTime",frame/12d); Capture($"title-{frame:D3}.png"); }
            Set("titleSelected",2); Call("UpdateTitleMenu",new InputFrame(Point.Zero,true,false,false));
            check((bool)Get("paused")! && Get("pausePage")!.ToString()=="Options", "Title Options uses the same organized settings pages");
            Set("pauseAmount",1f); Capture("title-options.png");
            Set("selected",4); Call("ActivateMenu"); Call("UpdateOverlay",new InputFrame(Point.Zero,false,true,false));
            check((bool)Get("titleScreen")! && (bool)Get("paused")! && (int)Get("selected")! == 4 && Get("overlay") is null,
                "Controls Back returns to the selected title Options entry");
            Call("UpdatePauseMenu",new InputFrame(Point.Zero,false,true,true));
            check(!(bool)Get("paused")! && (bool)Get("titleScreen")!, "Back from title Options never starts gameplay");
            Set("titleSelected",1); Call("UpdateTitleMenu",new InputFrame(Point.Zero,true,false,false));
            Capture("load-game.png");
            ChooseIndex(0); FinishLoad();
            check(!(bool)Get("titleScreen")! && ((GameSaveStore)Get("saves")!).FilePath==originalPath,
                "Title Load Game opens the selected adventure and adopts its save destination");
            var loaded = (GameSession)Get("session")!; var loadedBytes = GameSaveCodec.Encode(loaded,catalog);
            Call("ShowActions"); var previousOverlay = Get("overlay"); Set("mapOpen",true);
            Set("paused",true); Set("pausePage","Options"); Set("selected",5); Call("ActivateMenu");
            check(!(bool)Get("mapOpen")!, "Pause Help remains visible when opened over the map");
            Call("UpdateOverlay",new InputFrame(Point.Zero,false,true,false));
            check(ReferenceEquals(previousOverlay,Get("overlay")) && (bool)Get("paused")!
                && (bool)Get("mapOpen")! && loadedBytes.SequenceEqual(GameSaveCodec.Encode(loaded,catalog)), "Pause Help restores the suspended interaction and map without consuming a turn");
            Set("pausePage","Main"); Set("selected",3); Call("ActivateMenu");
            Capture("return-to-title.png"); ChooseIndex(0);
            check(ReferenceEquals(previousOverlay,Get("overlay")) && (bool)Get("paused")! && !(bool)Get("titleScreen")!,
                "Canceling Return to title restores the paused interaction");
            Call("ActivateMenu"); ChooseIndex(1);
            check((bool)Get("titleScreen")! && Get("overlay") is null && File.ReadAllBytes(originalPath).SequenceEqual(snapshot),
                "Confirmed Return to title preserves the existing save");
            Call("UpdateTitleMenu",new InputFrame(Point.Zero,true,false,false));
            Choose("Combat"); Choose("Use these attributes"); Choose("Done"); Choose("Medium"); Choose("Classic"); Choose("Knight");
            var first = (GameSaveStore)Get("saves")!; var created = (GameSession)Get("session")!;
            first.SaveAsync(created).GetAwaiter().GetResult();
            check(first.FilePath != originalPath && File.Exists(first.FilePath) && File.ReadAllBytes(originalPath).SequenceEqual(snapshot),
                "Saving a new adventure creates a separate slot without overwriting the loaded adventure");
            Call("ShowWelcome"); Call("UpdateTitleMenu",new InputFrame(Point.Zero,true,false,false));
            Choose("Combat"); Choose("Use these attributes"); Choose("Done"); Choose("Medium"); Choose("Classic"); Choose("Knight");
            var second = (GameSaveStore)Get("saves")!;
            check(second.FilePath != first.FilePath && second.FilePath != originalPath,
                "Even consecutive new adventures receive distinct save destinations");
            Call("ShowWelcome"); Set("titleSelected",1); Call("UpdateTitleMenu",new InputFrame(Point.Zero,true,false,false));
            check(Property<Array>(Get("overlay")!,"Options").Length==3, "Load Game lists both saved adventures and excludes the unsaved new slot");
            Capture("load-multiple-adventures.png");
            // A corrupt file must not become the live save target or replace the current session.
            var corrupt = Path.Combine(library,"corrupt.y3m"); File.WriteAllText(corrupt,"invalid");
            var priorStore = Get("saves"); var priorSession = Get("session");
            Call("ShowLoadGames"); Choose("Unavailable: corrupt"); FinishLoad();
            check((bool)Get("titleScreen")! && ReferenceEquals(priorStore,Get("saves")) && ReferenceEquals(priorSession,Get("session"))
                && Property<string>(Get("overlay")!,"Title")=="Load game", "Corrupt title loads preserve the current adventure and display a recoverable error");
            Capture("load-error.png");
            foreach (var size in new[]{new Point(640,360),new Point(1024,768),new Point(1920,1080)})
            {
                graphics.PreferredBackBufferWidth=size.X; graphics.PreferredBackBufferHeight=size.Y; graphics.ApplyChanges();
                Call("ShowWelcome"); Capture($"title-{size.X}x{size.Y}.png");
            }
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key,entry.Value);
            Set("renderedMap",null); Call("SynchronizeWorld",false);
            player.Face(facing switch {1=>new(-1,0),2=>new(1,0),3=>new(0,-1),_=>new(0,1)});
            graphics.PreferredBackBufferWidth=width; graphics.PreferredBackBufferHeight=height; graphics.ApplyChanges();
            Set("paused",saved["paused"]);
        }
        void Choose(string label)
        {
            var options=Property<Array>(Get("overlay")!,"Options").Cast<object>().ToArray();
            var index=Array.FindIndex(options,o=>Property<string>(o,"Label")==label);
            if(index<0) throw new InvalidOperationException("Missing title choice: "+label);
            ChooseIndex(index);
        }
        void ChooseIndex(int index)
        {
            Get("overlay")!.GetType().GetProperty("Selected")!.SetValue(Get("overlay"),index);
            Call("UpdateOverlay",new InputFrame(Point.Zero,true,false,false));
        }
        void FinishLoad()
        {
            try { ((Task)Get("loading")!).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { }
            Call("FinishStorage");
        }
        Color[] Capture(string name)
        {
            Call("Draw",new GameTime());
            var display=host.GraphicsDevice.PresentationParameters;
            var pixels=new Color[display.BackBufferWidth*display.BackBufferHeight]; host.GraphicsDevice.GetBackBufferData(pixels);
            using var image=new Texture2D(host.GraphicsDevice,display.BackBufferWidth,display.BackBufferHeight); image.SetData(pixels);
            using var file=File.Create(Path.Combine(directory,name)); image.SaveAsPng(file,image.Width,image.Height);
            return pixels;
        }
    }
}
