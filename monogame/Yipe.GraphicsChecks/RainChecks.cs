using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class RainChecks
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
            "sunShaftStrength", "cloudShadowStrength", "rainStrength", "outdoorWeatherTime", "tickRemaining", "pausePage", "selected", "selectionY", "menuMessage", "messageRemaining" }
            .ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var rain = type.Assembly.GetType("Yipe.Desktop.Rain")!;
        float Weather(double time) => (float)rain.GetMethod("WeatherAmount")!.Invoke(null, [time])!;
        var wetTime = (double)Enumerable.Range(300, 400).First(second => Weather(second) == 1);
        var directory = Path.Combine(output, "rain"); Directory.CreateDirectory(directory);
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 320, 180);
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0);
            Set("outdoorWeatherTime", wetTime);
            foreach (var id in new[] { 17, 1, 7, 41, 57, 117, 118 })
            {
                var adventure = Scene(id);
                var snapshot = GameSaveCodec.Encode(adventure.Session, catalog);
                var off = Frame(0, 1); var strong = Frame(2, 1);
                check(id < 41 ? !off.SequenceEqual(strong) : off.SequenceEqual(strong), $"Map {id}: rain and impacts affect only outdoor scenes");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(adventure.Session, catalog)), $"Map {id}: rain preserves gameplay state and RNG");
                if (id >= 41) continue;
                Save(off, $"map-{id:D3}-off.png"); Save(Frame(1, 1), $"map-{id:D3}-subtle.png"); Save(strong, $"map-{id:D3}-strong.png");
            }
            var town = Scene(17);
            Set("outdoorWeatherTime", 0d);
            var clear = Frame(2, 1);
            check(clear.SequenceEqual(Frame(0, 1)), "Automatic weather begins with a clear spell and an exact rain bypass");
            Set("outdoorWeatherTime", wetTime);
            check(!clear.SequenceEqual(Frame(2, 1)) && (double)Get("outdoorWeatherTime")! == wetTime,
                "The production renderer starts a scheduled shower without advancing its timer during Draw");
            Save(clear, "automatic-clear.png"); Save(Frame(2, 1), "automatic-shower.png");
            Set("outdoorWeatherTime", wetTime - 2); Save(Frame(2, 1), "automatic-arriving.png");
            var wetEnd = (double)Enumerable.Range((int)wetTime, 60).First(second => Weather(second) == 0);
            Set("outdoorWeatherTime", wetEnd - 2); Save(Frame(2, 1), "automatic-ending.png");
            Set("outdoorWeatherTime", wetEnd);
            check(clear.SequenceEqual(Frame(2, 1)), "A completed shower returns to the exact clear-weather frame");
            Set("outdoorWeatherTime", wetTime);
            Set("tickRemaining", .512d);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.1)));
            check(Math.Abs((double)Get("outdoorWeatherTime")! - wetTime - .1) < .00001, "Active outdoor play advances the weather timer");
            Set("rainStrength", 0); FreezeCheck("Rain Off suspends the weather timer");
            Set("rainStrength", 2); Scene(41); FreezeCheck("Indoor play suspends the outdoor weather timer");
            Scene(17); Call("ShowActions"); FreezeCheck("Action overlays suspend the weather timer"); Set("overlay", null);
            Set("mapOpen", true); FreezeCheck("Map viewing suspends the weather timer"); Set("mapOpen", false);
            Set("outdoorWeatherTime", wetTime);
            var native = Frame(2, 1);
            check(Enumerable.Range(0, native.Length).All(i => native[i] == native[i / 1280 / 4 * 4 * 1280 + i % 1280 / 4 * 4]),
                "Rain and splashes preserve exact native pixels");
            check(native.SequenceEqual(Frame(2, 1)) && !native.SequenceEqual(Frame(2, 1.15)), "Rain repeats at fixed scene time and animates when it advances");
            var subtle = Field(1, Vector2.Zero); var strongField = Field(2, Vector2.Zero);
            check(Field(0, Vector2.Zero).All(p => p == Color.Black) && strongField.Count(p => p != Color.Black) > subtle.Count(p => p != Color.Black),
                "Rain Off bypasses rendering; Strong increases drop density");
            check(Field(.35f, Vector2.Zero).Count(p => p != Color.Black) < subtle.Count(p => p != Color.Black)
                && Field(.35f, Vector2.Zero).Any(p => p != Color.Black), "Automatic transitions thin rainfall gradually while keeping visible drops");
            var shifted = Field(2, new(13, 7));
            check((from y in Enumerable.Range(0, 173) from x in Enumerable.Range(0, 307)
                select shifted[y * 320 + x] == strongField[(y + 7) * 320 + x + 13]).All(same => same),
                "Rain stays anchored to the world when the camera moves");
            var lake = new Rectangle(90, 40, 140, 90);
            var water = Field(2, Vector2.Zero, water: lake);
            check(water.Any(p => p != Color.Black) && Enumerable.Range(0, water.Length)
                .All(i => water[i] == Color.Black || lake.Contains(i % 320, i / 320)), "Water impact rings stay inside the supplied shoreline mask");
            check(Field(2, Vector2.Zero, water: lake, tile: 0x0b).All(p => p == Color.Black), "Water rings never appear on bridge decks");
            check(Field(2, Vector2.Zero, water: Rectangle.Empty, tile: 1).All(p => p == Color.Black), "Ground impacts exclude solid building tiles");
            check(Field(2, Vector2.Zero, water: Rectangle.Empty, tile: 2).Any(p => p != Color.Black), "Open ground receives small splash pixels");

            Set("paused", true); Set("pauseAmount", 1f); Set("pausePage", "Atmosphere"); Set("selected", 3); Set("selectionY", 36f);
            var paused = Frame(2, 1);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.2)));
            check((double)Get("sceneTime")! == 1 && (double)Get("outdoorWeatherTime")! == wetTime && paused.SequenceEqual(Frame(2, 1)), "Production pause freezes rainfall, splashes and the automatic weather timer");
            for (var expected = 0; expected < 3; expected++)
            {
                Call("ActivateMenu");
                var profile = Get("profile")!; var profileType = profile.GetType();
                var path = (string)profileType.GetField("path", flags)!.GetValue(profile)!;
                var data = profileType.GetProperty("Data")!.GetValue(Activator.CreateInstance(profileType, [path]))!;
                check((int)Get("rainStrength")! == expected && (int)data.GetType().GetProperty("RainStrength")!.GetValue(data)! == expected
                    && (int)data.GetType().GetProperty("CloudShadowStrength")!.GetValue(data)! == 0,
                    $"Rain menu cycles to {expected} and saves independently of cloud shadows");
            }
            Set("menuMessage", ""); var menuOff = Frame(0, 1); var menuOn = Frame(1, 1); Save(menuOn, "menu.png");
            check((from y in Enumerable.Range(6 * 4, 168 * 4) where y < 91 * 4 || y >= 98 * 4
                from x in Enumerable.Range(34 * 4, 252 * 4) select y * 1280 + x).All(i => menuOff[i] == menuOn[i]),
                "Rain leaves the pause panel and other settings unchanged");
            Set("paused", false); Set("pauseAmount", 0f); Call("ShowActions");
            var actionsOff = Frame(0, 1); var actionsOn = Frame(2, 1);
            check((from y in Enumerable.Range(20 * 4, 140 * 4) from x in Enumerable.Range(24 * 4, 272 * 4)
                select y * 1280 + x).All(i => actionsOff[i] == actionsOn[i]), "Rain never draws over action panels or text");
            Set("overlay", null);
            var journal = new MapJournal(); journal.Observe(town); Set("journal", journal);
            Set("mapOpen", true); Set("worldMapOpen", false);
            var mapOff = Frame(0, 1);
            check(mapOff.SequenceEqual(Frame(2, 1)), "Rain and splashes are omitted from the area map");
            Set("mapOpen", false); Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", true);
            Set("sunShaftStrength", 1); Set("cloudShadowStrength", 1);
            var combined = Frame(1, 1); Save(combined, "map-017-combined.png");
            check(combined.All(p => p.A == 255), "Rain composes with lighting, sun, clouds, miniature, vignette and scanlines");
            Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0); Set("vignette", false);
            for (var frame = 0; frame < 192; frame++)
            {
                Set("outdoorWeatherTime", wetTime - 6 + frame / 24d);
                Save(Frame(2, frame / 24d), $"animation-{frame:D3}.png");
            }
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(oldPosition);
            Call("SavePreferences"); Set("menuMessage", saved["menuMessage"]);
        }

        void FreezeCheck(string label)
        {
            var before = (double)Get("outdoorWeatherTime")!;
            Set("tickRemaining", .512d);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.1)));
            check((double)Get("outdoorWeatherTime")! == before, label);
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
        Color[] Field(float strength, Vector2 camera, Rectangle? water = null, ushort tile = 0x0e)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Black);
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(-camera.X, -camera.Y, 0));
            if (water is { } lake)
                rain.GetMethod("DrawSplashes")!.Invoke(null, [batch, Get("pixel"), 1d, strength,
                    (Func<MapPosition, ushort>)(_ => tile), (Func<int, int, bool>)((x, y) => lake.Contains(x, y)), null]);
            else rain.GetMethod("DrawStreaks")!.Invoke(null, [batch, Get("pixel"), 1d, strength, null]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[320 * 180]; target.GetData(pixels); return pixels;
        }
        Color[] Frame(int strength, double time)
        {
            Set("rainStrength", strength); Set("sceneTime", time); Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels;
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
    }
}
