using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

// Offline capture through the existing game renderer/actions. All adventures and preferences are isolated.
var output = Path.GetFullPath(args.FirstOrDefault() ?? "build/trailer-20261020-v3");
var stills = args.Contains("--stills");
var only = args.FirstOrDefault(a => a.StartsWith("--shot="))?[7..];
Directory.CreateDirectory(output);
var content = Path.Combine(AppContext.BaseDirectory, "Content");
using var maps = File.OpenRead(Path.Combine(content, "maps.json"));
var catalog = MapCatalog.Load(maps);
var save = Path.Combine(output, "capture-save", "game.y3m");
new GameSaveStore(save, catalog).SaveAsync(new GameSession(catalog)).GetAwaiter().GetResult();
var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.TownGame", true)!;
const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
using var game = (Game)Activator.CreateInstance(type, [false, null, save])!;
object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
object? Call(string name, params object?[] values) => type.GetMethod(name, flags)!.Invoke(game, values);
var graphics = (GraphicsDeviceManager)Get("graphics")!;
graphics.SynchronizeWithVerticalRetrace = false;
for (var i = 0; i < 6; i++) game.RunOneFrame();
if (Get("loading") is Task loading) { loading.GetAwaiter().GetResult(); Call("FinishStorage"); }
Set("loading", null); Set("saving", null); Set("studioSplashRemaining", 0d);
Set("touchEnabled", false); Set("titleScreen", false); Set("scanlines", false);
// The shipped Strong miniature setting is retained. Disable only display scanlines for clean encoding.
var audio = Get("audio")!; audio.GetType().GetProperty("Enabled")!.SetValue(audio, false);
var profile = Get("profile")!; var data = profile.GetType().GetProperty("Data")!.GetValue(profile)!;
data.GetType().GetProperty("Music")!.SetValue(data, false);
var player = (PlayerMotion)Get("player")!;
using var batch = new SpriteBatch(game.GraphicsDevice);
using var pixel = new Texture2D(game.GraphicsDevice, 1, 1); pixel.SetData([Color.White]);
var font = Get("font")!; var drawText = font.GetType().GetMethod("Draw")!;
var pixels = new Color[1280 * 720];
using var image = new Texture2D(game.GraphicsDevice, 1280, 720);
var shots = new[] {
    new Shot("01-ident", 17, new(3,3), 2.5, ""),
    new Shot("02-maynard", 17, new(3,10), 6, "UUUURRUUURRRRRU"),
    new Shot("03-overworld", 20, new(11,10), 4, "LLLLLUUURRURRR"),
    new Shot("04-snow", 13, new(6,5), 3.5, ""),
    new Shot("05-ice", 41, new(6,5), 4, "LL"),
    new Shot("06-raft", 62, new(4,7), 3, ""),
    new Shot("07-fire-battle", 111, new(6,9), 8, "RRUUUUUUUUL"),
    new Shot("09-hell", 117, new(5,5), 3, ""),
    new Shot("10-night", 17, new(10,2), 3, "LLLLLLDD"),
    new Shot("11-title", 17, new(8,3), 8, "")
};
File.WriteAllText(Path.Combine(output, "shots.json"), JsonSerializer.Serialize(shots, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
foreach (var shot in shots.Where(s => only is null || s.Name == only))
{
    Console.WriteLine($"Capturing {shot.Name}: {shot.Seconds}s, map {shot.Map}");
    (Get("command") as IDisposable)?.Dispose(); Set("command", null); Set("pendingEvent", null); Set("overlay", null);
    Set("paused", false); Set("pauseAmount", 0f); Set("mapOpen", false); Set("worldMapOpen", false);
    Set("pendingDirection", Point.Zero); Set("iceDirection", Point.Zero); Set("walkingStep", false);
    Set("sceneTime", 12d); Set("messageRemaining", 0d); Set("tickRemaining", 1000d);
    Set("timeOfDayMode", shot.Name is "10-night" or "11-title" ? 3 : 1);
    Set("rainStrength", 0);
    var layer = shot.Map >= 117 ? 0 : (shot.Map - 1) / 40;
    var cell = shot.Map >= 117 ? shot.Map - 99 : (shot.Map - 1) % 40;
    var source = catalog.CreateMap(shot.Map)!;
    var at = shot.Name == "06-raft" ? source.Events.Overlay.Position : shot.At;
    if (shot.Map != 17 && shot.Name != "06-raft")
        at = (from y in Enumerable.Range(1,10) from x in Enumerable.Range(1,11)
              let p = new MapPosition(x,y) where source.Tile(p) is 2 or 0x1f or 0x53 or 0x49 or 0x27 or 0x28 or 0x47 or 0x48
              where !source.Events.Depth.Any(d => d.HasPosition && d.Position == p)
              orderby Math.Abs(x-at.X)+Math.Abs(y-at.Y) select p).FirstOrDefault(at);
    var hero = new Player { Name = "Adventurer", Level = 15, Health = 120, MaximumHealth = 120,
        Strength = 40, Dexterity = 20, WeaponRank = 15, RandomEncounterThreshold = 32767 };
    var session = new GameSession(catalog, hero, new() { MapId = shot.Map, MapLayer = layer,
        AlternateMapSet = shot.Map >= 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = at }, randomState: 8);
    var adventure = new Adventure(session);
    for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) adventure.Visibility.RevealFrom(adventure.Map, new(x,y));
    Set("session", session); Set("adventure", adventure); Set("journal", new MapJournal()); Call("SynchronizeWorld", false);
    using var encoder = stills ? null : Encoder(Path.Combine(output, shot.Name + ".mp4"));
    var errors = encoder?.StandardError.ReadToEndAsync();
    var route = 0;
    var battleOpened = -1;
    var cues = new List<(double At, int Sound)>();
    var footsteps = new List<(double At, string Surface)>();
    var motion = new List<object>();
    var count = stills ? 1 : (int)Math.Round(shot.Seconds * 60);
    try
    {
        for (var frame = 0; frame < count; frame++)
        {
            var t = frame / 60d;
            if (shot.Name == "06-raft" && frame % 54 == 0)
            {
                var before = adventure.Map.Events.Overlay.Position;
                for (var attempt = 0; attempt < 60 && before == adventure.Map.Events.Overlay.Position; attempt++)
                    Call("StartCommand", adventure.Tick());
            }
            if (shot.Name == "07-fire-battle")
            {
                var current = Get("overlay");
                if (battleOpened < 0 && adventure.Battle is not null && current is not null
                    && (bool)current.GetType().GetProperty("Combat")!.GetValue(current)!)
                {
                    battleOpened = frame;
                    var monster = adventure.Battle.Monsters[0];
                    Console.WriteLine($"Walked into {monster.Name} at {player.Position}; combat opened at {t:F3}s.");
                    File.WriteAllText(Path.Combine(output, "encounter.json"), JsonSerializer.Serialize(new {
                        At = t, Map = session.CurrentMap.Id, player.Position, monster.TypeId, monster.Name,
                        StepsCompleted = route, Entry = "Adventure.Move into authored placement"
                    }, new JsonSerializerOptions { WriteIndented = true }));
                }
                var battleFrame = battleOpened < 0 ? -1 : frame - battleOpened;
                if (battleFrame == 27 || battleFrame == 147)
                {
                    Choose("Attack");
                    var overlay = Get("overlay")!;
                    var presentation = overlay.GetType().GetProperty("Presentation")!.GetValue(overlay)!;
                    var presentationType = presentation.GetType();
                    var impact = (double)presentationType.GetField("Impact")!.GetRawConstantValue()!;
                    cues.Add((t + impact, (int)presentationType.GetProperty("Sound")!.GetValue(presentation)!));
                    var damage = (int[])presentationType.GetProperty("DamageFromMonsters")!.GetValue(presentation)!;
                    for (var slot = 0; slot < damage.Length; slot++)
                        if (damage[slot] > 0)
                            cues.Add((t + (double)presentationType.GetMethod("CounterAt")!.Invoke(presentation, [slot])!, 134));
                }
                if (battleFrame == 132) Choose("Continue");
            }
            // Existing update animates accepted steps, ice traction, raft support and combat beats.
            Set("paused", false);
            var previousPosition = player.Position;
            var wasWalking = player.IsMoving && (bool)Get("walkingStep")!;
            Call("Update", new GameTime(TimeSpan.FromSeconds(t), TimeSpan.FromSeconds(1d/60)));
            if (wasWalking && (!player.IsMoving || player.Position != previousPosition))
            {
                var renderer = Get(shot.Map == TownMap.Id ? "town" : "world")!;
                var surface = renderer.GetType().GetMethod("FootstepAt", flags)!.Invoke(renderer,
                    shot.Map == TownMap.Id ? [previousPosition] : [session.CurrentMap, previousPosition])!;
                footsteps.Add((t, surface.ToString()!.ToLowerInvariant()));
            }
            // Start the next accepted step before drawing the completion frame. The old
            // 34-frame schedule left a visible standing pause after every 15-frame walk.
            if (frame >= 9 && route < shot.Route.Length && !player.IsMoving && Get("overlay") is null
                && Get("command") is null && Get("pendingEvent") is null)
            {
                var direction = shot.Route[route++] switch { 'L' => new Point(-1,0), 'R' => new(1,0), 'U' => new(0,-1), _ => new(0,1) };
                var before = player.Position;
                Call("MovePlayer", direction, direction);
                if (player.Position == before || session.CurrentMap.Id != shot.Map)
                    throw new InvalidOperationException($"Capture route blocked in {shot.Name} at {before}, step {route}.");
            }
            if (shot.Route.Length > 0)
                motion.Add(new { Frame = frame, X = player.VisualPosition.X, Y = player.VisualPosition.Y,
                    Moving = player.IsMoving, player.AnimationFrame });
            Set("messageRemaining", 0d);
            Call("Draw", new GameTime());
            if (shot.Name == "01-ident") Ident(stills ? 1 : t);
            if (shot.Name == "11-title") Title(stills ? 2 : t);
            game.GraphicsDevice.GetBackBufferData(pixels);
            if (frame == 0 || frame == 90 || frame == count - 1)
            {
                image.SetData(pixels);
                using var file = File.Create(Path.Combine(output, shot.Name + $"-{frame:D3}.png"));
                image.SaveAsPng(file, 1280,720);
            }
            encoder?.StandardInput.BaseStream.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
        }
    }
    finally { encoder?.StandardInput.Close(); }
    if (encoder is not null)
    {
        if (!encoder.WaitForExit(30000)) { encoder.Kill(true); throw new TimeoutException("FFmpeg capture did not finish."); }
        if (encoder.ExitCode != 0) throw new Exception(errors!.GetAwaiter().GetResult());
    }
    if (shot.Name == "07-fire-battle")
    {
        if (!stills && (battleOpened < 0 || adventure.Battle?.Monsters[0].TypeId != 14 || player.Position != new MapPosition(7,1)))
            throw new InvalidOperationException("The walk did not trigger the authored Flame Flier encounter.");
        File.WriteAllText(Path.Combine(output, "combat-cues.json"), JsonSerializer.Serialize(
            cues.Where(c => c.At < shot.Seconds).Select(c => new { c.At, c.Sound }), new JsonSerializerOptions { WriteIndented = true }));
    }
    File.WriteAllText(Path.Combine(output, shot.Name + "-footsteps.json"), JsonSerializer.Serialize(
        footsteps.Select(c => new { c.At, c.Surface }), new JsonSerializerOptions { WriteIndented = true }));
    if (motion.Count > 0)
        File.WriteAllText(Path.Combine(output, shot.Name + "-motion.json"), JsonSerializer.Serialize(motion));
    if (!stills && (route != shot.Route.Length || shot.Name != "07-fire-battle" && Get("overlay") is not null))
        throw new InvalidOperationException($"Capture route interrupted in {shot.Name}: {route}/{shot.Route.Length} steps.");
    Console.WriteLine($"Finished {shot.Name}: hero {player.Position}, overlay {Get("overlay")?.GetType().Name ?? "none"}");
}
game.Exit();

void Choose(string prefix)
{
    var overlay = Get("overlay") ?? throw new InvalidOperationException($"No menu for scripted action {prefix}.");
    var options = ((Array)overlay.GetType().GetProperty("Options")!.GetValue(overlay)!).Cast<object>().ToArray();
    var index = Array.FindIndex(options, o => ((string)o.GetType().GetProperty("Label")!.GetValue(o)!).StartsWith(prefix));
    if (index < 0) throw new InvalidOperationException($"Scripted action {prefix} is not available.");
    overlay.GetType().GetProperty("Selected")!.SetValue(overlay, index);
    Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
}
void Text(string text, int y, int scale, Color color)
    => drawText.Invoke(font, [batch, text, (1280-text.Length*6*scale)/2, y, color, scale]);
void Ident(double t)
{
    game.GraphicsDevice.Clear(new Color(10,18,27));
    var logo = (Texture2D)Get("studioLogo")!;
    var alpha = (float)Math.Clamp(Math.Min(t/.35, (2.5-t)/.35),0,1);
    batch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
    var width = 560; var height = width * logo.Height / logo.Width;
    batch.Draw(logo, new Rectangle((1280-width)/2,(720-height)/2,width,height),Color.White*alpha);
    batch.End();
}
void Title(double t)
{
    batch.Begin(samplerState: SamplerState.PointClamp);
    batch.Draw(pixel, new Rectangle(0,0,1280,720), new Color(6,12,20)*.79f);
    var gold = new Color(239,201,112); var ivory = new Color(245,236,209);
    Text("ICOSIUM LABS",95,3,gold);
    Text("QUEST OF",185,5,ivory);
    Text("YIPE! III",242,16,gold);
    Text("YOUR NEXT ADVENTURE AWAITS",396,3,ivory);
    Text("MACOS  /  ANDROID IN DEVELOPMENT",463,2,new Color(177,194,203));
    Text("BASED ON THE ORIGINAL ADVENTURE BY KEVIN KINELL",507,2,ivory);
    Text("WITH ORIGINAL ARTWORK BY PAT KELLEY",535,2,new Color(177,194,203));
    Text("CELTIC IMPULSE - KEVIN MACLEOD",582,2,ivory);
    Text("INCOMPETECH.COM / CC BY 4.0",606,2,ivory);
    Text("CREATIVECOMMONS.ORG/LICENSES/BY/4.0/",630,2,new Color(177,194,203));
    Text("MUSIC EXCERPT EDITED AND MIXED FOR THIS TRAILER",654,1,new Color(177,194,203));
    if (t > 7.5) batch.Draw(pixel,new Rectangle(0,0,1280,720),Color.Black*(float)((t-7.5)/.5));
    batch.End();
}
static Process Encoder(string path)
{
    var start = new ProcessStartInfo("ffmpeg") { RedirectStandardInput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var arg in new[] { "-y", "-v", "error", "-f", "rawvideo", "-pixel_format", "rgba", "-video_size", "1280x720", "-framerate", "60", "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "fast", "-crf", "15", "-pix_fmt", "yuv420p", "-movflags", "+faststart", path }) start.ArgumentList.Add(arg);
    return Process.Start(start)!;
}
record Shot(string Name, int Map, MapPosition At, double Seconds, string Route);
