using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class CloudShadowChecks
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
            "sunShaftStrength", "cloudShadowStrength", "pausePage", "selected", "selectionY", "menuMessage", "messageRemaining" }
            .ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var daylight = Get("sunShafts")!;
        var clouds = daylight.GetType().GetMethod("DrawClouds")!;
        var shafts = daylight.GetType().GetMethod("Draw")!;
        var directory = Path.Combine(output, "cloud-shadows"); Directory.CreateDirectory(directory);
        var silhouettes = (Texture2D)daylight.GetType().GetField("cloudMask", flags)!.GetValue(daylight)!;
        using (var file = File.Create(Path.Combine(directory, "cloud-silhouettes.png")))
            silhouettes.SaveAsPng(file, silhouettes.Width, silhouettes.Height);
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 320, 180);
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        var gray = new Color(200, 200, 200);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            Set("sunShaftStrength", 1);
            foreach (var id in new[] { 17, 1, 7, 8, 22, 27, 41, 57, 117, 118 })
            {
                var adventure = Scene(id);
                var snapshot = GameSaveCodec.Encode(adventure.Session, catalog);
                var off = Frame(0, 1); var strong = Frame(2, 1);
                check(id < 41 ? !off.SequenceEqual(strong) : off.SequenceEqual(strong),
                    $"Map {id}: cloud shadows affect only outdoor scenery and actors");
                if (id < 41)
                {
                    Save(off, $"map-{id:D3}-off.png"); Save(Frame(1, 1), $"map-{id:D3}-subtle.png");
                    Save(strong, $"map-{id:D3}-strong.png");
                    Set("sunShaftStrength", 0); Save(Frame(2, 1), $"map-{id:D3}-clouds-only.png");
                    Set("sunShaftStrength", 1);
                    Set("miniatureMode", 2); Set("vignette", true); Set("scanlines", true);
                    var combined = Frame(1, 1); Save(combined, $"map-{id:D3}-combined.png");
                    check(combined.All(p => p.A == 255), $"Map {id}: clouds compose with sun, local lighting, miniature, heat, vignette and scanlines");
                    Set("miniatureMode", 0); Set("vignette", false); Set("scanlines", false);
                }
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(adventure.Session, catalog)),
                    $"Map {id}: cloud rendering preserves the full gameplay state and RNG");
            }

            var town = Scene(17);
            var subtle = Field(gray, Vector2.Zero, 1, 1);
            var strongField = Field(gray, Vector2.Zero, 1, 2);
            using (var file = File.Create(Path.Combine(directory, "cloud-feathering.png")))
                target.SaveAsPng(file, target.Width, target.Height);
            check(strongField.Any(p => p.R < 190) && strongField.Any(p => p.R == 200)
                && strongField.All(p => p.R is >= 172 and <= 200 && p.R == p.G && p.G == p.B && p.A == 255)
                && subtle.Zip(strongField).All(p => p.First.R >= p.Second.R && p.First.R <= 200),
                "Cloud strengths gently shade existing colors, keep clear sky and never brighten or change alpha");
            check(strongField.Where(p => p.R < 190).Distinct().Count() >= 3,
                "Cloud banks retain gentle density variation within their shade");
            check(Field(Color.Black, Vector2.Zero, 1, 2).All(p => p == Color.Black), "Cloud shade preserves black outlines and hidden pixels exactly");
            check(Field(gray, Vector2.Zero, 1, 0).All(p => p == gray), "Cloud shadows Off is an exact rendering bypass");
            check(strongField.SequenceEqual(Field(gray, Vector2.Zero, 1, 2))
                && !strongField.SequenceEqual(Field(gray, Vector2.Zero, 9, 2)), "Clouds freeze at a fixed clock and drift with advancing scene time");
            var betweenPixels = Field(gray, Vector2.Zero, 1.08, 2);
            check(!strongField.SequenceEqual(betweenPixels)
                && strongField.Zip(betweenPixels).All(p => Math.Abs(p.First.R - p.Second.R) <= 10),
                "Fractional cloud travel changes edge opacity smoothly between whole-pixel steps");
            var shifted = Field(gray, new Vector2(13, 7), 1, 2);
            check((from y in Enumerable.Range(0, 173) from x in Enumerable.Range(0, 307)
                select shifted[y * 320 + x] == strongField[(y + 7) * 320 + x + 13]).All(same => same),
                "Cloud shade stays attached to world pixels when the camera moves");
            var wrapped = Field(gray, new Vector2(96, 204), 250, 2);
            check(wrapped.Any(p => p.R < 200) && wrapped.All(p => p.A == 255), "Cloud coverage survives texture wrapping at the far camera bounds");
            foreach (var (camera, time) in new[] { (Vector2.Zero, 1d), (new Vector2(48, 86), 9d), (new Vector2(96, 204), 250d) })
            {
                var sunlight = Field(gray, camera, time, 0, sun: true);
                var shade = Field(gray, camera, time, 2);
                var combined = Field(gray, camera, time, 2, sun: true);
                check(Enumerable.Range(0, shade.Length).All(i => combined[i].R >= shade[i].R && combined[i].R <= sunlight[i].R)
                    && Enumerable.Range(0, shade.Length).Any(i => shade[i].R < 185 && sunlight[i].R > 205 && combined[i].R < sunlight[i].R),
                    $"Independently drifting clouds attenuate shafts without cutting gaps in their silhouettes at time {time}");
                var covered = Enumerable.Range(0, shade.Length).First(i => shade[i].R < 185 && sunlight[i].R > 205);
                var clear = Enumerable.Range(0, shade.Length).First(i => shade[i].R == 200 && sunlight[i].R > 205);
                float Light(int i) => (float)daylight.GetType().GetMethod("LightAt", flags)!.Invoke(daylight,
                    [camera + new Vector2(i % 320, i / 320), time, 2])!;
                daylight.GetType().GetMethod("Update")!.Invoke(daylight, [town.Map, player, time, 2, 0]);
                var clearLight = Light(clear); var coveredLight = Light(covered);
                daylight.GetType().GetMethod("Update")!.Invoke(daylight, [town.Map, player, time, 2, 2]);
                check(coveredLight > 0 && Light(covered) < coveredLight * .5f && Light(clear) >= clearLight * .97f
                    && new[] { covered, clear }.All(i => Math.Abs(combined[i].R - shade[i].R * (1 + Light(i) * 48 / 255)) <= 2),
                    $"Hero highlights and dust sample the same blocked sunlight as the renderer at time {time}");
            }
            Set("sunShaftStrength", 0);
            var withoutSun = Frame(0, 1);
            check(!withoutSun.SequenceEqual(Frame(2, 1)), "Cloud shadows work independently with sun shafts disabled");
            Set("sunShaftStrength", 1);
            var native = Frame(2, 1);
            check(Enumerable.Range(0, native.Length).All(i => native[i] == native[i / 1280 / 4 * 4 * 1280 + i % 1280 / 4 * 4]),
                "Cloud shade retains exact native pixels at window scale");

            Set("paused", true); Set("pauseAmount", 1f); Set("pausePage", "Atmosphere"); Set("selected", 2); Set("selectionY", 24f);
            var paused = Frame(2, 1);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.2)));
            check((double)Get("sceneTime")! == 1 && paused.SequenceEqual(Frame(2, 1)), "Production pause freezes cloud movement and its framebuffer");
            for (var expected = 0; expected < 3; expected++)
            {
                Call("ActivateMenu");
                var profile = Get("profile")!; var profileType = profile.GetType();
                var path = (string)profileType.GetField("path", flags)!.GetValue(profile)!;
                var data = profileType.GetProperty("Data")!.GetValue(Activator.CreateInstance(profileType, [path]))!;
                check((int)Get("cloudShadowStrength")! == expected && (int)data.GetType().GetProperty("CloudShadowStrength")!.GetValue(data)! == expected
                    && (int)data.GetType().GetProperty("SunShaftStrength")!.GetValue(data)! == 1,
                    $"Cloud menu cycles to {expected} and saves independently of the sun-shaft preference");
            }
            Set("menuMessage", ""); var menuOff = Frame(0, 1); var menuOn = Frame(1, 1); Save(menuOn, "menu.png");
            check((from y in Enumerable.Range(6 * 4, 168 * 4) where y < 79 * 4 || y >= 86 * 4
                from x in Enumerable.Range(34 * 4, 252 * 4) select y * 1280 + x).All(i => menuOff[i] == menuOn[i]),
                "Cloud shadows leave the pause panel and every other label unchanged");
            Set("paused", false); Set("pauseAmount", 0f); Call("ShowActions");
            var actionsOff = Frame(0, 1); var actionsOn = Frame(2, 1);
            check((from y in Enumerable.Range(20 * 4, 140 * 4) from x in Enumerable.Range(24 * 4, 272 * 4)
                select y * 1280 + x).All(i => actionsOff[i] == actionsOn[i]), "Cloud shade leaves the action panel and text unchanged");
            Set("overlay", null);
            var journal = new MapJournal(); journal.Observe(town); Set("journal", journal);
            Set("mapOpen", true); Set("worldMapOpen", false);
            var mapOff = Frame(0, 1);
            check(mapOff.SequenceEqual(Frame(2, 1)), "Clouds never shade the area map or its labels");
            Set("mapOpen", false); Set("miniatureMode", 2); Set("scanlines", true);
            for (var frame = 0; frame < 288; frame++) Save(Frame(1, frame / 12d), $"animation-{frame:D3}.png");
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
        Color[] Field(Color background, Vector2 camera, double time, int strength, bool sun = false)
        {
            if (sun) daylight.GetType().GetMethod("Prepare")!.Invoke(daylight, [batch, camera, time, strength]);
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(background);
            clouds.Invoke(daylight, [batch, ((Adventure)Get("adventure")!).Map, camera, time, strength]);
            if (sun) shafts.Invoke(daylight, [batch, ((Adventure)Get("adventure")!).Map, camera, time, 2, strength]);
            game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[320 * 180]; target.GetData(pixels); return pixels;
        }
        Color[] Frame(int strength, double time)
        {
            Set("cloudShadowStrength", strength); Set("sceneTime", time); Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels;
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
    }
}
