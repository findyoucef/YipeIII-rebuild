using Microsoft.Xna.Framework.Audio;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>Original effects and interruption behavior. Playback never controls simulation time or RNG.</summary>
internal sealed class GameAudio : IDisposable
{
    private readonly Dictionary<int, SoundEffect> effects = new();
    private readonly Dictionary<int, SoundEffect> monsters = new();
    private SoundEffectInstance? current;
    public bool Enabled { get; set; } = true;
    public float Volume { get; set; } = .65f;
    public string Error { get; private set; } = "";

    public GameAudio(string content)
    {
        try
        {
            foreach (var (id, file) in AudioCatalog.Effects)
            {
                using var stream = File.OpenRead(Path.Combine(content, "Audio", "Effects", file));
                effects.Add(id, SoundEffect.FromStream(stream));
            }
            foreach (var path in Directory.EnumerateFiles(Path.Combine(content, "Audio", "Monsters"), "*.wav"))
            {
                using var stream = File.OpenRead(path);
                monsters.Add(int.Parse(Path.GetFileNameWithoutExtension(path)), SoundEffect.FromStream(stream));
            }
        }
        catch (Exception error) when (error is NoAudioHardwareException or IOException or InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            Error = "Sound could not be initialized. The game will continue silently.";
            Console.Error.WriteLine($"{Error} {error.Message}");
            Dispose();
        }
    }
    public void Play(int id)
    {
        if (id != 0 && effects.TryGetValue(id, out var sound)) PlayClip(sound);
    }
    public void MonsterDeath(int type)
    {
        if (monsters.TryGetValue(AudioCatalog.MonsterFile(type), out var sound)) PlayClip(sound);
        else Play(12);
    }
    private void PlayClip(SoundEffect sound)
    {
        if (!Enabled || Error.Length != 0) return;
        // The original clears queued audio each time a new effect is played.
        current?.Stop(); current?.Dispose();
        current = sound.CreateInstance(); current.Volume = Volume; current.Play();
    }
    public void Update(bool active)
    {
        if (!Enabled || !active) current?.Stop();
        if (current?.State == SoundState.Stopped) { current.Dispose(); current = null; }
        if (current is not null) current.Volume = Volume;
    }
    public void Dispose()
    {
        current?.Dispose(); current = null;
        foreach (var sound in effects.Values) sound.Dispose();
        foreach (var sound in monsters.Values) sound.Dispose();
        effects.Clear(); monsters.Clear();
    }
}
