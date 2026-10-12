using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class SunShaftChecks
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
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "session", "adventure", "cameraPosition", "paused", "pauseAmount", "overlay", "mapOpen",
            "worldMapOpen", "journal", "sceneTime", "miniatureMode", "scanlines", "vignette", "pixelLighting",
            "sunShaftStrength", "pausePage", "selected", "selectionY", "menuMessage", "messageRemaining" }.ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var shafts = Get("sunShafts")!;
        var render = shafts.GetType().GetMethod("Draw")!;
        var directory = Path.Combine(output, "sun-shafts"); Directory.CreateDirectory(directory);
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 320, 180);
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            foreach (var id in new[] { 17, 1, 7, 41, 57, 117, 118 })
            {
                var adventure = Scene(id);
                var before = GameSaveCodec.Encode(adventure.Session, catalog);
                var off = Frame(0, 1); var on = Frame(2, 1);
                check(id < 41 ? !off.SequenceEqual(on) : off.SequenceEqual(on),
                    $"Map {id}: sun shafts affect only outdoor scenery");
                if (id < 41)
                {
                    Save(off, $"map-{id:D3}-off.png"); Save(Frame(1, 1), $"map-{id:D3}-subtle.png");
                    Save(on, $"map-{id:D3}-strong.png");
                    Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", true);
                    var combined = Frame(1, 1); Save(combined, $"map-{id:D3}-combined.png");
                    check(combined.All(p => p.A == 255), $"Map {id}: sunlight composes with lighting, miniature, heat, scanlines and vignette");
                    Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false);
                }
                check(before.SequenceEqual(GameSaveCodec.Encode(adventure.Session, catalog)),
                    $"Map {id}: sunlight rendering preserves full gameplay state and RNG");
            }

            var town = Scene(17);
            var gray = new Color(100, 100, 100);
            var subtle = Field(gray, Vector2.Zero, 1, 1);
            var strong = Field(gray, Vector2.Zero, 1, 2);
            check(subtle.Any(p => p.R > 100) && subtle.Zip(strong).All(p => p.First.R <= p.Second.R)
                && strong.All(p => p.R is >= 100 and <= 119 && p.G <= p.R && p.B <= p.G && p.A == 255),
                "Both strengths give restrained warm light without reducing contrast or changing alpha");
            check(Field(Color.Black, Vector2.Zero, 1, 2).All(p => p == Color.Black),
                "Sun shafts preserve black outlines and hidden cells exactly");
            check(Field(gray, Vector2.Zero, 1, 0).All(p => p == gray), "Off skips the sunlight pass entirely");
            check(strong.SequenceEqual(Field(gray, Vector2.Zero, 1, 2))
                && !strong.SequenceEqual(Field(gray, Vector2.Zero, 9, 2)),
                "Shafts freeze at a fixed scene clock and drift when it advances");
            var shifted = Field(gray, new Vector2(13, 7), 1, 2);
            check((from y in Enumerable.Range(0, 173) from x in Enumerable.Range(0, 307)
                select shifted[y * 320 + x] == strong[(y + 7) * 320 + x + 13]).All(same => same),
                "Sun shafts stay attached to world pixels as the camera moves");
            var wrap = Field(gray, new Vector2(96, 204), 250, 2);
            check(wrap.Any(p => p.R > 100) && wrap.All(p => p.A == 255),
                "The drifting mask covers the camera extremes across its texture seam");

            var native = Frame(2, 1);
            check(Enumerable.Range(0, native.Length).All(i => native[i] == native[i / 1280 / 4 * 4 * 1280 + i % 1280 / 4 * 4]),
                "Sunlight retains exact native pixels when scaled to the window");
            Set("paused", true); Set("pauseAmount", 1f); Set("pausePage", "Atmosphere"); Set("selected", 1); Set("selectionY", 12f);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.2)));
            check((double)Get("sceneTime")! == 1, "The pause menu freezes the sunlight clock");
            for (var expected = 0; expected < 3; expected++)
            {
                Call("ActivateMenu");
                var profile = Get("profile")!; var profileType = profile.GetType();
                var path = (string)profileType.GetField("path", flags)!.GetValue(profile)!;
                var reloaded = Activator.CreateInstance(profileType, [path])!;
                var data = profileType.GetProperty("Data")!.GetValue(reloaded)!;
                check((int)Get("sunShaftStrength")! == expected && (int)data.GetType().GetProperty("SunShaftStrength")!.GetValue(data)! == expected,
                    $"The sun-shaft menu cycles to {expected} and persists it in the isolated profile");
            }
            Set("menuMessage", "");
            var menuOff = Frame(0, 1); var menuOn = Frame(1, 1); Save(menuOn, "menu.png");
            check((from y in Enumerable.Range(6 * 4, 168 * 4) where y < 67 * 4 || y >= 74 * 4
                from x in Enumerable.Range(34 * 4, 252 * 4) select y * 1280 + x).All(i => menuOff[i] == menuOn[i]),
                "Sun shafts leave the pause panel and every other label unchanged");
            Set("paused", false); Set("pauseAmount", 0f);
            Call("ShowActions");
            var actionsOff = Frame(0, 1); var actionsOn = Frame(2, 1);
            check((from y in Enumerable.Range(20 * 4, 140 * 4) from x in Enumerable.Range(24 * 4, 272 * 4)
                select y * 1280 + x).All(i => actionsOff[i] == actionsOn[i]), "Sunlight leaves the action panel and text unchanged");
            Set("overlay", null);
            var journal = new MapJournal(); journal.Observe(town); Set("journal", journal);
            Set("mapOpen", true); Set("worldMapOpen", false);
            var mapOff = Frame(0, 1);
            check(mapOff.SequenceEqual(Frame(2, 1)), "Sun shafts never tint the area map or its labels");
            Set("mapOpen", false); Set("miniatureMode", 2); Set("scanlines", true);
            for (var frame = 0; frame < 72; frame++) Save(Frame(1, frame / 6d), $"animation-{frame:D3}.png");
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(oldPosition);
            Call("SavePreferences"); Set("menuMessage", saved["menuMessage"]);
        }

        Adventure Scene(int id)
        {
            var layer = id >= 117 ? 0 : (id - 1) / 40;
            var cell = id >= 117 ? id - 99 : id - 1 - layer * 40;
            var session = new GameSession(catalog, location: new()
            { MapId = id, Position = new(6, 5), MapLayer = layer, AlternateMapSet = id >= 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6 });
            var adventure = new Adventure(session);
            for (var x = 0; x < Map.Width; x++)
                for (var y = 0; y < Map.Height; y++) adventure.Visibility.RevealFrom(adventure.Map, new(x, y));
            Set("session", session); Set("adventure", adventure); player.SnapTo(new(6, 5));
            Set("cameraPosition", new Vector2(48, 86));
            return adventure;
        }
        Color[] Field(Color background, Vector2 camera, double time, int strength)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(background);
            render.Invoke(shafts, [batch, ((Adventure)Get("adventure")!).Map, camera, time, strength, 0]);
            game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[320 * 180]; target.GetData(pixels); return pixels;
        }
        Color[] Frame(int strength, double time)
        {
            Set("sunShaftStrength", strength); Set("sceneTime", time); Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels;
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
    }
}
