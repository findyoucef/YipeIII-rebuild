using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class DayNightChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "session", "adventure", "cameraPosition", "paused", "pauseAmount", "overlay", "mapOpen",
            "worldMapOpen", "journal", "sceneTime", "miniatureMode", "scanlines", "vignette", "pixelLighting",
            "sunShaftStrength", "cloudShadowStrength", "rainStrength", "outdoorWeatherTime", "tickRemaining",
            "graphicsMenuOpen", "selected", "selectionY", "menuMessage", "messageRemaining", "timeOfDayMode", "dayNightSeconds", "dayNightUpdatedAt",
            "saving", "loading", "studioSplashRemaining" }
            .ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var directory = Path.Combine(output, "day-night"); Directory.CreateDirectory(directory);
        using var capture = new Texture2D(game.GraphicsDevice, 320, 180);
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("mapOpen", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0); Set("rainStrength", 0);
            foreach (var id in new[] { 17, 1, 8, 33, 41, 117 })
            {
                var adventure = Scene(id);
                var snapshot = GameSaveCodec.Encode(adventure.Session, catalog);
                Set("pixelLighting", false);
                var day = Frame(1); var night = Frame(3);
                check(id <= 40 ? !day.SequenceEqual(night) : day.SequenceEqual(night),
                    $"Map {id}: time-of-day color affects outdoor scenery while underground rendering stays unchanged");
                Save(day, $"map-{id}-day.png"); Save(night, $"map-{id}-night-unlit.png");
                if (id <= 40)
                {
                    check(day.Zip(night).All(pair => pair.First.A == pair.Second.A)
                        && night.Sum(p => p.R + p.G + p.B) >= day.Sum(p => p.R + p.G + p.B) * .5,
                        $"Map {id}: moonlight preserves opacity and at least half the scene's average intensity with local lighting Off");
                    Set("pixelLighting", true);
                    Save(Frame(1), $"map-{id}-day-lit.png"); Save(Frame(2), $"map-{id}-dusk.png");
                    Save(Frame(3), $"map-{id}-night.png"); Set("dayNightSeconds", 1140d);
                    Save(Frame(0), $"map-{id}-dawn.png");
                }
                else
                {
                    Set("pixelLighting", true);
                    check(Frame(1).SequenceEqual(Frame(3)), $"Map {id}: torch and flame lighting retain their existing nighttime appearance");
                }
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(adventure.Session, catalog)),
                    $"Map {id}: all time-of-day rendering preserves the complete adventure and RNG");
            }
            var town = Scene(17);
            Set("pixelLighting", true);
            Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0);
            var dark = Frame(3);
            Set("sunShaftStrength", 2); Set("cloudShadowStrength", 2);
            check(dark.SequenceEqual(Frame(3)), "Night removes sun shafts and cloud shade without changing their saved preferences");
            check(!Frame(1).SequenceEqual(dark), "Day restores the same enabled sunlight and cloud effects");
            Set("dayNightSeconds", 659d); Set("timeOfDayMode", 0); Set("sceneTime", 1d);
            Advance("Active exploration advances the cycle independently of Rain Off", frozen: false);
            check((double)Get("outdoorWeatherTime")! == (double)saved["outdoorWeatherTime"]!,
                "Rain Off retains its independent weather clock while daylight advances");
            Scene(41); Advance("Underground exploration advances the outside clock without resetting it at map entry", frozen: false);
            Scene(17);
            Set("paused", true); Advance("Pause advances the automatic cycle while keeping gameplay frozen"); Set("paused", false);
            Call("ShowActions"); Advance("Action panels advance the automatic cycle while keeping gameplay frozen"); Set("overlay", null);
            Set("mapOpen", true); Advance("Map viewing advances the automatic cycle while keeping gameplay frozen"); Set("mapOpen", false);
            Set("saving", new TaskCompletionSource<bool>().Task); Advance("An in-flight save advances the automatic cycle without simulating gameplay"); Set("saving", null);
            Set("loading", new TaskCompletionSource<GameSession>().Task); Advance("An in-flight load advances the automatic cycle without simulating gameplay"); Set("loading", null);
            Set("studioSplashRemaining", 1d); Advance("The company ident advances the automatic cycle without simulating gameplay"); Set("studioSplashRemaining", 0d);

            var platform = typeof(Game).GetField("Platform", flags)!.GetValue(game)!;
            var active = platform.GetType().GetProperty("IsActive")!;
            var wasActive = game.IsActive;
            try
            {
                active.SetValue(platform, false);
                Advance("An inactive app advances the automatic cycle while movement, encounters and RNG remain frozen", 10);
            }
            finally { active.SetValue(platform, wasActive); }
            Set("paused", true); Set("dayNightSeconds", 710d);
            Advance("A suspension gap catches up to Night using wall time, not the frame's clamped elapsed time", 130);
            Call("Draw", new GameTime());
            check((double)Get("dayNightSeconds")! is >= 840 and < 841
                && (float)Get("sunShafts")!.GetType().GetProperty("Daylight")!.GetValue(Get("sunShafts"))! == 0,
                "Returning after suspension updates night lighting immediately, even while paused");
            Advance("Long suspension gaps wrap multiple cycles without simulating missed gameplay", 3607.75);
            var caughtUp = (double)Get("dayNightSeconds")!;
            Update();
            check((double)Get("dayNightSeconds")! - caughtUp is >= 0 and < 1,
                "The next frame never reapplies a suspension interval");
            var beforeRollback = Get("dayNightSeconds");
            Set("dayNightUpdatedAt", DateTime.UtcNow.AddMinutes(1));
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.1)));
            check(Equals(beforeRollback, Get("dayNightSeconds")), "A backward system-clock adjustment never rewinds the cosmetic phase");
            Set("paused", false);
            Set("timeOfDayMode", 3); Freeze("A fixed palette suspends the previous automatic phase");
            Set("timeOfDayMode", 0); Advance("Returning to Auto resumes the previous phase without counting time spent in fixed mode", frozen: false);

            Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0);
            var journal = new MapJournal(); journal.Observe(town); Set("journal", journal);
            Set("mapOpen", true); Set("worldMapOpen", false);
            check(Frame(1).SequenceEqual(Frame(3)), "Area maps retain their normal navigation colors at night");
            Set("worldMapOpen", true);
            check(Frame(1).SequenceEqual(Frame(3)), "World maps retain their normal navigation colors at night");
            Set("mapOpen", false);
            Call("ShowActions"); var actionsDay = Frame(1); var actionsNight = Frame(3);
            check((from y in Enumerable.Range(20, 140) from x in Enumerable.Range(24, 272)
                select y * 320 + x).All(i => actionsDay[i] == actionsNight[i]),
                "Night color never dims action panels or their text");
            Set("overlay", null); Set("paused", true); Set("pauseAmount", 1f);
            Set("graphicsMenuOpen", true); Set("selected", 9); Set("selectionY", 90f); Set("timeOfDayMode", 0);
            var phase = (double)Get("dayNightSeconds")!;
            for (var mode = 1; mode <= 4; mode++)
            {
                Call("ActivateMenu");
                var profile = Get("profile")!; var profileType = profile.GetType();
                var path = (string)profileType.GetField("path", flags)!.GetValue(profile)!;
                var reopened = Activator.CreateInstance(profileType, [path])!;
                var data = profileType.GetProperty("Data")!.GetValue(reopened)!;
                check((int)Get("timeOfDayMode")! == mode % 4
                    && (int)data.GetType().GetProperty("TimeOfDayMode")!.GetValue(data)! == mode % 4
                    && (double)data.GetType().GetProperty("DayNightSeconds")!.GetValue(data)! == phase,
                    $"Time-of-day menu persists mode {mode % 4} without discarding its automatic phase");
            }
            Save(Frame(3), "menu-night.png"); Set("paused", false); Set("pauseAmount", 0f); Set("menuMessage", "");
            var hidden = Scene(41);
            for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++) hidden.Map.RestoreTile(new(x, y), 0x22);
            hidden.Map.RestoreTile(new(3, 3), 0x27); hidden.Map.RestoreTile(new(6, 3), 0x24);
            hidden.Visibility.Initialize(hidden.Map, new(3, 3));
            Set("pixelLighting", false); var fog = Frame(3); Set("pixelLighting", true);
            check(fog.SequenceEqual(Frame(3)), "Selecting Night cannot reveal an undiscovered underground fire source");

            var battleScene = Scene(17);
            var placement = Enumerable.Range(1, 128).SelectMany(id => MonsterPlacements.ForMap(id, 2, 1))
                .First(p => p.CompanionType1 > 0 && p.CompanionType2 > 0);
            typeof(Adventure).GetProperty("Battle")!.SetValue(battleScene,
                Combat.StartMonsterEncounter(placement, battleScene.Player, battleScene.Session.Random, battleScene.Map.Environment));
            Call("ShowBattle"); var battleDay = Frame(1); var battleNight = Frame(3); Save(battleNight, "battle-night.png");
            check((from y in Enumerable.Range(20, 140) from x in Enumerable.Range(24, 272)
                select y * 320 + x).All(i => battleDay[i] == battleNight[i]),
                "Three-enemy battle portraits, health bars and commands keep their normal nighttime brightness");
            Set("timeOfDayMode", 0); Advance("A live battle menu advances the automatic cycle while combat state and RNG remain frozen"); Set("overlay", null);

            Scene(17); Set("pixelLighting", true); Set("timeOfDayMode", 0);
            for (var frame = 0; frame < 240; frame++)
            {
                Set("dayNightSeconds", frame * 5d); Set("sceneTime", frame / 24d);
                Save(Frame(0), $"cycle-{frame:D3}.png");
            }
            Set("miniatureMode", 2); Set("scanlines", true); Set("vignette", true);
            Set("rainStrength", 2);
            var weather = type.Assembly.GetType("Yipe.Desktop.Rain")!.GetMethod("WeatherAmount")!;
            Set("outdoorWeatherTime", (double)Enumerable.Range(300, 600)
                .First(second => (float)weather.Invoke(null, [(double)second])! >= .9f));
            Save(Frame(3), "night-combined.png");
            check(Frame(3).All(p => p.A == 255), "Night composes with rain, local lights, miniature, vignette and scanlines");
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(oldPosition); Call("SavePreferences"); Set("menuMessage", saved["menuMessage"]);
            Call("Draw", new GameTime());
        }

        void Update()
        {
            Set("tickRemaining", .512d);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.1)));
        }
        void Advance(string label, double elapsed = .1, bool frozen = true)
        {
            var phase = (double)Get("dayNightSeconds")!;
            var clock = DateTime.UtcNow.AddSeconds(-elapsed);
            var scene = Get("sceneTime"); var position = player.VisualPosition;
            var snapshot = GameSaveCodec.Encode((GameSession)Get("session")!, catalog);
            Set("dayNightUpdatedAt", clock); Update();
            var wallElapsed = ((DateTime)Get("dayNightUpdatedAt")! - clock).TotalSeconds;
            var expected = (phase + wallElapsed) % 1200;
            check(Math.Abs((double)Get("dayNightSeconds")! - expected) < .000001
                && (!frozen || Equals(scene, Get("sceneTime")) && player.VisualPosition == position
                    && snapshot.SequenceEqual(GameSaveCodec.Encode((GameSession)Get("session")!, catalog))), label);
        }
        void Freeze(string label)
        {
            var before = Get("dayNightSeconds"); Update(); check(Equals(before, Get("dayNightSeconds")), label);
        }
        Adventure Scene(int id)
        {
            var layer = id >= 117 ? 0 : (id - 1) / 40;
            var cell = id >= 117 ? id - 99 : id - 1 - layer * 40;
            var session = new GameSession(catalog, location: new()
            { MapId = id, Position = new(6, 5), MapLayer = layer, AlternateMapSet = id >= 117, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6 });
            var adventure = new Adventure(session);
            for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
                adventure.Visibility.RevealFrom(adventure.Map, new(x, y));
            Set("session", session); Set("adventure", adventure); player.SnapTo(new(6, 5));
            Set("cameraPosition", new Vector2(48, 86)); return adventure;
        }
        Color[] Frame(int mode)
        {
            Set("timeOfDayMode", mode); Call("Draw", new GameTime());
            // Heat and miniature may compose the world after the canvas. Sample the actual presented image.
            var presented = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(presented);
            return Enumerable.Range(0, 320 * 180)
                .Select(i => presented[(i / 320 * 4 + 2) * 1280 + i % 320 * 4 + 2]).ToArray();
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 320, 180);
        }
    }
}
