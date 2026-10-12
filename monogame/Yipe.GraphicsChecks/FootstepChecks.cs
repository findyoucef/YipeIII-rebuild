using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class FootstepChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "paused", "overlay", "mapOpen", "saving", "loading", "studioSplashRemaining", "sceneTime",
            "dayNightSeconds", "dayNightUpdatedAt", "outdoorWeatherTime", "tickRemaining", "walkingStep", "cameraPosition", "pendingDirection" }
            .ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position; var oldFacing = player.FacingRow;
        var session = (GameSession)Get("session")!; var catalog = (MapCatalog)Get("catalog")!;
        var snapshot = GameSaveCodec.Encode(session, catalog);
        var audio = Get("audio")!; var audioType = audio.GetType();
        var enabled = (bool)audioType.GetProperty("Enabled")!.GetValue(audio)!;
        var volume = (float)audioType.GetProperty("Volume")!.GetValue(audio)!;
        var stepField = audioType.GetField("footstep", flags)!;
        var voices = (List<SoundEffectInstance>)audioType.GetField("voices", flags)!.GetValue(audio)!;
        var indexField = audioType.GetField("footstepIndex", flags)!;
        int Count() => (int)indexField.GetValue(audio)!;
        var surfaceType = typeof(GameInput).Assembly.GetType("Yipe.Desktop.FootstepSurface")!;
        void Sound(string surface) => audioType.GetMethod("Footstep")!.Invoke(audio, [Enum.Parse(surfaceType, surface)]);
        void Stop() => audioType.GetMethod("Update")!.Invoke(audio, [false]);
        var directory = Path.Combine(output, "footsteps"); Directory.CreateDirectory(directory);
        try
        {
            check((string)audioType.GetProperty("Error")!.GetValue(audio)! == "", "Original audio and all recorded footstep assets initialize on the real audio device");
            audioType.GetProperty("Enabled")!.SetValue(audio, true); audioType.GetProperty("Volume")!.SetValue(audio, .4f); Stop();
            foreach (var surface in Enum.GetNames(surfaceType))
            {
                Sound(surface); var voice = (SoundEffectInstance)stepField.GetValue(audio)!;
                check(voice.State == SoundState.Playing && Math.Abs(voice.Volume - .112f) < .00001,
                    $"{surface} footsteps play through their own quiet voice at master volume");
                Stop(); check(voice.IsDisposed && stepField.GetValue(audio) is null, $"{surface} voice stops and releases on focus loss");
            }
            Sound("Grass"); var previous = (SoundEffectInstance)stepField.GetValue(audio)!;
            audioType.GetMethod("Play")!.Invoke(audio, [131, false]); var important = voices[^1];
            check(previous.IsDisposed && stepField.GetValue(audio) is null && important.State == SoundState.Playing,
                "A door effect immediately replaces only the footstep voice, preserving original-effect priority");
            var before = Count(); Sound("Stone");
            check(Count() == before && stepField.GetValue(audio) is null && ReferenceEquals(important, voices.LastOrDefault()) && !important.IsDisposed,
                "Walking cannot interrupt or queue footsteps behind a playing important effect");
            Stop(); audioType.GetProperty("Enabled")!.SetValue(audio, false); Sound("Grass");
            check(Count() == before && stepField.GetValue(audio) is null, "Sound Off suppresses footsteps without advancing their variation sequence");
            audioType.GetProperty("Enabled")!.SetValue(audio, true); Sound("Sand");
            audioType.GetProperty("Volume")!.SetValue(audio, 0f); audioType.GetMethod("Update")!.Invoke(audio, [true]);
            check(stepField.GetValue(audio) is null, "Master volume zero stops the current footstep");
            audioType.GetProperty("Volume")!.SetValue(audio, .4f);

            var town = Get("town")!; var townSample = town.GetType().GetMethod("FootstepAt", flags)!;
            string Town(MapPosition position) => townSample.Invoke(town, [position])!.ToString()!;
            var template = catalog.CreateMap(17)!;
            var cells = (from x in Enumerable.Range(0, Map.Width) from y in Enumerable.Range(0, Map.Height)
                select new MapPosition(x, y)).ToArray();
            var paved = cells.First(cell => template.Tile(cell) == 8);
            var bridge = cells.First(cell => template.Tile(cell) == 0x0b);
            check(Town(new(5, 5)) == "Grass" && Town(bridge) == "Wood" && Town(paved) == "Stone",
                "Town grass, visible bridge decks and paved paths select their visible materials");
            var world = Get("world")!; var worldSample = world.GetType().GetMethod("FootstepAt", flags)!;
            var map = MakeMap(1); var position = new MapPosition(5, 5);
            string Surface(Map source) => worldSample.Invoke(world, [source, position])!.ToString()!;
            void Fill(ushort tile) { for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++) map.RestoreTile(new(x, y), tile); }
            Fill(2); check(Surface(map) == "Grass", "Outdoor bare ground uses the soft grass/dirt contact");
            map.RestoreTile(position, 0x16); check(Surface(map) == "Stone", "Authored stone roads override surrounding soil");
            Fill(0x2d); check(Surface(map) == "Sand", "The compiled visible sand footprint produces a sand scuff");
            Fill(0x0d); check(Surface(map) == "Water", "Actual water under the planted feet produces a splash");
            map.RestoreTile(position, 0x0b); check(Surface(map) == "Wood", "A wooden deck overrides the water beneath its feet");
            Fill(2); map.RestoreTile(position, 0x11);
            check(Surface(map) == "Grass", "The dry bank of a river tile stays dry at the actual foot pixel");
            map.RestoreTile(position, 0x59); check(Surface(map) == "Water", "Waterproof walking on visible water is a splash, not a hidden bridge sound");
            var cave = MakeMap(57); cave.RestoreTile(position, 0x22); var wall = Surface(cave);
            cave.RestoreTile(position, 0x43); check(Surface(cave) == wall, "A secret cave-wall variant retains the audible material of its visible wall");

            Set("paused", false); Set("overlay", null); Set("mapOpen", false);
            foreach (var fps in new[] { 24, 60 })
            {
                player.SnapTo(new(5, 5)); player.MoveTo(new(6, 5)); Set("walkingStep", true); before = Count();
                for (var i = 0; i < fps && player.IsMoving; i++) Update(1d / fps);
                check(!player.IsMoving && Count() == before + 1, $"Walking at {fps} updates per second emits exactly one planted contact");
                Update(.1); check(Count() == before + 1, "Idle updates never repeat the last contact"); Stop();
            }
            player.SnapTo(new(5, 4)); player.MoveTo(new(6, 5)); Set("walkingStep", true); before = Count(); Update(.25);
            check(Count() == before + 1 && !player.IsMoving, "A diagonal walking step has one contact and keeps its 250 ms travel"); Stop();
            player.SnapTo(new(5, 5)); player.MoveTo(new(6, 5)); Set("walkingStep", false); before = Count(); Update(.25);
            check(Count() == before, "Passive movement produces no walking contact");
            player.SnapTo(new(5, 5)); player.MoveTo(new(6, 5)); Set("walkingStep", true); before = Count(); Update(.1);
            Set("paused", true); Update(.3); Set("paused", false); Update(.15);
            check(Count() == before + 1 && !player.IsMoving, "Pause freezes an unfinished walk and resume produces its single eventual contact"); Stop();
            Sound("Grass"); Set("paused", true); Update(.1);
            check(stepField.GetValue(audio) is null, "Pause immediately clears a playing footstep"); Set("paused", false);
            Sound("Grass"); Set("mapOpen", true); Update(.1);
            check(stepField.GetValue(audio) is null, "Map viewing clears footsteps"); Set("mapOpen", false);
            Sound("Grass"); Call("ShowActions"); Update(.1);
            check(stepField.GetValue(audio) is null, "Opening action panels clears footsteps"); Set("overlay", null);
            player.SnapTo(new(5, 5)); player.MoveTo(new(6, 5)); Set("walkingStep", true);
            Call("RestorePosition", oldPosition); before = Count(); Update(.25);
            check(Count() == before && !(bool)Get("walkingStep")!, "A snap/restore cancels its old pending contact");
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(session, catalog)), "Footstep timing and playback preserve the complete saved adventure and RNG");

            var sounds = Path.Combine(AppContext.BaseDirectory, "Content", "Audio", "Footsteps");
            var mix = new short[22050 * (Enum.GetValues(surfaceType).Length * 5 / 2 + 1)]; var offset = 0;
            foreach (var surface in Enum.GetValues(surfaceType))
            {
                var variants = new byte[3][];
                for (var variant = 0; variant < 3; variant++)
                {
                    var file = $"{surface.ToString()!.ToLowerInvariant()}-{variant}.wav";
                    File.Copy(Path.Combine(sounds, file), Path.Combine(directory, file));
                    variants[variant] = File.ReadAllBytes(Path.Combine(sounds, file))[44..];
                }
                for (var contact = 0; contact < 8; contact++)
                {
                    var pcm = variants[contact % 3];
                    for (var i = 0; i < pcm.Length / 2; i++) mix[offset + i] = (short)(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2, 2)) * .65 * .28);
                    offset += 22050 / 4;
                }
                offset += 22050 / 2;
            }
            var mixed = new byte[mix.Length * 2]; Buffer.BlockCopy(mix, 0, mixed, 0, mixed.Length);
            WriteWave(Path.Combine(directory, "walking-surfaces.wav"), mixed);
        }
        finally
        {
            Stop(); audioType.GetProperty("Enabled")!.SetValue(audio, enabled); audioType.GetProperty("Volume")!.SetValue(audio, volume);
            foreach (var item in saved) Set(item.Key, item.Value); player.SnapTo(oldPosition);
            player.Face(oldFacing switch { 1 => new(-1, 0), 2 => new(1, 0), 3 => new(0, -1), _ => new(0, 1) });
        }
        void Update(double elapsed)
        {
            Set("tickRemaining", 100d); Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(elapsed)));
        }
        Map MakeMap(int id)
        {
            var layer = (id - 1) / 40; var cell = id - 1 - layer * 40;
            return new GameSession(catalog, location: new() { MapId = id, MapLayer = layer,
                RegionX = cell % 6 - 4, RegionY = 2 - cell / 6, Position = new(5, 5) }).CurrentMap;
        }
        static void WriteWave(string path, byte[] pcm)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write("RIFF"u8); writer.Write(pcm.Length + 36); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(44100);
            writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(pcm.Length); writer.Write(pcm);
        }
    }
}
