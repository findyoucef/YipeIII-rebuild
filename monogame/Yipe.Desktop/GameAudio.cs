using Microsoft.Xna.Framework.Audio;
using Yipe.Core;

namespace Yipe.Desktop;

internal enum FootstepSurface { Grass, Stone, Sand, Wood, Water, Snow }

/// <summary>Original effects with bounded combat overlap. Playback never controls simulation time or RNG.</summary>
internal sealed class GameAudio : IDisposable
{
    private readonly Dictionary<int, SoundEffect> effects = new();
    private readonly Dictionary<int, SoundEffect> monsters = new();
    private readonly List<SoundEffectInstance> voices = new();
    private bool EffectPlaying => voices.Any(voice => voice.State == SoundState.Playing);
    private readonly Dictionary<FootstepSurface, SoundEffect[]> footsteps = new();
    private SoundEffectInstance? footstep;
    private int footstepIndex;
    private SoundEffect? caveAmbience;
    private SoundEffect? fireAmbience, ambienceTrack;
    private SoundEffect? earthAmbience, deepEarthAmbience;
    private SoundEffect? hellAmbience;
    private SoundEffect? iceAmbience;
    private SoundEffect? waterCaveAmbience, finalAmbience;
    private SoundEffect? townAmbience, townNightAmbience, riverSound;
    private SoundEffectInstance? river;
    private float riverGain;
    private SoundEffectInstance? ambience;
    private float ambienceGain;
    private SoundEffect? outdoorBirdSound, outdoorNightSound;
    private SoundEffectInstance? outdoorBirds, outdoorNight;
    private float outdoorBirdGain, outdoorNightGain;
    public bool Enabled { get; set; } = true;
    public float Volume { get; set; } = .65f;
    public float EffectsVolume { get; set; } = 1f;
    private float OutputVolume => Volume * EffectsVolume;
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
            foreach (var surface in Enum.GetValues<FootstepSurface>())
            {
                var variants = new SoundEffect[3];
                footsteps.Add(surface, variants);
                for (var variant = 0; variant < variants.Length; variant++)
                {
                    using var stream = File.OpenRead(Path.Combine(content, "Audio", "Footsteps",
                        $"{surface.ToString().ToLowerInvariant()}-{variant}.wav"));
                    variants[variant] = SoundEffect.FromStream(stream);
                }
            }
            using var ambienceStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "breeding-ground.wav"));
            caveAmbience = SoundEffect.FromStream(ambienceStream);
            using var fireStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "fire-cave.wav"));
            fireAmbience = SoundEffect.FromStream(fireStream);
            using var earthStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "earth-cave.wav"));
            earthAmbience = SoundEffect.FromStream(earthStream);
            using var deepEarthStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "earth-cave-deep.wav"));
            deepEarthAmbience = SoundEffect.FromStream(deepEarthStream);
            using var hellStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "hell.wav"));
            hellAmbience = SoundEffect.FromStream(hellStream);
            using var iceStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "ice-cave.wav"));
            iceAmbience = SoundEffect.FromStream(iceStream);
            using var waterCaveStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "water-cave.wav"));
            waterCaveAmbience = SoundEffect.FromStream(waterCaveStream);
            using var finalStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "final-cave.wav"));
            finalAmbience = SoundEffect.FromStream(finalStream);
            using var townStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "maynard-day.wav"));
            townAmbience = SoundEffect.FromStream(townStream);
            using var nightStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "maynard-night.wav"));
            townNightAmbience = SoundEffect.FromStream(nightStream);
            using var riverStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "maynard-river.wav"));
            riverSound = SoundEffect.FromStream(riverStream);
            using var birdStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "outdoor-birds.wav"));
            outdoorBirdSound = SoundEffect.FromStream(birdStream);
            using var cricketStream = File.OpenRead(Path.Combine(content, "Audio", "Ambience", "outdoor-night.wav"));
            outdoorNightSound = SoundEffect.FromStream(cricketStream);
        }
        catch (Exception error) when (error is NoAudioHardwareException or IOException or InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            Error = "Sound could not be initialized. The game will continue silently.";
            Console.Error.WriteLine($"{Error} {error.Message}");
            Dispose();
        }
    }
    public void Play(int id, bool overlap = false)
    {
        if (id != 0 && effects.TryGetValue(id, out var sound)) PlayClip(sound, overlap);
    }
    public void MonsterDeath(int type)
    {
        if (monsters.TryGetValue(AudioCatalog.MonsterFile(type), out var sound)) PlayClip(sound, overlap: true);
        else Play(12, overlap: true);
    }
    private void PlayClip(SoundEffect sound, bool overlap)
    {
        if (!Enabled || OutputVolume <= 0 || Error.Length != 0) return;
        StopFootsteps();
        UpdateEffects(overlap);
        // Keep combat responsive without cutting off every preceding impact or building a backlog.
        if (voices.Count == 4) { voices[0].Dispose(); voices.RemoveAt(0); }
        var voice = sound.CreateInstance();
        voice.Volume = OutputVolume; voice.Play(); voices.Add(voice);
    }
    public void Footstep(FootstepSurface surface)
    {
        if (!Enabled || OutputVolume <= 0 || Error.Length != 0 || EffectPlaying) return;
        StopFootsteps();
        var variant = footstepIndex++ % 3;
        footstep = footsteps[surface][variant].CreateInstance();
        footstep.Volume = OutputVolume * .28f;
        footstep.Play();
    }
    public void StopFootsteps()
    {
        footstep?.Stop(); footstep?.Dispose(); footstep = null;
    }
    public void Update(bool active)
    {
        if (!Enabled || !active || OutputVolume <= 0) { ambience?.Pause(); river?.Pause(); outdoorBirds?.Pause(); outdoorNight?.Pause(); }
        UpdateEffects(active);
        if (!Enabled || !active || OutputVolume <= 0 || footstep?.State == SoundState.Stopped) StopFootsteps();
        if (footstep is not null) footstep.Volume = OutputVolume * .28f;
    }
    private void UpdateEffects(bool active)
    {
        for (var index = voices.Count - 1; index >= 0; index--)
        {
            var voice = voices[index];
            if (!Enabled || !active || OutputVolume <= 0 || voice.State == SoundState.Stopped)
            {
                voice.Dispose(); voices.RemoveAt(index);
            }
            else voice.Volume = OutputVolume;
        }
    }
    public void UpdateAmbience(bool inBreedingGrounds, bool playing, double elapsed)
        => UpdateAmbientTrack(inBreedingGrounds ? caveAmbience : null, playing, elapsed, .18f);

    public void UpdateFireAmbience(float intensity, bool playing, double elapsed)
        => UpdateAmbientTrack(fireAmbience, playing, elapsed, .24f * Math.Clamp(intensity, 0, 1.2f));

    public void UpdateEarthAmbience(bool deep, bool playing, double elapsed)
        => UpdateAmbientTrack(deep ? deepEarthAmbience : earthAmbience, playing, elapsed, .22f);

    public void UpdateStoneAmbience(bool flooded, bool playing, double elapsed)
        => UpdateAmbientTrack(flooded ? deepEarthAmbience : caveAmbience, playing, elapsed, .15f);

    public void UpdateHellAmbience(bool playing, double elapsed)
        => UpdateAmbientTrack(hellAmbience, playing, elapsed, .24f);

    public void UpdateIceAmbience(bool deep, bool playing, double elapsed)
        => UpdateAmbientTrack(iceAmbience, playing, elapsed, deep ? .16f : .18f);

    public void UpdateWaterCaveAmbience(float wetness, bool playing, double elapsed)
        => UpdateAmbientTrack(waterCaveAmbience, playing, elapsed, .13f + .07f * Math.Clamp(wetness, 0, 1));

    public void UpdateFinalAmbience(bool playing, double elapsed)
        => UpdateAmbientTrack(finalAmbience, playing, elapsed, .19f);

    public void UpdateTownAmbience(float daylight, bool playing, double elapsed)
        => UpdateAmbientTrack(daylight < .15f ? townNightAmbience : townAmbience, playing, elapsed,
            daylight < .15f ? .13f : .18f * (.3f + .7f * daylight));

    public void UpdateRiverAmbience(float intensity, float pan, bool playing, double elapsed)
    {
        if (Error.Length != 0 || riverSound is null) { StopRiver(); return; }
        if (!Enabled || !playing || OutputVolume <= 0) { river?.Pause(); return; }
        var blend = 1 - MathF.Exp(-(float)Math.Clamp(elapsed, 0, 1) * 4);
        riverGain += (Math.Clamp(intensity, 0, 1) - riverGain) * blend;
        if (intensity <= 0 && riverGain < .001f) { StopRiver(); return; }
        if (river is null) { river = riverSound.CreateInstance(); river.IsLooped = true; }
        river.Volume = OutputVolume * .24f * riverGain * (EffectPlaying ? .4f : 1);
        river.Pan += (Math.Clamp(pan, -.65f, .65f) - river.Pan) * blend;
        if (river.State == SoundState.Paused) river.Resume();
        else if (river.State == SoundState.Stopped) river.Play();
    }

    public void StopRiver()
    {
        river?.Stop(); river?.Dispose(); river = null; riverGain = 0;
    }

    public void UpdateOutdoorAmbience(float dry, float cold, float wood, float daylight, float rain, bool playing, double elapsed)
    {
        var wind = .10f + dry * .04f + cold * .07f + rain * .03f - wood * .025f;
        UpdateAmbientTrack(townNightAmbience, playing, elapsed, wind);
        var birds = (.12f + wood * .025f) * daylight * (1 - dry * .96f) * (1 - cold) * (1 - rain * .85f);
        var insects = .075f * (1 - daylight) * (1 - dry * .8f) * (1 - cold) * (1 - rain * .85f);
        UpdateOutdoorVoice(ref outdoorBirds, outdoorBirdSound, ref outdoorBirdGain, birds, playing, elapsed);
        UpdateOutdoorVoice(ref outdoorNight, outdoorNightSound, ref outdoorNightGain, insects, playing, elapsed);
    }

    private void UpdateOutdoorVoice(ref SoundEffectInstance? voice, SoundEffect? sound, ref float gain,
        float target, bool playing, double elapsed)
    {
        if (Error.Length != 0 || sound is null) { voice?.Dispose(); voice = null; gain = 0; return; }
        if (!Enabled || !playing || OutputVolume <= 0) { voice?.Pause(); return; }
        gain += (target - gain) * (1 - MathF.Exp(-(float)Math.Clamp(elapsed, 0, 1) * 2));
        if (target <= .001f && gain < .001f) { voice?.Dispose(); voice = null; gain = 0; return; }
        if (voice is null) { voice = sound.CreateInstance(); voice.IsLooped = true; }
        voice.Volume = OutputVolume * gain * (EffectPlaying ? .4f : 1);
        if (voice.State == SoundState.Paused) voice.Resume();
        else if (voice.State == SoundState.Stopped) voice.Play();
    }

    public void StopOutdoorNature()
    {
        outdoorBirds?.Stop(); outdoorBirds?.Dispose(); outdoorBirds = null; outdoorBirdGain = 0;
        outdoorNight?.Stop(); outdoorNight?.Dispose(); outdoorNight = null; outdoorNightGain = 0;
    }

    private void UpdateAmbientTrack(SoundEffect? track, bool playing, double elapsed, float volume)
    {
        if (!ReferenceEquals(track, ambienceTrack) || Error.Length != 0 || track is null)
        {
            ambience?.Stop(); ambience?.Dispose(); ambience = null; ambienceGain = 0;
            ambienceTrack = track;
        }
        if (track is null || Error.Length != 0) return;
        if (!Enabled || !playing || OutputVolume <= 0) { ambience?.Pause(); return; }
        if (ambience is null)
        {
            ambience = track.CreateInstance(); ambience.IsLooped = true;
        }
        ambienceGain = Math.Min(1, ambienceGain + (float)Math.Clamp(elapsed, 0, 1) / .8f);
        ambience.Volume = OutputVolume * volume * ambienceGain * (EffectPlaying ? .4f : 1);
        if (ambience.State == SoundState.Paused) ambience.Resume();
        else if (ambience.State == SoundState.Stopped) ambience.Play();
    }
    public void Dispose()
    {
        UpdateEffects(false);
        StopFootsteps();
        ambience?.Dispose(); ambience = null;
        caveAmbience?.Dispose(); caveAmbience = null;
        fireAmbience?.Dispose(); fireAmbience = null; ambienceTrack = null;
        earthAmbience?.Dispose(); earthAmbience = null;
        deepEarthAmbience?.Dispose(); deepEarthAmbience = null;
        hellAmbience?.Dispose(); hellAmbience = null;
        iceAmbience?.Dispose(); iceAmbience = null;
        waterCaveAmbience?.Dispose(); waterCaveAmbience = null;
        finalAmbience?.Dispose(); finalAmbience = null;
        StopRiver();
        townAmbience?.Dispose(); townAmbience = null;
        townNightAmbience?.Dispose(); townNightAmbience = null;
        riverSound?.Dispose(); riverSound = null;
        StopOutdoorNature(); outdoorBirdSound?.Dispose(); outdoorBirdSound = null;
        outdoorNightSound?.Dispose(); outdoorNightSound = null;
        foreach (var sound in effects.Values) sound.Dispose();
        foreach (var sound in monsters.Values) sound.Dispose();
        effects.Clear(); monsters.Clear();
        foreach (var variants in footsteps.Values) foreach (var sound in variants) sound?.Dispose();
        footsteps.Clear();
    }
}
