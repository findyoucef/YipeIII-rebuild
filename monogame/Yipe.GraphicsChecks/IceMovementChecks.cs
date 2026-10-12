using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class IceMovementChecks
{
    public static void Run(Game host, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = host.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(host);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(host, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(host, args);
        var saved = new[] { "session", "adventure", "renderedMap", "journal", "cameraPosition", "sceneTime", "paused", "pauseAmount",
            "overlay", "mapOpen", "worldMapOpen", "saving", "loading", "studioSplashRemaining", "walkingStep", "pendingDirection",
            "iceDirection", "nextStride", "command", "pendingEvent", "tickRemaining", "messageRemaining" }.ToDictionary(n => n, Get);
        var player = (PlayerMotion)Get("player")!; var original = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!;
        var east = new Point(1, 0); var south = new Point(0, 1); var zero = Point.Zero;
        Point Momentum() => (Point)Get("iceDirection")!;
        var directory = Path.Combine(output, "ice-movement"); Directory.CreateDirectory(directory);
        using var capture = new Texture2D(host.GraphicsDevice, 1280, 720);
        void Capture(string name)
        {
            Set("pauseAmount", 0f); Set("messageRemaining", 0d); Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; host.GraphicsDevice.GetBackBufferData(pixels); capture.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
        void Move(Point pressed, Point held) => Call("MovePlayer", pressed, held);
        void Finish() { player.Update(1); var world = Get("world")!; world.GetType().GetMethod("UpdateMonsters")!.Invoke(world, [1d]); }
        void Update() => Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.016)));
        Adventure Scene(int id = 53, MapPosition? position = null, bool clear = true)
        {
            (Get("command") as IDisposable)?.Dispose(); Set("command", null); Set("pendingEvent", null); Set("overlay", null);
            Set("paused", false); Set("mapOpen", false); Set("worldMapOpen", false); Set("saving", null); Set("loading", null);
            Set("studioSplashRemaining", 0d); Set("tickRemaining", 100d); Set("pendingDirection", zero); Set("iceDirection", zero);
            var layer = (id - 1) / 40; var cell = id - layer * 40 - 1;
            var session = new GameSession(catalog, location: new() { MapId = id, MapLayer = layer,
                RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = position ?? new(5, 5) });
            var map = session.CurrentMap;
            if (clear)
            {
                for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++) map.RestoreTile(new(x, y), 0x53);
                map.Events.Message = map.Events.Overlay = MapEvent.None; map.Events.Princess = PrincessEvent.None;
                map.Events.Pursuer = PursuingMonsterEvent.None;
                Array.Fill(map.Events.Depth, MapEvent.None); Array.Fill(map.Events.Chests, new MapPosition(-1, -1));
            }
            var adventure = new Adventure(session);
            var defeated = (List<ushort>)typeof(Adventure).GetField("temporaryDefeats", flags)!.GetValue(adventure)!;
            foreach (var placement in adventure.ActivePlacements) defeated.Add(placement.PlacementIndex);
            Set("session", session); Set("adventure", adventure); Set("journal", new MapJournal()); Call("SynchronizeWorld", false);
            return adventure;
        }
        try
        {
            // Real locked-door approaches: a released step must align with the opening.
            foreach (var (id, approachStart, along, enter) in new (int, MapPosition, Point, Point)[]
                { (41, new(3, 7), east, south), (47, new(1, 4), east, south), (88, new(11, 5), south, new(-1, 0)) })
            {
                var room = Scene(id, approachStart, clear: false);
                room.Player.HasRainbowKey = room.Player.HasUnderworldKey = true;
                var approach = new MapPosition(approachStart.X + along.X, approachStart.Y + along.Y);
                var doorway = new MapPosition(approach.X + enter.X, approach.Y + enter.Y);
                Move(along, along); Finish(); Move(zero, zero); Finish();
                Capture($"door-{id}-release.png");
                check(player.Position == approach && Momentum() == zero,
                    $"Ice {id}: releasing at doorway {doorway.X},{doorway.Y} keeps the player aligned instead of sliding past");
                Move(enter, enter); Finish(); Move(zero, zero); Finish();
                check(player.Position == doorway && room.Map.Tile(doorway) == 0x53 && Momentum() == zero,
                    $"Ice {id}: an intentional turn opens the keyed door and stops on its threshold");
                Capture($"door-{id}-entered.png");
                Move(new(-enter.X, -enter.Y), new(-enter.X, -enter.Y)); Finish(); Move(zero, zero); Finish();
                Move(enter, enter); Finish(); Move(zero, zero); Finish();
                check(player.Position == doorway && Momentum() == zero,
                    $"Ice {id}: the opened doorway retains traction after its tile becomes ordinary ice");
            }
            var turn = Scene(41, new(3, 7), clear: false); turn.Player.HasRainbowKey = true;
            Move(east, east); Finish(); Move(south, south); Finish();
            check(player.Position == new MapPosition(4, 8), "Turning into an authored Ice doorway immediately honors the new held direction");
            var locked = Scene(41, new(4, 7), clear: false); Move(south, south); Finish();
            check(player.Position == new MapPosition(4, 7) && locked.Map.Tile(new(4, 8)) == 0x3f && Get("overlay") is not null,
                "Precise Ice steering still requires the original key to open a locked door");
            Scene(53, new(1, 4), clear: false); Move(east, east); Finish(); Move(zero, zero); Finish();
            check(player.Position == new MapPosition(2, 4), "Upper A3: release aligns with the one-tile passage at 2,5");
            Move(south, south); Finish(); Move(zero, zero); Finish();
            check(player.Position == new MapPosition(2, 5) && Momentum() == zero,
                "Upper A3: the player can enter and stop inside the narrow passage");
            Capture("passage-53.png");
            Scene(47, new(1, 1), clear: false); Move(east, east); Finish(); Move(zero, zero); Finish();
            check(player.Position == new MapPosition(2, 1), "Upper A2: release aligns with the northern room opening");
            Move(new(0, -1), new(0, -1)); Finish();
            check(((GameSession)Get("session")!).CurrentMap.Id == 41 && player.Position == new MapPosition(2, 10) && Momentum() == zero,
                "The intentional step through the Ice room opening uses the original transition and clears momentum");
            Scene(42, new(9, 1), clear: false); Move(east, east); Finish(); Move(zero, zero); Finish();
            check(((GameSession)Get("session")!).CurrentMap.Id == 42 && player.Position == new MapPosition(10, 1),
                "Upper B1: release stops beside the depth stairs without entering them automatically");
            Move(east, east); Finish();
            check(((GameSession)Get("session")!).CurrentMap.Id == 82 && player.Position == new MapPosition(11, 1) && Momentum() == zero,
                "An intentional step takes the authored Ice depth stairs");
            foreach (var rock in new ushort[] { 0x22, 0x43 })
            {
                var room = Scene(); room.Map.RestoreTile(new(6, 6), rock);
                Move(east, east); Finish(); Move(zero, zero); Finish();
                check(player.Position == new MapPosition(6, 5) && Momentum() == zero,
                    $"Ice wall {rock:x}: ordinary and secret wall artwork give identical nearby traction");
            }
            foreach (var id in new[] { 41, 42, 47, 53, 81, 82, 87, 88, 93, 94, 99 })
            {
                Scene(id); Move(east, east); Finish(); Move(zero, zero); Finish(); Move(zero, zero);
                check(player.Position == new MapPosition(7, 5) && !player.IsMoving && Momentum() == zero,
                    $"Ice {id}: a tap makes one accepted push and exactly one coasting tile");
            }
            Scene(); Move(east, east); Finish(); Move(zero, east);
            check(player.IsSliding && Momentum() == east && player.Position == new MapPosition(7, 5),
                "Holding direction glides continuously between keyboard/controller repeat pulses");
            Finish(); Move(south, south); Finish(); Move(south, south);
            check(player.Position == new MapPosition(8, 6) && Momentum() == south,
                "Turning sheds forward momentum for one tile before gaining traction in the new direction");
            Scene(); Move(east, east); Finish(); Move(new(-1, 0), new(-1, 0)); Finish(); Move(new(-1, 0), new(-1, 0));
            check(player.Position == new MapPosition(6, 5) && Momentum() == new Point(-1, 0),
                "Reversing brakes once and then pushes back instead of instantly reversing velocity");
            foreach (var obstacle in new ushort[] { 0x22, 0x3e, 0x3f, 0x40, 0x27, 0x43 })
            {
                var a = Scene(); a.Map.RestoreTile(new(7, 5), obstacle); Move(east, east); Finish(); var before = a.Session.Random.State;
                Move(east, zero);
                check(player.Position == new MapPosition(6, 5) && Momentum() == zero && Get("command") is null
                    && a.Session.Random.State == before && a.Map.Tile(new(7, 5)) == obstacle,
                    $"Automatic ice drift stops before tile {obstacle:x2}, without buffered input opening doors or revealing secrets");
            }
            var depth = Scene(); depth.Map.Events.Depth[0] = new(new(7, 5), 0x3a);
            Move(east, east); Finish(); Move(zero, zero);
            check(depth.Map.Id == 53 && player.Position == new MapPosition(6, 5), "Ice drift never automatically takes a depth exit");
            Scene(position: new(10, 5)); Move(east, east); Finish(); Move(zero, zero);
            check(((GameSession)Get("session")!).CurrentMap.Id == 53 && player.Position == new MapPosition(11, 5),
                "Ice drift stops before a room boundary");
            var wall = Scene(); wall.Map.RestoreTile(new(6, 5), 0x22); Move(east, east);
            check(player.Position == new MapPosition(5, 5) && !player.IsMoving && Momentum() == zero,
                "A rejected ice push builds no momentum and respects normal collision");
            var alias = Scene(); alias.Map.RestoreTile(new(6, 5), 0x49); Move(east, east); Finish(); Move(zero, zero);
            check(player.Position == new MapPosition(7, 5), "The matching ice-floor alias retains the same traction");
            foreach (var id in new[] { 13, 54, 57, 59, 67 })
            {
                Scene(id); Move(east, east); player.Update(.125);
                check(player.VisualPosition == new Vector2(5.5f, 5) && Momentum() == zero,
                    $"Map {id}: ordinary movement stays unchanged even with an ice-colored fixture tile");
            }
            foreach (var gate in new[] { "paused", "mapOpen" })
            {
                Scene(); Move(east, east); Set(gate, true); Update();
                check(Momentum() == zero, $"{gate} cancels ice momentum immediately");
            }
            Scene(); Move(east, east); Finish(); Call("ShowActions"); Update();
            check(Momentum() == zero, "Action panels cancel ice momentum");
            Scene(); Move(east, east); Call("RestorePosition", new MapPosition(4, 4));
            check(Momentum() == zero && !player.IsMoving, "Restoring a position cancels both ice momentum and interpolation");
            var encounter = Scene(); Move(east, east); Finish();
            typeof(Adventure).GetField("roaming", flags)!.SetValue(encounter, new RoamingMonster(21, new(7, 5)));
            Move(zero, zero);
            check(encounter.Battle is not null && Get("pendingEvent") is AdventureEvent { Kind: AdventureEventKind.Combat }
                && Momentum() == zero, "A coast into a monster uses the real encounter pipeline and clears momentum");

            // Actual authored entrance room, isolated from personal adventures. Show push,
            // steady glide and release at 60 Hz, with the existing reflected pixel sprite.
            var demonstration = Scene(41, new(3, 7), clear: false); demonstration.Player.HasRainbowKey = true;
            for (var frame = 0; frame < 120; frame++)
            {
                if (frame == 0) Move(east, east);
                if (frame == 30) Move(zero, zero);
                if (frame == 60) Move(south, south);
                if (frame == 90) Move(zero, zero);
                player.Update(1d / 60); Set("sceneTime", frame / 60d); Capture($"door-{frame:D3}.png");
            }
            check(player.Position == new MapPosition(4, 8) && !player.IsMoving && Momentum() == zero,
                "The authored doorway demonstration stops in alignment and then enters without a skid");

            // Open floor away from the new traction margin still demonstrates the full glide.
            var start = new MapPosition(6, 7);
            Scene(position: start, clear: false); Set("pauseAmount", 0f); Set("messageRemaining", 0d);
            Move(east, east);
            for (var frame = 0; frame < 120; frame++)
            {
                if (!player.IsMoving && frame < 22) Move(east, east);
                else if (!player.IsMoving) Move(zero, zero);
                player.Update(1d / 60); Set("sceneTime", frame / 60d);
                Capture($"slide-{frame:D3}.png");
            }
            check(!player.IsMoving && Momentum() == zero && player.Position == new MapPosition(start.X + 3, start.Y),
                "Authored Ice Cave demonstration ends after push, glide and one release skid");
        }
        finally
        {
            (Get("command") as IDisposable)?.Dispose();
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(original); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
        }
    }
}
