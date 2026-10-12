using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

/// <summary>Normal paced Game.Run, with screenshots excluded from measured intervals.</summary>
internal sealed class PerformanceChecks : DrawableGameComponent
{
    private static readonly (string Name, int Map)[] Scenes =
        [("maynard",17),("snow",13),("forest",20),("ervyn",27),("breeding",60),
         ("fire",72),("ice",41),("hell",117),("three-enemy-battle",17)];
    private readonly string output;
    private readonly Type type;
    private readonly List<object> results = [];
    private readonly List<double> intervals = [], submissions = [];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double previous, updateEnd;
    private long allocated;
    private int phase, frame;
    private bool started, completed;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private PerformanceChecks(Game game, string output) : base(game)
    { this.output = output; type = game.GetType(); }

    internal static int Run(string directory)
    {
        var output = Path.GetFullPath(directory); Directory.CreateDirectory(output);
        using var deadline = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromMinutes(4), deadline.Token).ContinueWith(_ => Environment.Exit(1),
            CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        try
        {
            using var maps = File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Content","maps.json"));
            var catalog = MapCatalog.Load(maps); var save = Path.Combine(output,"fixture.y3m");
            File.WriteAllBytes(save,GameSaveCodec.Encode(new GameSession(catalog),catalog));
            // Fresh profile uses the shipping visual defaults; audio remains enabled.
            File.Delete(Path.ChangeExtension(save,".profile.json"));
            var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.TownGame",true)!;
            using var game = (Game)Activator.CreateInstance(type,[false,null,save])!;
            var observer = new PerformanceChecks(game,output); game.Components.Add(observer); game.Run();
            if (!observer.completed) throw new InvalidOperationException("Performance pass did not finish.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { deadline.Cancel(); }
    }

    public override void Update(GameTime gameTime) => updateEnd = clock.Elapsed.TotalMilliseconds;

    public override void Draw(GameTime gameTime)
    {
        if (Get("loading") is not null) return;
        if (!Game.IsActive) throw new InvalidOperationException("Keep the performance window focused.");
        if (!started) { started = true; Scene(); return; }
        var now = clock.Elapsed.TotalMilliseconds;
        frame++;
        if (frame == 59) Capture();
        if (frame == 60) allocated = GC.GetTotalAllocatedBytes();
        if (frame > 60)
        {
            intervals.Add(now-previous);
            submissions.Add(now-updateEnd);
        }
        previous = clock.Elapsed.TotalMilliseconds;
        if (Scenes[phase % Scenes.Length].Name == "three-enemy-battle")
            Get("overlay")!.GetType().GetProperty("CombatTime")!.SetValue(Get("overlay"), frame % 150 / 60d);
        if (frame < 360) return;
        var scene = Scenes[phase % Scenes.Length];
        var fps = 1000 / intervals.Average();
        var result = new {
            scene = scene.Name, map = scene.Map, width = phase < Scenes.Length ? 1280 : 1920,
            samples = intervals.Count, fps, intervalP95Ms = Percentile(intervals,.95),
            intervalP99Ms = Percentile(intervals,.99), longestIntervalMs = intervals.Max(),
            intervalsOver25Ms = intervals.Count(x=>x>25), cpuDrawSubmissionP95Ms = Percentile(submissions,.95),
            allocatedBytesPerFrame = (GC.GetTotalAllocatedBytes()-allocated)/intervals.Count,
            workingSetBytes = Environment.WorkingSet,
        };
        results.Add(result); Console.WriteLine(JsonSerializer.Serialize(result));
        phase++;
        if (phase < Scenes.Length*2) { Scene(); return; }
        File.WriteAllText(Path.Combine(output,"performance.json"),JsonSerializer.Serialize(new {
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            adapter = GraphicsDevice.Adapter.Description,
            method = "Normal fixed 60 Hz Game.Run; 60 warmup and 300 measured frames per scene/resolution. Screenshots excluded. CPU draw submission excludes GPU completion; intervals include presentation and frame pacing. Fresh shipping visual defaults and audio enabled. Diagnostic scenes reveal all cells, freeze world ticks, retain visual animation, and loop a real three-enemy attack presentation. Forest includes automatic rain.",
            results,
        },new JsonSerializerOptions {WriteIndented=true}));
        completed=true; Game.Exit();
    }

    private void Scene()
    {
        var scene=Scenes[phase%Scenes.Length]; var catalog=(MapCatalog)Get("catalog")!;
        var layer=scene.Map>=99?0:scene.Map>=41?1:0;
        var cell=scene.Map-1-layer*40-(scene.Map>=99?98:0);
        var hero=new Player { Name="Performance", Health=700, MaximumHealth=700, Level=15, Strength=40,
            Dexterity=20, Intelligence=35, WeaponRank=6, SpellPoints=200, MaximumSpellPoints=200 };
        var session=new GameSession(catalog,hero,new() { MapId=scene.Map, MapLayer=layer,
            AlternateMapSet=scene.Map>=99, RegionX=cell%6-4, RegionY=2-cell/6, Position=new(5,5) });
        var adventure=new Adventure(session);
        for(var x=0;x<Map.Width;x++) for(var y=0;y<Map.Height;y++) adventure.Visibility.RevealFrom(session.CurrentMap,new(x,y));
        Set("session",session); Set("adventure",adventure); Set("overlay",null);
        Set("paused",false); Set("pauseAmount",0f); Set("titleScreen",false); Set("sceneTime",40d);
        Set("dayNightSeconds",300d);
        var weather=type.Assembly.GetType("Yipe.Desktop.Rain")!.GetMethod("WeatherAmount")!;
        Set("outdoorWeatherTime",scene.Name=="forest" ? Enumerable.Range(300,360).First(t=>(float)weather.Invoke(null,[(double)t])!>.99f)+0d : 0d);
        Set("tickRemaining",100000d);
        Call("SynchronizeWorld",false);
        if (scene.Name=="three-enemy-battle")
        {
            var battle=new CombatEncounter();
            for(var i=0;i<3;i++) battle.Monsters[i]=new Monster { TypeId=35+i, Name=CombatCatalog.Monster(35+i).Name,
                Health=900,MaximumHealth=900,AttackRating=40,DefenseRating=1,Rank=1 };
            typeof(Adventure).GetProperty("Battle")!.SetValue(adventure,battle); Call("ShowBattle");
            Call("UpdateOverlay", new InputFrame(Point.Zero,true,false,false));
        }
        var graphics=(GraphicsDeviceManager)Get("graphics")!;
        graphics.PreferredBackBufferWidth=phase<Scenes.Length?1280:1920;
        graphics.PreferredBackBufferHeight=phase<Scenes.Length?720:1080; graphics.ApplyChanges();
        frame=0; intervals.Clear(); submissions.Clear(); previous=clock.Elapsed.TotalMilliseconds;
    }
    private static double Percentile(List<double> values,double fraction)=>values.Order().ElementAt((int)Math.Ceiling(values.Count*fraction)-1);
    private object? Get(string name)=>type.GetField(name,Flags)!.GetValue(Game);
    private void Set(string name,object? value)=>type.GetField(name,Flags)!.SetValue(Game,value);
    private void Call(string name,params object[] args)=>type.GetMethod(name,Flags)!.Invoke(Game,args);
    private void Capture()
    {
        var pp=GraphicsDevice.PresentationParameters;
        using var image=new Texture2D(GraphicsDevice,pp.BackBufferWidth,pp.BackBufferHeight);
        var pixels=new Color[image.Width*image.Height]; GraphicsDevice.GetBackBufferData(pixels); image.SetData(pixels);
        using var file=File.Create(Path.Combine(output,$"{phase:D2}-{Scenes[phase%Scenes.Length].Name}.png"));
        image.SaveAsPng(file,image.Width,image.Height);
    }
}
