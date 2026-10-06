using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class PixelLightingChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        var saved = new[] { "session", "adventure", "cameraPosition", "paused", "pauseAmount", "overlay", "mapOpen",
            "worldMapOpen", "journal", "sceneTime", "miniatureMode", "scanlines", "vignette", "pixelLighting", "selected", "selectionY", "menuMessage", "messageRemaining" }
            .ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var lighting = Get("lighting")!; var lightType = lighting.GetType();
        var prepare = lightType.GetMethod("Prepare")!;
        var apply = lightType.GetMethod("Apply")!;
        var draw = type.GetMethod("Draw", flags)!;
        var menu = type.GetMethod("ActivateMenu", flags)!;
        var directory = Path.Combine(output, "pixel-lighting"); Directory.CreateDirectory(directory);
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 320, 180);
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            foreach (var (id, position) in new (int, MapPosition)[]
                { (17, new(8, 5)), (57, new(10, 6)), (63, new(6, 3)), (117, new(6, 7)), (118, new(6, 5)), (119, new(6, 5)), (120, new(6, 5)) })
            {
                var adventure = Scene(id, position);
                var snapshot = GameSaveCodec.Encode(adventure.Session, catalog);
                Set("pixelLighting", false); var before = Frame(1); Save(before, $"map-{id}-off.png");
                Set("pixelLighting", true); var after = Frame(1); Save(after, $"map-{id}-on.png");
                check(before.Zip(after).Count(p => p.First != p.Second) > 64,
                    $"Map {id}: local lights visibly affect scenery and nearby actors");
                check(after.SequenceEqual(Frame(1)), $"Map {id}: lighting repeats exactly at a paused scene clock");
                check(GameSaveCodec.Encode(adventure.Session, catalog).SequenceEqual(snapshot),
                    $"Map {id}: lighting preserves gameplay, map contents and RNG");
                Set("miniatureMode", 2); Set("vignette", true); Set("scanlines", true);
                check(Frame(1).All(p => p.A == 255), $"Map {id}: lighting composes with heat, miniature, vignette and scanlines");
                Save(Frame(1), $"map-{id}-combined.png");
                Set("miniatureMode", 0); Set("vignette", false); Set("scanlines", false);
            }

            var fixture = Scene(57, new(4, 4));
            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++) fixture.Map.RestoreTile(new(x, y), 0x27);
            fixture.Map.RestoreTile(new(4, 3), 0x22);
            var open = LightFrame(fixture, 1);
            check(open.Any(p => p.R > 0), "A wall-mounted cave torch emits a local light field");
            fixture.Map.RestoreTile(new(5, 3), 0x3e);
            var blocked = LightFrame(fixture, 1);
            var behind = 116 * 320 + 196;
            check(open[behind].R > 0 && blocked[behind].R == 0,
                "A closed door blocks torchlight from reaching the floor behind it");
            fixture.Map.OpenDoor(new(5, 4), new(5, 3));
            check(LightFrame(fixture, 1).SequenceEqual(open), "Opening a door invalidates the lighting cache and restores the light path");
            foreach (var secret in new ushort[] { 0x43, 0x23, 0x44, 0x25, 0x5a })
            {
                fixture.Map.RestoreTile(new(4, 3), secret);
                check(LightFrame(fixture, 1).SequenceEqual(open), $"Wall variant {secret:x2} cannot reveal a secret through its lighting");
            }
            var later = LightFrame(fixture, 1.5);
            check(!open.SequenceEqual(later) && open.SequenceEqual(LightFrame(fixture, 1)),
                "Torch flicker advances only with scene time and preserves its fixed footprint");
            fixture.Map.RestoreTile(new(4, 3), 0x27);
            check(LightFrame(fixture, 1).All(p => p.R == 0 && p.G == 0 && p.B == 0),
                "Removing a torch wall leaves no stale light behind");

            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++) fixture.Map.RestoreTile(new(x, y), 0x22);
            fixture.Map.RestoreTile(new(6, 4), 0x24);
            fixture.Visibility.Initialize(fixture.Map, new(4, 4));
            check(LightFrame(fixture, 1).All(p => p.R == 0 && p.G == 0 && p.B == 0),
                "An undiscovered fire source cannot cast light into a discovered room");
            fixture.Visibility.RevealFrom(fixture.Map, new(6, 4));
            var fog = LightFrame(fixture, 1);
            check(fog.Any(p => p.R > 0) && Enumerable.Range(0, fog.Length)
                .Where(i => !fixture.Visibility.Contains(new(i % 320 / 32, i / 320 / 32)))
                .All(i => fog[i].R == 0 && fog[i].G == 0 && fog[i].B == 0),
                "Visible fire light never paints undiscovered map cells");

            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++) fixture.Map.RestoreTile(new(x, y), 0x24);
            Reveal(fixture);
            var dense = LightFrame(fixture, 1);
            check(dense.All(p => p.R <= 86 && p.G <= 51 && p.B <= 22),
                "Overlapping flames stay below one source's peak brightness instead of accumulating bloom");
            prepare.Invoke(lighting, [batch, fixture, Matrix.Identity, 1d]);
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Black);
            apply.Invoke(lighting, [batch]); game.GraphicsDevice.SetRenderTarget(null);
            var black = new Color[320 * 180]; target.GetData(black);
            check(black.All(p => p == Color.Black), "Local lighting preserves black outlines and opaque alpha instead of adding haze");
            prepare.Invoke(lighting, [batch, fixture, Matrix.Identity, 1d]);
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(new Color(64, 48, 32));
            apply.Invoke(lighting, [batch]); game.GraphicsDevice.SetRenderTarget(null);
            var lit = new Color[320 * 180]; target.GetData(lit);
            check(lit.Any(p => p.R > 64) && lit.All(p => p.R is >= 64 and <= 86 && p.G is >= 48 and <= 58 && p.B is >= 32 and <= 35 && p.A == 255),
                "The light pass brightens existing colors gently without flattening terrain contrast");

            var town = Scene(17, new(8, 5));
            Set("paused", true); Set("pauseAmount", 1f); Set("selected", 8); Set("selectionY", 80f);
            var on = Frame(1); menu.Invoke(game, null); var off = Frame(1);
            check(!(bool)Get("pixelLighting")!, "Pixel lighting switches off through the pause menu");
            var profile = Get("profile")!; var profileType = profile.GetType();
            var profilePath = (string)profileType.GetField("path", flags)!.GetValue(profile)!;
            var reloaded = Activator.CreateInstance(profileType, [profilePath])!;
            var data = profileType.GetProperty("Data")!.GetValue(reloaded)!;
            check(!(bool)data.GetType().GetProperty("PixelLighting")!.GetValue(data)!, "The lighting menu preference survives reopening its profile");
            Set("menuMessage", ""); off = Frame(1); Set("pixelLighting", true); on = Frame(1);
            check((from y in Enumerable.Range(6 * 4, 168 * 4) where y < 124 * 4 || y >= 131 * 4
                from x in Enumerable.Range(34 * 4, 252 * 4) select y * 1280 + x).All(i => on[i] == off[i]),
                "Pixel lighting leaves the pause panel and every other label unchanged");
            Save(on, "menu.png");
            Set("paused", false); Set("pauseAmount", 0f);
            var journal = new MapJournal(); journal.Observe(town); Set("journal", journal); Set("mapOpen", true); Set("worldMapOpen", false);
            var mapOn = Frame(1); Set("pixelLighting", false);
            check(mapOn.SequenceEqual(Frame(1)), "Local lighting leaves the area map and its labels unchanged");
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(oldPosition);
            type.GetMethod("SavePreferences", flags)!.Invoke(game, null);
            Set("menuMessage", saved["menuMessage"]);
        }

        Adventure Scene(int id, MapPosition position)
        {
            var layer = id >= 117 ? 0 : (id - 1) / 40;
            var cell = id >= 117 ? id - 99 : id - 1 - layer * 40;
            var session = new GameSession(catalog, location: new()
            { MapId = id, Position = position, MapLayer = layer, AlternateMapSet = id >= 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6 });
            var adventure = new Adventure(session); Reveal(adventure);
            Set("session", session); Set("adventure", adventure); player.SnapTo(position);
            Set("cameraPosition", Vector2.Clamp(new Vector2(position.X, position.Y) * 32 + new Vector2(16) - new Vector2(160, 90), Vector2.Zero, new Vector2(96, 204)));
            return adventure;
        }
        void Reveal(Adventure adventure)
        {
            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++) adventure.Visibility.RevealFrom(adventure.Map, new(x, y));
        }
        Color[] LightFrame(Adventure adventure, double time)
        {
            prepare.Invoke(lighting, [batch, adventure, Matrix.Identity, time]); game.GraphicsDevice.SetRenderTarget(null);
            var texture = (Texture2D)lightType.GetField("lightMap", flags)!.GetValue(lighting)!;
            var pixels = new Color[320 * 180]; texture.GetData(pixels); return pixels;
        }
        Color[] Frame(double time)
        {
            Set("sceneTime", time); draw.Invoke(game, [new GameTime()]);
            var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels;
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
    }
}
