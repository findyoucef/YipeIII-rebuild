using System.Text.Json;
using System.Text.Json.Serialization;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed class ProfileData
{
    public int Version { get; set; } = 1;
    public bool Sound { get; set; } = true;
    public float Volume { get; set; } = .65f;
    public bool Scanlines { get; set; } = true;
    public int Miniature { get; set; } = 2;
    public bool Vignette { get; set; }
    public bool PixelLighting { get; set; } = true;
    public int SunShaftStrength { get; set; } = 1;
    public int CloudShadowStrength { get; set; } = 1;
    public int RainStrength { get; set; } = 1;
    public bool ScreenShake { get; set; }
    public bool AutoBattle { get; set; }
    public int TimeOfDayMode { get; set; }
    public double DayNightSeconds { get; set; } = DayNightCycle.OpeningTime;
    public HighScoreEntry[] Scores { get; set; } = HighScores.Create();

    public ControlBindings Controls { get; set; } = new();
}

[JsonSerializable(typeof(ProfileData))]
internal partial class ProfileJsonContext : JsonSerializerContext;

/// <summary>Desktop preferences and score tables, separate from adventure saves.</summary>
internal sealed class DesktopProfile
{
    private readonly string path;
    public ProfileData Data { get; private set; } = new();
    public string LoadError { get; private set; } = "";
    public DesktopProfile(string path)
    {
        this.path = Path.GetFullPath(path);
        try
        {
            if (!File.Exists(path)) return;
            using var file = File.OpenRead(path);
            if (file.Length > 128 * 1024) throw new InvalidDataException("The profile is too large.");
            var data = JsonSerializer.Deserialize(file, ProfileJsonContext.Default.ProfileData);
            if (data is null || data.Version != 1 || !float.IsFinite(data.Volume) || data.Volume is < 0 or > 1
                || data.Miniature is < 0 or > 2 || data.SunShaftStrength is < 0 or > 2
                || data.CloudShadowStrength is < 0 or > 2 || data.RainStrength is < 0 or > 2
                || data.TimeOfDayMode is < 0 or > 3 || !double.IsFinite(data.DayNightSeconds)
                || data.DayNightSeconds is < 0 or >= DayNightCycle.Duration
                || data.Scores is null || data.Scores.Length != 45
                || data.Scores.Any(score => score is null || score.Name is null || score.Name.Length > 1024)
                || data.Controls is null || data.Controls.ValidationError().Length != 0)
                throw new InvalidDataException("The profile is invalid or from another version.");
            Data = data;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            LoadError = "Could not open preferences and scores. The existing file is preserved. " + error.Message;
            Console.Error.WriteLine(LoadError);
        }
    }
    public void Save()
    {
        // Never silently replace an unreadable or newer profile with default values.
        if (LoadError.Length != 0) throw new InvalidDataException(LoadError);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, Data, ProfileJsonContext.Default.ProfileData);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
