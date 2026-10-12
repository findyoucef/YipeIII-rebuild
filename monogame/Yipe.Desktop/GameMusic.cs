using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Yipe.Desktop;

internal enum MusicTrack { None, Town, Overworld, Cave, CrystalCave, Battle }

/// <summary>Streams the soundtrack independently of effects, ambience and gameplay time.</summary>
internal sealed class GameMusic : IDisposable
{
    private Song? town, overworld, cave, crystalCave, battle, current;
    private readonly Dictionary<Song, TimeSpan> positions = new();
    private float gain;
    public string Error { get; private set; } = "";

    public GameMusic(string content)
    {
        try
        {
            var directory = Path.GetFullPath(Path.Combine(content, "Audio", "Music"));
            town = Song.FromUri("Woodland Fantasy", new Uri(Path.Combine(directory, "woodland-fantasy.ogg")));
            overworld = Song.FromUri("Celtic Impulse", new Uri(Path.Combine(directory, "celtic-impulse.ogg")));
            cave = Song.FromUri("Cave Theme", new Uri(Path.Combine(directory, "cave-theme.ogg")));
            crystalCave = Song.FromUri("Crystal Caves", new Uri(Path.Combine(directory, "crystal-caves.ogg")));
            battle = Song.FromUri("Battle Theme A", new Uri(Path.Combine(directory, "battle-theme-a.ogg")));
        }
        catch (Exception error) when (error is NoAudioHardwareException or IOException or InvalidOperationException or NotSupportedException)
        { Fail(error); }
    }

    public void Update(MusicTrack track, bool active, float volume, float intensity, double elapsed)
    {
        if (Error.Length != 0) return;
        try
        {
            if (!active || volume <= 0)
            {
                if (current is not null) { MediaPlayer.Volume = 0; MediaPlayer.Pause(); }
                gain = 0;
                return;
            }

            var wanted = track switch
            {
                MusicTrack.Town => town, MusicTrack.Overworld => overworld,
                MusicTrack.Cave => cave, MusicTrack.CrystalCave => crystalCave,
                MusicTrack.Battle => battle, _ => null,
            };
            // Combat should react promptly; exploration changes can breathe.
            var step = (float)Math.Clamp(elapsed, 0, .25) / (track == MusicTrack.Battle ? .2f : 1.2f);
            if (!ReferenceEquals(wanted, current))
            {
                gain = Math.Max(0, gain - step);
                MediaPlayer.Volume = Math.Clamp(volume, 0, 1) * gain;
                if (gain > 0) return;
                if (wanted is null)
                {
                    MediaPlayer.Pause();
                    return;
                }
                if (current is not null && current != battle)
                    positions[current] = MediaPlayer.PlayPosition;
                current = wanted;
                MediaPlayer.IsRepeating = true;
                // A fresh battle gets its opening cue. Exploration returns where it left off.
                var position = positions.GetValueOrDefault(current);
                if (position > TimeSpan.Zero && position < current.Duration) MediaPlayer.Play(current, position);
                else MediaPlayer.Play(current);
            }
            if (current is null) return;
            gain += Math.Clamp(Math.Clamp(intensity, 0, 1) - gain, -step, step);
            MediaPlayer.Volume = Math.Clamp(volume, 0, 1) * gain;
            if (MediaPlayer.State == MediaState.Paused) MediaPlayer.Resume();
        }
        catch (Exception error) when (error is NoAudioHardwareException or IOException or InvalidOperationException or NotSupportedException)
        { Fail(error); }
    }

    private void Fail(Exception error)
    {
        Error = "Music is unavailable. Other game sounds remain enabled.";
        Console.Error.WriteLine($"{Error} {error.Message}");
        Dispose();
    }

    public void Dispose()
    {
        if (current is not null) MediaPlayer.Stop();
        current = null; gain = 0;
        town?.Dispose(); town = null;
        overworld?.Dispose(); overworld = null;
        cave?.Dispose(); cave = null;
        crystalCave?.Dispose(); crystalCave = null;
        battle?.Dispose(); battle = null;
        positions.Clear();
    }
}
