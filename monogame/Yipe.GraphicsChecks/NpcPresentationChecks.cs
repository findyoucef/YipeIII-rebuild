using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class NpcPresentationChecks
{
    internal static void Run(Game host, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = host.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(host);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(host, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(host, args);
        var saved = new[] { "session", "adventure", "renderedMap", "journal", "cameraPosition", "sceneTime", "paused", "pauseAmount",
            "overlay", "mapOpen", "worldMapOpen", "saving", "loading", "studioSplashRemaining", "walkingStep", "pendingDirection",
            "miniatureMode", "scanlines", "vignette", "pixelLighting", "messageRemaining", "timeOfDayMode", "pendingEvent", "command", "pendingNpcCelebration", "tickRemaining", "iceDirection", "dayNightSeconds", "dayNightUpdatedAt" }.ToDictionary(n => n, Get);
        var player = (PlayerMotion)Get("player")!; var position = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!;
        var directory = Path.Combine(output, "npcs"); Directory.CreateDirectory(directory);
        var world = Get("world")!; var wt = world.GetType();
        object? Invoke(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public | BindingFlags.Static)!.Invoke(world, args);
        T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        void PropertySet(object value, string name, object data) => value.GetType().GetProperty(name)!.SetValue(value, data);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var presented = new Texture2D(host.GraphicsDevice, 1280, 720);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("walkingStep", false);
            Set("pendingDirection", Point.Zero); Set("messageRemaining", 0d); Set("journal", new MapJournal()); Set("timeOfDayMode", 1);
            Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", false); Set("pixelLighting", true);
            CheckAssets();
            foreach (var (name, id, at) in new[] { ("king",17,new MapPosition(11,2)), ("teacher",75,new MapPosition(4,2)),
                ("princess-pink",46,new MapPosition(7,2)), ("princess-purple",65,new MapPosition(10,6)), ("princess-blue",51,new MapPosition(5,3)) })
            {
                var game = Scene(id, at);
                var before = GameSaveCodec.Encode(game.Session, catalog);
                Capture(name + "-world.png");
                check(before.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)), name + ": world rendering preserves gameplay and RNG");
                for (var frame = 0; frame < 40; frame++)
                { Set("sceneTime", frame / 8d); Capture($"{name}-world-{frame:D2}.png"); }
                var direction = id is 51 or 65 ? new Point(-1,0) : new Point(0,-1);
                var expected = NewGame(id, at);
                using var expectedMove = expected.Move(direction.X,direction.Y).GetEnumerator();
                while (expectedMove.MoveNext() && expectedMove.Current.Kind == AdventureEventKind.Sound) { }
                Call("StartCommand", game.Move(direction.X,direction.Y));
                FinishMovement(); Capture(name + "-dialogue.png");
                check(Get("overlay") is not null, name + ": original event opens its dialogue");
                var current = Get("overlay")!;
                var actor = Property<object>(current, "Npc").ToString()!;
                check(actor == (id == 17 ? "King" : id == 75 ? "Teacher" : id == 46 ? "PrincessPink" : id == 65 ? "PrincessPurple" : "PrincessBlue"),
                    name + ": dialogue uses the correct actor/variant");
                check(GameSaveCodec.Encode(expected.Session, catalog).SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)),
                    name + ": opening the animated dialogue matches the original event state and RNG");
                CheckDialogueLayout(current, name);
                var frozen = GameSaveCodec.Encode(game.Session, catalog); var sceneTime = (double)Get("sceneTime")!;
                var screenA = Capture(name + "-dialogue.png");
                Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.7)));
                var screenB = Capture(name + "-gesture.png");
                check(Property<double>(current,"NpcTime") > 0 && !screenA.SequenceEqual(screenB), name + ": dialogue animates through the real update loop");
                check(frozen.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)) && (double)Get("sceneTime")! == sceneTime,
                    name + ": portrait time does not advance the world or RNG");
                for (var frame = 0; frame < 24; frame++)
                { PropertySet(current,"NpcTime",frame / 8d); Capture($"{name}-talk-{frame:D2}.png"); }
                if (id == 17)
                {
                    check(string.Join(" ", Property<string[]>(current,"Lines")).Split(' ',StringSplitOptions.RemoveEmptyEntries)
                        .SequenceEqual(expectedMove.Current.Text.Replace("\n"," ").Split(' ',StringSplitOptions.RemoveEmptyEntries)), "King dialogue retains every original word across pages");
                    CheckPause(current);
                    var pages = Property<int>(current,"PageCount");
                    for (var page = 0; page < pages - 1; page++) Call("UpdateOverlay", new InputFrame(Point.Zero,true,false,false));
                    check(ReferenceEquals(Get("overlay"),current) && Property<int>(current,"Page") == pages - 1,
                        "King prose paginates with Continue without closing early");
                    Capture("king-dialogue-last-page.png");
                }
                else if (id == 75)
                {
                    var gold = game.Player.Gold; var offer = Shops.AbilityForEnvironment(game.Map.Environment)!.Value;
                    Choose(0); Shops.TrainAbility(expected.Player, expected.Map.Environment);
                    check(Property<bool>(Get("overlay")!,"NpcCelebrating") && game.Player.Gold == gold - offer.Price
                        && GameSaveCodec.Encode(expected.Session,catalog).SequenceEqual(GameSaveCodec.Encode(game.Session,catalog)),
                        "Learning an ability celebrates once and preserves its original cost and outcome");
                    RecordReaction("teacher-trained");
                    Choose(0); Choose(0);
                    check(!Property<bool>(Get("overlay")!,"NpcCelebrating") && game.Player.Gold == gold - offer.Price,
                        "Already-trained feedback does not celebrate or charge again");
                }
                else
                {
                    Choose(0); expectedMove.Current.Choice = 1;
                    while (expectedMove.MoveNext() && expectedMove.Current.Kind == AdventureEventKind.Sound) { }
                    check(Property<bool>(Get("overlay")!,"NpcCelebrating")
                        && GameSaveCodec.Encode(expected.Session,catalog).SequenceEqual(GameSaveCodec.Encode(game.Session,catalog)),
                        name + ": rescue celebration preserves the original gift, flags and RNG");
                    RecordReaction(name + "-rescued");
                }
                if (Get("command") is IDisposable command) command.Dispose();
                Set("command", null); Set("pendingEvent", null); Set("overlay", null);
            }
            CheckWorldVisibility(); CheckOutcomes();
        }
        finally
        {
            if (Get("command") is IDisposable command) command.Dispose();
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(position); player.Face(facing switch { 1 => new(-1,0), 2 => new(1,0), 3 => new(0,-1), _ => new(0,1) });
            Call("SynchronizeWorld", false); Call("Draw", new GameTime());
        }
        void Choose(int index)
        {
            var current = Get("overlay")!;
            while (Property<int>(current,"Page") < Property<int>(current,"PageCount") - 1)
                Call("UpdateOverlay",new InputFrame(new(1,0),false,false,false));
            PropertySet(current,"Selected",index);
            Call("UpdateOverlay",new InputFrame(Point.Zero,true,false,false));
        }
        void RecordReaction(string name)
        {
            var current = Get("overlay")!; var game = (Adventure)Get("adventure")!;
            var snapshot = GameSaveCodec.Encode(game.Session,catalog);
            PropertySet(current,"NpcTime",.7d); Capture(name + ".png");
            for (var frame = 0; frame < 24; frame++)
            { PropertySet(current,"NpcTime",frame / 8d); Capture($"{name}-{frame:D2}.png"); }
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session,catalog)), name + ": reaction frames preserve full gameplay and RNG");
        }
        void CheckDialogueLayout(object current, string name)
        {
            var lines = Property<string[]>(current,"Lines"); var perPage = Property<int>(current,"LinesPerPage");
            var options = Property<Array>(current,"Options");
            check(lines.All(l => l.Length <= 44) && 64 + (perPage - 1) * 10 + 7 < 150
                && (options.Length <= 1 || 74 + Math.Min(lines.Length,perPage) * 10 + (options.Length - 1) * 11 + 7 < 164),
                name + ": prose and every choice fit below the portrait without overlap");
        }
        void CheckPause(object current)
        {
            var before = Property<double>(current,"NpcTime");
            Set("paused",true); Call("Update",new GameTime(TimeSpan.FromSeconds(2),TimeSpan.FromSeconds(.5)));
            check(Property<double>(current,"NpcTime") == before, "Pause freezes NPC portrait animation"); Set("paused",false);
            var platform = typeof(Game).GetField("Platform",flags)!.GetValue(host)!;
            var active = platform.GetType().GetProperty("IsActive")!; var wasActive = host.IsActive;
            try
            {
                active.SetValue(platform,false); Call("Update",new GameTime(TimeSpan.FromSeconds(3),TimeSpan.FromSeconds(.5)));
                check(Property<double>(current,"NpcTime") == before, "Focus loss freezes NPC portrait animation");
            }
            finally { active.SetValue(platform,wasActive); Set("paused",false); Set("pauseAmount",0f); }
        }
        void CheckAssets()
        {
            var sheets = (System.Collections.IDictionary)wt.GetField("npcAnimations",flags)!.GetValue(world)!;
            var palette = (Color[])type.Assembly.GetType("Yipe.Desktop.RetroArt")!.GetField("Palette",BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            check(sheets.Count == 5,"Five NPC sheets preserve the King, Teacher and all three Princess identities");
            foreach (System.Collections.DictionaryEntry entry in sheets)
            {
                var sheet = (Texture2D)entry.Value!; var data = new Color[sheet.Width * sheet.Height]; sheet.GetData(data);
                check(sheet.Width == 192 && sheet.Height == 144 && data.All(c => c.A == 0 || c.A == 255 && palette.Contains(c)),
                    entry.Key + ": native 48-pixel poses use binary alpha and the shared palette");
                var poses = new List<Color[]>(); var feetStable = true; var clearBorders = true;
                for (var frame = 0; frame < 12; frame++)
                {
                    var pose = new Color[48*48]; sheet.GetData(0,new(frame%4*48,frame/4*48,48,48),pose,0,pose.Length); poses.Add(pose);
                    var bottom = Enumerable.Range(0,48).Where(y => pose.Skip(y*48).Take(48).Any(c => c.A != 0)).Max();
                    feetStable &= bottom == 43;
                    clearBorders &= Enumerable.Range(0,48).All(i => pose[i].A == 0 && pose[47*48+i].A == 0 && pose[i*48].A == 0 && pose[i*48+47].A == 0);
                }
                check(feetStable && clearBorders, entry.Key + ": every pose has planted feet and clean cell borders");
                check(!poses[0].SequenceEqual(poses[2]) && !poses[0].SequenceEqual(poses[5]) && !poses[5].SequenceEqual(poses[10]),
                    entry.Key + ": blink, conversation and celebration have distinct artwork");
                using var file = File.Create(Path.Combine(directory,entry.Key + "-native.png")); sheet.SaveAsPng(file,sheet.Width,sheet.Height);
                check((int)Invoke("NpcFrame",entry.Key,2d,true,true)! < 4 && (int)Invoke("NpcFrame",entry.Key,8d,true,true)! < 4,
                    entry.Key + ": one-shot celebration settles to idle instead of repeating");
            }
        }
        Color[] World(Adventure game)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState:SamplerState.PointClamp);
            Invoke("Draw",batch,game,player,Get("town")!,1d,false,0f);
            batch.End(); host.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[416*384]; target.GetData(pixels); return pixels;
        }
        void CheckWorldVisibility()
        {
            foreach (var id in catalog.Ids.Where(id => catalog.CreateMap(id)!.Events.Princess.HasPosition))
            {
                var map = catalog.CreateMap(id)!; var p = map.Events.Princess;
                var game = Scene(id,p.Position); player.SnapTo(new(6,5));
                var visible = World(game); var snapshot = GameSaveCodec.Encode(game.Session,catalog);
                check(visible.SequenceEqual(World(game)), $"Princess {id}: frozen rendering is deterministic");
                game.Player.InventoryValues[p.RescueIndex] = 1; var rescued = World(game);
                check(!visible.SequenceEqual(rescued), $"Princess {id}: the world actor disappears after rescue");
                game.Visibility.Initialize(new Map(id,map.Environment,Enumerable.Repeat((ushort)0x22,Map.TileCount).ToArray()),new(12,11));
                var hidden = World(game); game.Player.InventoryValues[p.RescueIndex] = 0;
                check(hidden.SequenceEqual(World(game)), $"Princess {id}: unexplored cells never reveal the actor");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session,catalog)), $"Princess {id}: drawing uses no gameplay state or RNG");
            }
            foreach (var id in new[] {75,94,104,105})
            {
                var map = catalog.CreateMap(id)!;
                var at = (from x in Enumerable.Range(0,13) from y in Enumerable.Range(0,12) where map.Tile(new(x,y)) == 0x41 select new MapPosition(x,y)).Single();
                var game = Scene(id,at); var visible = World(game); game.Map.RestoreTile(at,0x28);
                check(!visible.SequenceEqual(World(game)), $"Teacher {id}: artwork occupies the original training tile");
            }
        }
        void CheckOutcomes()
        {
            // Every original rescue choice can be accepted immediately, without waiting for the gesture.
            foreach (var gift in new[] {1,2,3,4})
            {
                var game = Scene(46,new(7,2)); var expected = NewGame(46,new(7,2));
                using var move = expected.Move(0,-1).GetEnumerator();
                while(move.MoveNext() && move.Current.Kind == AdventureEventKind.Sound) { }
                Call("StartCommand",game.Move(0,-1)); FinishMovement();
                var labels = Property<Array>(Get("overlay")!,"Options").Cast<object>().Select(o=>Property<string>(o,"Label"));
                check(labels.SequenceEqual(new[]{"Gold","Experience","Kiss","Mystery"}), $"Rescue gift {gift}: original choices and order remain available");
                Choose(gift-1); move.Current.Choice = gift;
                while(move.MoveNext() && move.Current.Kind == AdventureEventKind.Sound) { }
                check(GameSaveCodec.Encode(game.Session,catalog).SequenceEqual(GameSaveCodec.Encode(expected.Session,catalog)),
                    $"Rescue gift {gift}: immediate choice exactly matches core rewards and RNG");
            }
            var cancelled = Scene(46,new(7,2)); Call("StartCommand",cancelled.Move(0,-1)); FinishMovement();
            Call("UpdateOverlay",new InputFrame(Point.Zero,false,true,false));
            check(cancelled.Player.InventoryValues[cancelled.Map.Events.Princess.RescueIndex] == 0 && Get("overlay") is null,
                "Backing out of a rescue leaves the Princess available and gives no reward");
            foreach (var id in new[]{75,94,104,105})
            {
                var map = catalog.CreateMap(id)!;
                var at = (from x in Enumerable.Range(0,13) from y in Enumerable.Range(0,12) where map.Tile(new(x,y)) == 0x41 select new MapPosition(x,y)).Single();
                var game = Scene(id,at); game.Player.Gold = 0;
                Call("ShowInteraction",new AdventureEvent(AdventureEventKind.AbilityTraining,"Ability Training"));
                CheckDialogueLayout(Get("overlay")!,"Teacher " + id);
                var before = GameSaveCodec.Encode(game.Session,catalog); Choose(0);
                check(!Property<bool>(Get("overlay")!,"NpcCelebrating") && before.SequenceEqual(GameSaveCodec.Encode(game.Session,catalog)),
                    $"Teacher {id}: insufficient funds give ordinary feedback without charging or teaching");
            }
            foreach (var reward in new[]{"gold","item","equipment"})
            {
                var king = Scene(17,new(11,2));
                var p = king.Player; p.QuestFlags[0] = p.QuestFlags[1] = true;
                if(reward == "gold") p.StampsFound = 1;
                else if(reward == "item") { p.PrincessesFound = 10; p.QuestFlags[5] = true; }
                else { p.StampsFound = 6; p.GatekeeperState = 2; p.QuestFlags[6] = p.QuestFlags[9] = true; }
                Call("StartCommand",king.Move(0,-1)); FinishMovement();
                check(Property<bool>(Get("overlay")!,"NpcCelebrating"), "King: " + reward + " reward plays an approving reaction");
                if(reward == "gold") RecordReaction("king-reward");
            }
        }
        Adventure NewGame(int id, MapPosition at)
        {
            var layer = id >= 117 ? 0 : (id - 1) / 40; var cell = id >= 117 ? id - 99 : id - layer * 40 - 1;
            var game = new Adventure(new GameSession(catalog, location: new() { MapId = id, MapLayer = layer, AlternateMapSet = id >= 117,
                RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = at }));
            game.Player.Gold = 10000;
            return game;
        }
        Adventure Scene(int id, MapPosition at)
        {
            if (Get("command") is IDisposable previous) previous.Dispose();
            var game = NewGame(id,at);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Visibility.RevealFrom(game.Map, new(x,y));
            Set("session", game.Session); Set("adventure", game); Set("sceneTime", 1d);
            // Maynard terrain is unchanged: retain the host renderer and its ownership.
            Set("renderedMap", game.Map);
            Set("overlay", null); Set("command", null); Set("pendingEvent", null);
            Call("SynchronizeWorld", false);
            Set("cameraPosition", type.GetMethod("CameraTarget", flags | BindingFlags.Static)!.Invoke(host, [player.VisualPosition]));
            return game;
        }
        void FinishMovement()
        {
            player.Update(1);
            Invoke("UpdateMonsters", 1d);
            if (Get("pendingEvent") is AdventureEvent next)
            { Set("pendingEvent", null); Call("ShowInteraction", next); }
        }
        Color[] Capture(string name)
        {
            Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; host.GraphicsDevice.GetBackBufferData(pixels); presented.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); presented.SaveAsPng(file, 1280, 720);
            return pixels;
        }
    }
}
