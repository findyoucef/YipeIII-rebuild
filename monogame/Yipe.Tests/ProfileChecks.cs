using System.Reflection;
using Yipe.Core;
using Yipe.Desktop;

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
            check((bool)Get(data, "Scanlines") && (int)Get(data, "Miniature") == 2 && (bool)Get(data, "Sound")
                && !(bool)Get(data, "Vignette") && (bool)Get(data, "PixelLighting") && !File.Exists(path), "Missing desktop profile uses the approved defaults without writing a file");
            Set(data, "Scanlines", false); Set(data, "Miniature", 1); Set(data, "Sound", false); Set(data, "Volume", .4f);
            Set(data, "Vignette", true);
            Set(data, "PixelLighting", false);
            HighScores.Update((HighScoreEntry[])Get(data, "Scores"), new Player { Name = "Profile Hero", Gold = 12345 }, true);
            Save(fresh);
            var reopened = Data(Open());
            check(!(bool)Get(reopened, "Scanlines") && (int)Get(reopened, "Miniature") == 1 && !(bool)Get(reopened, "Sound")
                && (bool)Get(reopened, "Vignette") && !(bool)Get(reopened, "PixelLighting") && (float)Get(reopened, "Volume") == .4f && ((HighScoreEntry[])Get(reopened, "Scores"))[21] == new HighScoreEntry("Profile Hero", 12345)
                && ((HighScoreEntry[])Get(reopened, "Scores"))[27].Name == "Profile Hero",
                "Desktop profile atomically reopens preferences and all score categories together");
            check(Directory.GetFiles(directory).Length == 1, "Successful profile writes leave no staging files");
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            legacy.Remove("Vignette");
            legacy.Remove("PixelLighting");
            File.WriteAllText(path, legacy.ToJsonString());
            var legacyBytes = File.ReadAllBytes(path);
            reopened = Data(Open());
            check(!(bool)Get(reopened, "Vignette") && (bool)Get(reopened, "PixelLighting") && (int)Get(reopened, "Miniature") == 1
                && ((HighScoreEntry[])Get(reopened, "Scores"))[21].Name == "Profile Hero"
                && legacyBytes.SequenceEqual(File.ReadAllBytes(path)),
                "Existing profiles retain preferences and scores, with vignette off and pixel lighting on");
            foreach (var invalid in new[] { "{", "{\"Version\":99}", "{\"Version\":1,\"Volume\":9}", "{\"Version\":1,\"Scores\":[]}" })
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
