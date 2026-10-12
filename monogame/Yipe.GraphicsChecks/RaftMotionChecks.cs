using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class RaftMotionChecks
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
            "iceDirection", "nextStride", "command", "pendingEvent", "tickRemaining", "messageRemaining", "miniatureMode", "scanlines", "pixelLighting" }.ToDictionary(n => n, Get);
        var player = (PlayerMotion)Get("player")!; var original = player.Position; var facing = player.FacingRow;
        var catalog = (MapCatalog)Get("catalog")!; var world = Get("world")!; var wt = world.GetType();
        object? World(string name, params object[] args) => wt.GetMethod(name, flags | BindingFlags.Public)!.Invoke(world, args);
        var directory = Path.Combine(output, "raft-motion"); Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(host.GraphicsDevice, 416, 384);
        using var batch = new SpriteBatch(host.GraphicsDevice);
        using var capture = new Texture2D(host.GraphicsDevice, 1280, 720);
        Adventure Scene(int id = 62, MapPosition? at = null)
        {
            (Get("command") as IDisposable)?.Dispose(); Set("command", null); Set("pendingEvent", null); Set("overlay", null);
            Set("paused", false); Set("pauseAmount", 0f); Set("mapOpen", false); Set("worldMapOpen", false);
            Set("saving", null); Set("loading", null); Set("studioSplashRemaining", 0d); Set("tickRemaining", 100d);
            Set("pendingDirection", Point.Zero); Set("iceDirection", Point.Zero); Set("sceneTime", 0d); Set("messageRemaining", 0d);
            var map = catalog.CreateMap(id)!; var layer = (id - 1) / 40; var cell = (id - 1) % 40;
            var session = new GameSession(catalog, new Player { Health = 100, MaximumHealth = 100, RandomEncounterThreshold = 32767 },
                new() { MapId = id, MapLayer = layer, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6,
                    Position = at ?? map.Events.Overlay.Position });
            // Keep authored terrain/routes. Isolate the movement demonstration from combat.
            session.CurrentMap.Events.Pursuer = PursuingMonsterEvent.None;
            var game = new Adventure(session);
            var defeated = (List<ushort>)typeof(Adventure).GetField("temporaryDefeats", flags)!.GetValue(game)!;
            foreach (var placement in game.ActivePlacements) defeated.Add(placement.PlacementIndex);
            for (var x = 0; x < 13; x++) for (var y = 0; y < 12; y++) game.Visibility.RevealFrom(game.Map, new(x, y));
            Set("session", session); Set("adventure", game); Set("journal", new MapJournal()); Call("SynchronizeWorld", false);
            return game;
        }
        void Frame(double elapsed) => Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(elapsed)));
        Vector2 Deck(Adventure game) => (Vector2)World("RaftPosition", game.Map, (double)Get("sceneTime")!)!;
        void Move(Point direction) => Call("MovePlayer", direction, direction);
        void TickMove(Adventure game)
        {
            var old = game.Map.Events.Overlay.Position;
            for (var i = 0; i < 60 && old == game.Map.Events.Overlay.Position; i++) Call("StartCommand", game.Tick());
            if (old == game.Map.Events.Overlay.Position) throw new InvalidOperationException("Authored raft did not advance.");
        }
        Color[] Raft(Adventure game)
        {
            host.GraphicsDevice.SetRenderTarget(target); host.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp); World("DrawRaft", batch, game, (double)Get("sceneTime")!); batch.End();
            host.GraphicsDevice.SetRenderTarget(null); var pixels = new Color[416 * 384]; target.GetData(pixels); return pixels;
        }
        void Capture(string name)
        {
            Call("Draw", new GameTime()); var pixels = new Color[1280 * 720]; host.GraphicsDevice.GetBackBufferData(pixels); capture.SetData(pixels);
            using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
        try
        {
            foreach (var id in new[] { 44, 45, 50, 56, 62, 68, 74, 96, 102, 108, 114 })
            {
                if (catalog.CreateMap(id)!.Events.Overlay.Value <= 0) continue;
                var game = Scene(id); var old = Deck(game); var direction = player.FacingRow;
                TickMove(game);
                check(player.Position == game.Map.Events.Overlay.Position && player.VisualPosition == old && !player.IsMoving,
                    $"Raft {id}: an accepted carry starts from the previous visible position without a walking step");
                var snapshot = GameSaveCodec.Encode(game.Session, catalog); Frame(.45);
                var delta = Deck(game) - old;
                check(player.VisualPosition == Deck(game) && delta.Length() is > .2f and < .85f && player.FacingRow == direction,
                    $"Raft {id}: rider and logs glide together between authored tiles without changing facing");
                var image = Raft(game); var feet = new Point((int)MathF.Round(player.VisualPosition.X * 32) + 16,
                    (int)MathF.Round(player.VisualPosition.Y * 32) + 25);
                check(Enumerable.Range(-4, 9).All(dx => image[feet.Y * 416 + feet.X + dx].A == 255),
                    $"Raft {id}: opaque log deck supports the complete boot/contact-shadow width");
                Frame(.46);
                check(player.VisualPosition == Deck(game) && snapshot.SequenceEqual(GameSaveCodec.Encode(game.Session, catalog)),
                    $"Raft {id}: interpolation finishes without changing gameplay or RNG");
            }
            var boarding = Scene(at: new(4, 8));
            while (boarding.Map.Events.Overlay.Position != new MapPosition(4, 7)) { TickMove(boarding); Frame(.91); }
            Move(new(0, -1)); Frame(.125);
            check(player.IsMoving && player.VisualPosition.Y > Deck(boarding).Y, "Boarding walks from the bank toward the actual visible deck");
            TickMove(boarding); Frame(.125);
            check(!player.IsMoving && player.VisualPosition == Deck(boarding), "A raft move during boarding keeps the landing attached to the logs");
            check(World("FootstepAt", boarding.Map, player.Position)!.ToString() == "Wood", "Boarding produces a wood contact rather than a water splash");
            var audio = Get("audio")!; var audioType = audio.GetType();
            var contacts = audioType.GetField("footstepIndex", flags)!.GetValue(audio);
            Frame(.3);
            check(Equals(contacts, audioType.GetField("footstepIndex", flags)!.GetValue(audio)) && !(bool)Get("walkingStep")!,
                "Passive rafting creates no footsteps or walking animation");
            foreach (var gate in new[] { "paused", "mapOpen" })
            {
                var position = player.VisualPosition; var deck = Deck(boarding); Set(gate, true); Frame(.2);
                check(player.VisualPosition == position && Deck(boarding) == deck, $"{gate} freezes raft, rider and buoyancy together");
                Set(gate, false);
            }
            Call("ShowActions"); var still = Deck(boarding); Frame(.2);
            check(Deck(boarding) == still && player.VisualPosition == still, "Action panels freeze an in-progress raft glide");
            Set("overlay", null); Set("tickRemaining", 100d); Frame(.5);
            check(player.VisualPosition == Deck(boarding), "Resuming keeps the rider attached without replaying paused travel");

            // Reload through the actual codec and synchronization, with a fresh map identity.
            var reopened = GameSaveCodec.Decode(GameSaveCodec.Encode(boarding.Session, catalog), catalog);
            var restored = new Adventure(reopened); Set("session", reopened); Set("adventure", restored); Call("SynchronizeWorld", false);
            check(reopened.CurrentMap.Events.Overlay.Position == player.Position && player.VisualPosition == Deck(restored) && !player.IsMoving,
                "Loading aboard resets old interpolation and puts the boots directly on the restored raft");

            var departing = Scene(at: new(4, 8));
            while (departing.Map.Events.Overlay.Position != new MapPosition(4, 7)) { TickMove(departing); Frame(.91); }
            Move(new(0, -1)); Frame(.25); var from = player.VisualPosition;
            Move(new(0, 1)); Frame(.125);
            check(player.IsMoving && player.Position == new MapPosition(4, 8) && player.VisualPosition.Y > from.Y,
                "Disembarking starts at the deck and uses the original adjacent-bank movement rule");
            Frame(.125); TickMove(departing); Frame(.3);
            check(player.VisualPosition == new Vector2(4, 8) && World("RaftSupport", departing, (double)Get("sceneTime")!) is null,
                "Once on shore, the character no longer follows the moving raft");
            departing.Visibility.Initialize(new Map(62, 5, Enumerable.Repeat((ushort)0x25, Map.TileCount).ToArray()), new(0, 0));
            check(Raft(departing).All(c => c.A == 0), "An undiscovered raft and its ripples stay hidden");
            var elsewhere = Scene(53, new(5, 5));
            check(World("RaftSupport", elsewhere, 0d) is null && Raft(elsewhere).All(c => c.A == 0), "Raft presentation does not leak into Ice Cave");

            var demo = Scene(); Set("miniatureMode", 2); Set("scanlines", true); Set("pixelLighting", true);
            Capture("aboard.png"); TickMove(demo);
            for (var frame = 0; frame < 180; frame++)
            {
                if (frame == 92) TickMove(demo);
                Frame(1d / 60);
                Capture($"raft-{frame:D3}.png");
                if (frame == 27) Capture("mid-glide.png");
            }
            check(player.VisualPosition == Deck(demo), "Three-second authored Water Cave clip keeps rider and deck aligned throughout travel");
        }
        finally
        {
            (Get("command") as IDisposable)?.Dispose();
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(original); player.Face(facing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
            World("SynchronizeRaft", (Adventure)Get("adventure")!, original, false);
        }
    }
}
