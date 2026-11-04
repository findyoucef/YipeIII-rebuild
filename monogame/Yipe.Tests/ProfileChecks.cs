using System.Reflection;
using Yipe.Core;
using Yipe.Desktop;
using Microsoft.Xna.Framework.Input;

internal static class ProfileChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "yipe-profile-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "profile.json");
        var type = typeof(GameInput).Assembly.GetType("Yipe.Desktop.DesktopProfile", throwOnError: true)!;
        object Open() => Activator.CreateInstance(type, [path])!;
        object Data(object profile) => type.GetProperty("Data")!.GetValue(profile)!;
        object Get(object data, string property) => data.GetType().GetProperty(property)!.GetValue(data)!;
        void Set(object data, string property, object value) => data.GetType().GetProperty(property)!.SetValue(data, value);
        void Save(object profile) => type.GetMethod("Save")!.Invoke(profile, null);
        try
        {
            var fresh = Open(); var data = Data(fresh);
            check((bool)Get(data, "Music") && (float)Get(data, "MusicVolume") == .5f && (float)Get(data, "EffectsVolume") == 1f,
                "Music starts enabled with its own moderate volume");
            check((int)Get(data, "TimeOfDayMode") == 0 && (double)Get(data, "DayNightSeconds") == 300,
                "New profiles enable the automatic day/night cycle at midday");
            check((bool)Get(data, "Scanlines") && (int)Get(data, "Miniature") == 2 && (bool)Get(data, "Sound")
                && !(bool)Get(data, "Vignette") && (bool)Get(data, "PixelLighting")
                && (int)Get(data, "SunShaftStrength") == 1 && (int)Get(data, "CloudShadowStrength") == 1
                && !(bool)Get(data, "AutoBattle") && !(bool)Get(data, "ScreenShake") && (int)Get(data, "RainStrength") == 1 && !File.Exists(path), "Missing desktop profile uses the approved defaults without writing a file");
            Set(data, "Scanlines", false); Set(data, "Miniature", 1); Set(data, "Sound", false); Set(data, "Volume", .4f);
            Set(data, "Music", false); Set(data, "MusicVolume", .2f); Set(data, "EffectsVolume", .7f);
            Set(data, "Vignette", true);
            Set(data, "PixelLighting", false);
            Set(data, "SunShaftStrength", 2);
            Set(data, "CloudShadowStrength", 2);
            Set(data, "RainStrength", 2);
            Set(data, "ScreenShake", true); Set(data, "AutoBattle", true);
            Set(data, "TimeOfDayMode", 3); Set(data, "DayNightSeconds", 987.5d);
            Set(data, "TouchSize", 1.2f); Set(data, "TouchOpacity", .4f);
            var controls = (ControlBindings)Get(data, "Controls");
            controls.BindKey(4, Keys.E);
            controls.BindButton(0, Buttons.X);
            HighScores.Update((HighScoreEntry[])Get(data, "Scores"), new Player { Name = "Profile Hero", Gold = 12345 }, true);
            Save(fresh);
            var reopened = Data(Open());
            check((float)Get(reopened, "TouchSize") == 1.2f && (float)Get(reopened, "TouchOpacity") == .4f,
                "Touch size and opacity survive a profile reopen");
            check(!(bool)Get(reopened, "Music") && (float)Get(reopened, "MusicVolume") == .2f && (float)Get(reopened, "EffectsVolume") == .7f,
                "Music and effects volumes persist independently of master sound");
            check((int)Get(reopened, "TimeOfDayMode") == 3 && (double)Get(reopened, "DayNightSeconds") == 987.5,
                "Fixed time-of-day preference and suspended automatic phase survive profile reopen");
            check(!(bool)Get(reopened, "Scanlines") && (int)Get(reopened, "Miniature") == 1 && !(bool)Get(reopened, "Sound")
                && (bool)Get(reopened, "Vignette") && !(bool)Get(reopened, "PixelLighting")
                && (int)Get(reopened, "SunShaftStrength") == 2 && (int)Get(reopened, "CloudShadowStrength") == 2
                && (bool)Get(reopened, "AutoBattle") && (bool)Get(reopened, "ScreenShake") && (int)Get(reopened, "RainStrength") == 2 && (float)Get(reopened, "Volume") == .4f && ((HighScoreEntry[])Get(reopened, "Scores"))[21] == new HighScoreEntry("Profile Hero", 12345)
                && ((HighScoreEntry[])Get(reopened, "Scores"))[27].Name == "Profile Hero",
                "Desktop profile atomically reopens preferences and all score categories together");
            check(Directory.GetFiles(directory).Length == 1, "Successful profile writes leave no staging files");
            var restoredControls = (ControlBindings)Get(reopened, "Controls");
            check(restoredControls.Keyboard[4] == Keys.E && restoredControls.Controller[0] == Buttons.X,
                "Gameplay keyboard and controller remaps survive an atomic profile reopen");
            Set(data, "RainStrength", 0); Save(fresh);
            check((int)Get(Data(Open()), "RainStrength") == 0, "An explicit Rain Off preference remains Off when automatic weather is available");
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            legacy.Remove("Vignette");
            legacy.Remove("PixelLighting");
            legacy.Remove("SunShaftStrength");
            legacy.Remove("CloudShadowStrength");
            legacy.Remove("RainStrength");
            legacy.Remove("ScreenShake"); legacy.Remove("AutoBattle");
            legacy.Remove("Controls");
            legacy.Remove("TouchSize"); legacy.Remove("TouchOpacity");
            legacy.Remove("Music"); legacy.Remove("MusicVolume"); legacy.Remove("EffectsVolume");
            legacy.Remove("TimeOfDayMode"); legacy.Remove("DayNightSeconds");
            File.WriteAllText(path, legacy.ToJsonString());
            var legacyBytes = File.ReadAllBytes(path);
            reopened = Data(Open());
            check((bool)Get(reopened, "Music") && (float)Get(reopened, "MusicVolume") == .5f
                && !(bool)Get(reopened, "Sound") && (float)Get(reopened, "Volume") == .4f && (float)Get(reopened, "EffectsVolume") == 1f,
                "Legacy profiles receive music defaults while retaining their existing sound preferences");
            check((int)Get(reopened, "TimeOfDayMode") == 0 && (double)Get(reopened, "DayNightSeconds") == 300,
                "Legacy profiles enable the automatic cycle without modifying their file");
            check(!(bool)Get(reopened, "Vignette") && (bool)Get(reopened, "PixelLighting") && (int)Get(reopened, "Miniature") == 1
                && (int)Get(reopened, "SunShaftStrength") == 1
                && (int)Get(reopened, "CloudShadowStrength") == 1 && (int)Get(reopened, "RainStrength") == 1 && !(bool)Get(reopened, "AutoBattle") && !(bool)Get(reopened, "ScreenShake")
                && ((HighScoreEntry[])Get(reopened, "Scores"))[21].Name == "Profile Hero"
                && legacyBytes.SequenceEqual(File.ReadAllBytes(path)),
                "Existing profiles retain preferences and scores, with vignette off, pixel lighting on, automatic rain and subtle sunlight/cloud effects");
            check(((ControlBindings)Get(reopened, "Controls")).Keyboard[4] == Keys.Enter
                && ((ControlBindings)Get(reopened, "Controls")).Controller[0] == Buttons.A,
                "Profiles without bindings migrate to standard controls without rewriting their file");
            check((float)Get(reopened, "TouchSize") == 1f && (float)Get(reopened, "TouchOpacity") == .55f,
                "Existing profiles receive touch defaults without migration writes");
            foreach (var invalid in new[] { "{", "{\"Version\":99}", "{\"Version\":1,\"Volume\":9}", "{\"Version\":1,\"Scores\":[]}",
                "{\"TouchSize\":0}", "{\"TouchSize\":2}", "{\"TouchOpacity\":1e999}", "{\"TouchOpacity\":0}",
                "{\"Controls\":null}", "{\"Controls\":{\"Keyboard\":[]}}", "{\"Controls\":{\"Controller\":[1,1,1,1]}}",
                "{\"SunShaftStrength\":-1}", "{\"SunShaftStrength\":3}", "{\"CloudShadowStrength\":-1}", "{\"CloudShadowStrength\":3}", "{\"RainStrength\":-1}", "{\"RainStrength\":3}",
                "{\"TimeOfDayMode\":-1}", "{\"TimeOfDayMode\":4}", "{\"DayNightSeconds\":-1}", "{\"DayNightSeconds\":1200}", "{\"DayNightSeconds\":1e999}",
                "{\"MusicVolume\":-1}", "{\"MusicVolume\":2}", "{\"MusicVolume\":1e999}", "{\"EffectsVolume\":-1}", "{\"EffectsVolume\":2}", "{\"EffectsVolume\":1e999}" })
            {
                File.WriteAllText(path, invalid);
                var broken = Open(); var rejected = false;
                try { Save(broken); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { rejected = true; }
                check(rejected && File.ReadAllText(path) == invalid && (int)Get(Data(broken), "Miniature") == 2,
                    "Invalid/newer profile falls back for this session and cannot overwrite the original file");
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
