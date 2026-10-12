using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class SunActorChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "sunShafts", "sunShaftStrength", "session", "adventure", "cameraPosition", "paused", "pauseAmount",
            "overlay", "mapOpen", "worldMapOpen", "journal", "sceneTime", "miniatureMode", "scanlines", "vignette", "messageRemaining" }
            .ToDictionary(name => name, Get);
        var town = Get("town")!; var townType = town.GetType();
        var sunlight = townType.GetProperty("Sunlight")!;
        var alternate = townType.GetProperty("AlternateHero")!; var oldAlternate = alternate.GetValue(town);
        var oldSunlight = sunlight.GetValue(town);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var sunType = Get("sunShafts")!.GetType();
        using var effect = (IDisposable)Activator.CreateInstance(sunType, [game.GraphicsDevice, 320, 180])!;
        object Sun(string name) => sunType.GetField(name, flags)!.GetValue(effect)!;
        void Advance(Map map, PlayerMotion actor, double time, int strength = 2) => sunType.GetMethod("Update")!.Invoke(effect, [map, actor, time, strength, 0]);
        float Light(Vector2 position, double time) => (float)sunType.GetMethod("LightAt", flags)!.Invoke(effect, [position, time, 2])!;
        int DustCount() => ((ICollection)Sun("dust")).Count;
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 64, 64);
        using var pixel = new Texture2D(game.GraphicsDevice, 1, 1); pixel.SetData([Color.White]);
        var directory = Path.Combine(output, "sun-actor"); Directory.CreateDirectory(directory);
        var map = new Map(17, 1, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        var litCell = (from y in Enumerable.Range(2, 7) from x in Enumerable.Range(1, 10)
            where Light(new Vector2(x * 32 + 13, y * 32 + 7), 100) > .35f
                && Light(new Vector2((x + 1) * 32 + 13, y * 32 + 7), 100) == 0
            select new MapPosition(x, y)).First();
        var actor = new PlayerMotion(litCell);
        try
        {
            Set("sunShafts", effect);
            Advance(map, actor, 100); Advance(map, actor, 100.2);
            check((float)Sun("heroLight") > .2f, "The actor samples the actual sun mask at head/shoulder height");
            var world = Get("world")!;
            for (var appearance = 0; appearance <= 12; appearance++)
            {
                alternate.SetValue(town, appearance == 0 ? null : world.GetType().GetMethod("Character")!.Invoke(world, [appearance]));
                var valid = true; var changed = 0;
                foreach (var direction in new[] { new Point(0, 1), new(-1, 0), new(1, 0), new(0, -1) })
                {
                    actor.SnapTo(litCell); actor.Face(direction);
                    var plain = Hero(false, 100.2); var lit = Hero(true, 100.2);
                    changed += plain.Zip(lit).Count(p => p.First != p.Second);
                    valid &= plain.Zip(lit).All(p => p.First.A == p.Second.A &&
                        (p.First != new Color(20, 25, 36) && p.First != new Color(37, 43, 59) || p.First == p.Second));
                }
                actor.MoveTo(new(litCell.X, litCell.Y - 1)); actor.Update(.125);
                var walkingOff = Hero(false, 100.2); var walkingOn = Hero(true, 100.2);
                valid &= walkingOff.Zip(walkingOn).All(p => p.First.A == p.Second.A);
                check(valid && changed > 0, $"Appearance {appearance}: sunlight follows directional/animated poses without changing silhouette or dark outlines");
                Save(walkingOn, 64, 64, $"appearance-{appearance:D2}.png");
            }
            alternate.SetValue(town, null);
            actor.SnapTo(litCell); Advance(map, actor, 101); Advance(map, actor, 101.2);
            actor.MoveTo(new(litCell.X + 1, litCell.Y)); Advance(map, actor, 101.21);
            var glint = Hero(true, 101.21); var noGlint = Hero(true, 101.41);
            check(glint.Zip(noGlint).Count(p => p.First != p.Second) is 1 or 2,
                "Entering sunlight adds only a one- or two-pixel equipment glint");
            Save(glint, 64, 64, "hero-glint.png"); Save(noGlint, 64, 64, "hero-rim.png");
            Advance(map, actor, 101.22);
            check((double)Sun("glintTime") == 101.21, "The glint cooldown prevents repeated flashes on consecutive updates");
            var sawFade = false;
            for (var i = 1; i <= 15; i++)
            {
                actor.Update(1d / 60); var time = 101.22 + i / 60d; Advance(map, actor, time);
                sawFade |= Light(actor.VisualPosition * 32 + new Vector2(13, 7), time) == 0 && (float)Sun("heroLight") > 0;
            }
            Advance(map, actor, 101.67);
            check(sawFade && (float)Sun("heroLight") == 0, "The warm rim fades away within 0.2 seconds of leaving a shaft");

            // Walk the same lit ground under different materials; all fixtures are disposable.
            var dustStart = (from y in Enumerable.Range(2, 7) from x in Enumerable.Range(1, 10)
                where Light(new Vector2(x * 32 + 32, y * 32 + 29), 200.125) > .15f
                select new MapPosition(x, y)).First();
            foreach (var tile in new ushort[] { 2, 8, 0x2d, 0x0c, 11, 0x24, 0x71 })
            {
                map = new Map(17, 1, Enumerable.Repeat(tile, Map.TileCount).ToArray());
                actor.SnapTo(dustStart); Advance(map, actor, 200);
                actor.MoveTo(new(dustStart.X + 1, dustStart.Y));
                for (var i = 1; i <= 8; i++) { actor.Update(1d / 60); Advance(map, actor, 200 + i / 60d); }
                check(tile is 2 or 8 or 0x2d ? DustCount() == 2 : DustCount() == 0,
                    $"Terrain {tile:x2}: sunlit dust is limited to dry grass, roads and sand");
            }
            var shadedStart = (from y in Enumerable.Range(2, 7) from x in Enumerable.Range(1, 10)
                where Light(new Vector2(x * 32 + 29, y * 32 + 29), 200.125) == 0
                select new MapPosition(x, y)).First();
            map = new Map(17, 1, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
            actor.SnapTo(shadedStart); Advance(map, actor, 200); actor.MoveTo(new(shadedStart.X + 1, shadedStart.Y));
            actor.Update(.125); Advance(map, actor, 200.125);
            check(DustCount() == 0, "Dry ground outside a sun shaft produces no illuminated dust");
            map = new Map(17, 1, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
            actor.SnapTo(dustStart); Advance(map, actor, 200); actor.MoveTo(new(dustStart.X + 1, dustStart.Y));
            actor.Update(.125); Advance(map, actor, 200.125);
            var dust = DustFrame(200.125); var repeated = DustFrame(200.125);
            check(dust.Any(p => p.A > 0) && dust.SequenceEqual(repeated), "Sunlit footfall motes render and freeze at a fixed scene clock");
            Save(dust, 64, 64, "dust-birth.png");
            check(!dust.SequenceEqual(DustFrame(200.45)), "Dust rises in whole pixels and fades as scene time advances");
            Save(DustFrame(200.45), 64, 64, "dust-fading.png");
            actor.Update(.125); Advance(map, actor, 200.25); Advance(map, actor, 201);
            check(DustCount() == 0 && DustFrame(201).All(p => p.A == 0), "Stopping leaves no persistent motes or new footfalls");
            actor.MoveTo(dustStart); actor.Update(.125); Advance(map, actor, 201.125);
            actor.SnapTo(new(11, 10)); Advance(map, actor, 201.14);
            check(DustCount() == 0 && (float)Sun("heroLight") == 0, "Teleporting clears the transient highlight and dust trail");
            foreach (var id in new[] { 41, 117 })
            {
                map = new Map(id, 1, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
                actor.SnapTo(dustStart); Advance(map, actor, 202); actor.MoveTo(new(dustStart.X + 1, dustStart.Y));
                actor.Update(.125); Advance(map, actor, 202.125);
                check(DustCount() == 0 && (float)Sun("heroLight") == 0, $"Map {id}: caves and Hell have no sunlit actor effects");
            }
            map = new Map(17, 1, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
            actor.SnapTo(dustStart); Advance(map, actor, 200); actor.MoveTo(new(dustStart.X + 1, dustStart.Y));
            actor.Update(.125); Advance(map, actor, 200.125); Advance(map, actor, 200.125, 0);
            check(DustCount() == 0 && (float)Sun("heroLight") == 0, "Switching sun shafts Off immediately clears the actor effects");

            // Capture the shared renderer and production update with an isolated outdoor session.
            var catalog = (MapCatalog)Get("catalog")!;
            var session = new GameSession(catalog); var adventure = new Adventure(session);
            var snapshot = GameSaveCodec.Encode(session, catalog);
            Set("session", session); Set("adventure", adventure); Set("sunShaftStrength", 2);
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            var route = (from y in Enumerable.Range(2, 7) from x in Enumerable.Range(1, 10)
                where session.CurrentMap.Tile(new(x, y)) == 2 && session.CurrentMap.Tile(new(x + 1, y)) == 2
                    && Light(new Vector2(x * 32 + 32, y * 32 + 29), 200.125) > .15f
                select new MapPosition(x, y)).First();
            player.SnapTo(route); Advance(session.CurrentMap, player, 200);
            Set("cameraPosition", Vector2.Clamp(new Vector2(route.X, route.Y) * 32 - new Vector2(128, 64), Vector2.Zero, new Vector2(96, 204)));
            for (var frame = 0; frame < 96; frame++)
            {
                if (frame % 24 == 0) player.MoveTo(new(route.X + (frame / 24 % 2 == 0 ? 1 : 0), route.Y));
                player.Update(1d / 24); var time = 200 + (frame + 1) / 24d;
                Advance(session.CurrentMap, player, time); Set("sceneTime", time);
                Save(Frame(), 1280, 720, $"walking-{frame:D3}.png");
            }
            Set("paused", true); Set("pauseAmount", 1f);
            var clock = (double)Get("sceneTime")!;
            Call("Update", new GameTime(TimeSpan.FromSeconds(clock), TimeSpan.FromSeconds(.1)));
            check((double)Get("sceneTime")! == clock && (double)Sun("actorTime") == clock,
                "Production pause freezes the highlight, glint and dust clocks together");
            var journal = new MapJournal(); journal.Observe(adventure); Set("journal", journal);
            Set("paused", false); Set("pauseAmount", 0f); Set("mapOpen", true); Set("worldMapOpen", false);
            // Force a lit character so the map check can detect a leaked rim, not just a dark pose.
            player.SnapTo(litCell); Advance(session.CurrentMap, player, 100); Advance(session.CurrentMap, player, 100.2);
            Set("sceneTime", 100.2); var mapLit = Frame(); Set("sunShaftStrength", 0);
            check(mapLit.SequenceEqual(Frame()), "The area map omits the hero highlight and dust as well as the world shafts");
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(session, catalog)), "Actor sunlight and all diagnostic draws preserve full gameplay state and RNG");
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(oldPosition); alternate.SetValue(town, oldAlternate); sunlight.SetValue(town, oldSunlight);
        }

        Color[] Hero(bool lit, double time)
        {
            sunlight.SetValue(town, lit ? effect : null);
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp,
                transformMatrix: Matrix.CreateTranslation(16 - MathF.Round(actor.VisualPosition.X * 32), 19 - MathF.Round(actor.VisualPosition.Y * 32), 0));
            townType.GetMethod("DrawHero", flags)!.Invoke(town, [batch, actor, time, false, false]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var result = new Color[64 * 64]; target.GetData(result); return result;
        }
        Color[] DustFrame(double time)
        {
            var feet = actor.VisualPosition * 32 + new Vector2(16, 25);
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp,
                transformMatrix: Matrix.CreateTranslation(-feet.X + 32, -feet.Y + 32, 0));
            sunType.GetMethod("DrawDust")!.Invoke(effect, [batch, pixel, time]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var result = new Color[64 * 64]; target.GetData(result); return result;
        }
        Color[] Frame()
        {
            Call("Draw", new GameTime()); var result = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(result); return result;
        }
        void Save(Color[] pixels, int width, int height, string name)
        {
            using var texture = new Texture2D(game.GraphicsDevice, width, height); texture.SetData(pixels);
            using var stream = File.Create(Path.Combine(directory, name)); texture.SaveAsPng(stream, width, height);
        }
    }
}
