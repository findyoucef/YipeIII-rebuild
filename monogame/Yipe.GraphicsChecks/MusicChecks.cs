using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class MusicChecks
{
    internal static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        var saved = new[] { "session", "adventure", "titleScreen", "studioSplashRemaining", "paused", "overlay", "mapOpen",
            "pausePage", "selected", "selectionY", "pauseAmount", "menuMessage", "soundMenuParent" }.ToDictionary(n => n, Get);
        var catalog = (MapCatalog)Get("catalog")!;
        var before = GameSaveCodec.Encode((GameSession)Get("session")!, catalog);
        var audio = Get("audio")!;
        var data = Get("profile")!.GetType().GetProperty("Data")!.GetValue(Get("profile"))!;
        object Property(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o)!;
        void PropertySet(object o, string name, object value) => o.GetType().GetProperty(name)!.SetValue(o, value);
        var enabled = Property(audio, "Enabled"); var volume = Property(audio, "Volume");
        var effectsVolume = Property(audio, "EffectsVolume");
        var voices = (List<SoundEffectInstance>)audio.GetType().GetField("voices", flags)!.GetValue(audio)!;
        var musicEnabled = Property(data, "Music"); var musicVolume = Property(data, "MusicVolume");
        var music = Get("music")!;
        Song Current() => (Song)music.GetType().GetField("current", flags)!.GetValue(music)!;
        void Step(int count = 30) { for (var i = 0; i < count; i++) Call("UpdateMusic", .1d); }
        void Scene(int id)
        {
            var session = new GameSession(catalog, location: new() { MapId = id, Position = new(5, 5) });
            Set("session", session); Set("adventure", new Adventure(session));
            Set("titleScreen", false);
        }
        var directory = Path.Combine(output, "music"); Directory.CreateDirectory(directory);
        try
        {
            check((string)Property(music, "Error") == "", "All five packaged Ogg music streams initialize on the real audio device");
            PropertySet(audio, "Enabled", true); PropertySet(audio, "Volume", .5f); PropertySet(audio, "EffectsVolume", 1f);
            PropertySet(data, "Music", true); PropertySet(data, "MusicVolume", .5f);
            Set("paused", false); Set("overlay", null); Set("mapOpen", false);
            Set("studioSplashRemaining", 2d); Set("titleScreen", false); Step();
            check(MediaPlayer.State != MediaState.Playing, "The studio ident has no premature town music");
            Set("studioSplashRemaining", 0d); Set("titleScreen", true); Step();
            var town = Current();
            check(town.Name == "Woodland Fantasy" && town.Duration.TotalSeconds > 145
                && MediaPlayer.State == MediaState.Playing && MediaPlayer.IsRepeating,
                "Title plays the full adapted Woodland Fantasy recording with repeat enabled");
            check(Math.Abs(MediaPlayer.Volume - .25f) < .001f, "Master and independent music volume multiply at full scene intensity");
            var plays = town.PlayCount;
            Thread.Sleep(120);
            var position = MediaPlayer.PlayPosition;
            foreach (var field in new[] { "paused", "mapOpen" })
            {
                Set(field, true); Step();
                check(MediaPlayer.State == MediaState.Playing && town.PlayCount == plays
                    && Math.Abs(MediaPlayer.Volume - .1625f) < .001f, $"{field} lowers music without restarting it");
                Set(field, false); Step();
            }
            PropertySet(data, "Music", false); Call("UpdateMusic", .1d);
            audio.GetType().GetMethod("Play")!.Invoke(audio, [12, false]);
            var effect = voices[^1];
            check(MediaPlayer.State == MediaState.Paused && MediaPlayer.Volume == 0 && effect.State == SoundState.Playing,
                "Music Off silences only music while action effects still play");
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            PropertySet(data, "Music", true); Step();
            check(town.PlayCount == plays && MediaPlayer.State == MediaState.Playing && MediaPlayer.PlayPosition >= position,
                "Re-enabling music resumes its existing position");
            PropertySet(audio, "Enabled", false); Step(1);
            check(MediaPlayer.State == MediaState.Paused, "Master Sound Off also silences music");
            PropertySet(audio, "Enabled", true);
            foreach (var (owner, property) in new[] { (audio, "Volume"), (data, "MusicVolume") })
            {
                PropertySet(owner, property, 0f); Step(1);
                check(MediaPlayer.State == MediaState.Paused, $"Zero {property} pauses the soundtrack");
                PropertySet(owner, property, .5f); Step();
            }
            PropertySet(audio, "EffectsVolume", .4f);
            audio.GetType().GetMethod("Play")!.Invoke(audio, [12, false]);
            effect = voices[^1];
            Step();
            check(Math.Abs(effect.Volume - .2f) < .001f && Math.Abs(MediaPlayer.Volume - .25f) < .001f,
                "Effects gain scales action sounds independently of the music stream");
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            audio.GetType().GetMethod("UpdateAmbience")!.Invoke(audio, [true, true, 1d]);
            var ambience = (SoundEffectInstance)audio.GetType().GetField("ambience", flags)!.GetValue(audio)!;
            audio.GetType().GetMethod("UpdateRiverAmbience")!.Invoke(audio, [1f, 0f, true, 1d]);
            var river = (SoundEffectInstance)audio.GetType().GetField("river", flags)!.GetValue(audio)!;
            check(Math.Abs(ambience.Volume - .036f) < .001f && river.Volume is > .046f and < .049f,
                "Effects volume also scales real cave ambience and positional river audio");
            PropertySet(audio, "EffectsVolume", 0f); audio.GetType().GetMethod("Update")!.Invoke(audio, [true]); Step();
            audio.GetType().GetMethod("Play")!.Invoke(audio, [12, false]);
            check(ambience.State == SoundState.Paused && river.State == SoundState.Paused
                && voices.Count == 0
                && MediaPlayer.State == MediaState.Playing && Math.Abs(MediaPlayer.Volume - .25f) < .001f,
                "Zero effects volume silences effects and ambience while music continues unchanged");
            PropertySet(audio, "EffectsVolume", 1f);
            audio.GetType().GetMethod("StopRiver")!.Invoke(audio, null);
            audio.GetType().GetMethod("UpdateAmbience")!.Invoke(audio, [false, false, 0d]);
            var trackType = type.Assembly.GetType("Yipe.Desktop.MusicTrack")!;
            music.GetType().GetMethod("Update")!.Invoke(music, [Enum.Parse(trackType, "Town"), false, .25f, 1f, .1d]);
            check(MediaPlayer.State == MediaState.Paused && MediaPlayer.Volume == 0, "Inactive window immediately silences the stream");
            Step();
            check(Current() == town && town.PlayCount == plays, "Focus return fades back in without replaying the intro");

            Scene(18); Step(1);
            check(Current() == town && MediaPlayer.Volume is > 0 and < .25f, "Leaving town fades the old track before switching");
            Step(); var travel = Current();
            check(travel.Name == "Celtic Impulse" && travel.Duration.TotalSeconds > 150 && MediaPlayer.State == MediaState.Playing,
                "Open overworld selects Celtic Impulse after the fade");
            plays = travel.PlayCount; Scene(19); Step();
            check(Current() == travel && travel.PlayCount == plays, "Crossing outdoor region boundaries preserves the song position");
            // Seek far enough into exploration to catch accidental intro restarts.
            MediaPlayer.Play(travel, TimeSpan.FromSeconds(23));
            var travelPosition = MediaPlayer.PlayPosition;
            var adventure = (Adventure)Get("adventure")!;
            var encounter = new CombatEncounter();
            encounter.Monsters[0] = new Monster { TypeId = 1, Name = "Rabbit", Health = 8, MaximumHealth = 8 };
            adventure.GetType().GetProperty("Battle")!.SetValue(adventure, encounter);
            Call("ShowBattle"); Step(4);
            var battle = Current();
            check(battle.Name == "Battle Theme A" && MediaPlayer.State == MediaState.Playing
                && Math.Abs(MediaPlayer.Volume - .25f) < .001f,
                "Combat starts and fades up within 0.4 seconds; battle choices do not duck its score");
            var battlePlays = battle.PlayCount; Step();
            check(battle.PlayCount == battlePlays, "Combat turns keep one continuous battle recording");
            MediaPlayer.Play(battle, TimeSpan.FromSeconds(17));
            var presentationType = type.Assembly.GetType("Yipe.Desktop.CombatPresentation")!;
            var actionType = type.Assembly.GetType("Yipe.Desktop.CombatAction")!;
            var presentation = Activator.CreateInstance(presentationType,
                [adventure.Player, encounter, Enum.Parse(actionType, "Attack"), -1, SpellPower.Low])!;
            var victory = new CombatTurn { Outcome = CombatOutcome.Victory };
            presentationType.GetMethod("Complete")!.Invoke(presentation,
                [adventure.Player, encounter, victory, null, 0, 145, false]);
            Call("ShowTurn", victory, presentation, "Victory"); Step();
            check(Current() == battle && Math.Abs(MediaPlayer.Volume - .125f) < .001f,
                "Victory lowers the battle score for the reward and level-up sounds");
            adventure.GetType().GetProperty("Battle")!.SetValue(adventure, null);
            Set("overlay", null); Step();
            check(Current() == travel && MediaPlayer.State == MediaState.Playing
                && MediaPlayer.PlayPosition >= travelPosition && MediaPlayer.PlayPosition.TotalSeconds < 26,
                "Battle return restores exploration at its interrupted position, not its intro");

            Scene(57); Step(); var cave = Current();
            check(cave.Name == "Cave Theme" && Math.Abs(MediaPlayer.Volume - .175f) < .001f,
                "Earth Cave gets the quiet underground score with headroom for ambience");
            MediaPlayer.Play(cave, TimeSpan.FromSeconds(31));
            var cavePosition = MediaPlayer.PlayPosition;
            plays = cave.PlayCount; Scene(63); Step();
            check(Current() == cave && cave.PlayCount == plays && MediaPlayer.PlayPosition >= cavePosition,
                "Moving between regions of a cave preserves continuous playback");
            adventure = (Adventure)Get("adventure")!;
            adventure.GetType().GetProperty("Battle")!.SetValue(adventure, encounter); Step(4);
            check(Current() == battle && MediaPlayer.State == MediaState.Playing && MediaPlayer.PlayPosition.TotalSeconds < 5,
                "A new cave battle starts a fresh combat cue");
            adventure.GetType().GetProperty("Battle")!.SetValue(adventure, null); Step();
            check(Current() == cave && MediaPlayer.PlayPosition >= cavePosition && MediaPlayer.PlayPosition.TotalSeconds < 34,
                "Cave combat returns to the correct underground song and bookmark");
            foreach (var id in new[] { 51, 67, 59, 43, 117, 120 })
            {
                Scene(id); Step();
                check(Current() == cave && MediaPlayer.State == MediaState.Playing,
                    $"Underground map {id} uses Cave Theme (Stone, Fire, Breeding, Final and Hell coverage)");
            }
            Scene(41); Step(); var crystal = Current();
            check(crystal.Name == "Crystal Caves" && Math.Abs(MediaPlayer.Volume - .175f) < .001f,
                "Ice Cave selects the gentler crystal score without drowning its ambience");
            plays = crystal.PlayCount; Scene(44); Step();
            check(Current() == crystal && crystal.PlayCount == plays,
                "Water Cave shares the crystal score without restarting it at the boundary");
            Set("paused", true); Step();
            check(Math.Abs(MediaPlayer.Volume - .11375f) < .001f, "Pause further dims the quieter cave mix");
            Set("paused", false);
            PropertySet(data, "Music", false); Step(1); Scene(67); Step();
            check(MediaPlayer.State == MediaState.Paused && MediaPlayer.Volume == 0,
                "A scene change while muted never leaks the newly selected cue");
            PropertySet(data, "Music", true); Step();
            check(Current() == cave && MediaPlayer.State == MediaState.Playing,
                "Re-enabling music selects the current scene, not the scene where it was muted");
            adventure = (Adventure)Get("adventure")!;
            adventure.GetType().GetProperty("Battle")!.SetValue(adventure, encounter); Step(1);
            adventure.GetType().GetProperty("Battle")!.SetValue(adventure, null); Step();
            check(Current() == cave && MediaPlayer.Volume > 0,
                "A canceled transition recovers without switching to a stale battle request");
            adventure.Player.Health = 0; Step();
            check(MediaPlayer.State == MediaState.Paused && MediaPlayer.Volume == 0,
                "Defeat silences the score so the death sound remains clear");
            Scene(18); Step();
            check(Current() == travel && MediaPlayer.PlayPosition >= travelPosition,
                "Leaving a cave restores the overworld bookmark");
            foreach (var song in new[] { battle, cave, crystal })
            {
                // Let the real streaming worker settle after the accelerated scene checks.
                MediaPlayer.Stop();
                for (var i = 0; i < 10; i++) { Thread.Sleep(20); FrameworkDispatcher.Update(); }
                MediaPlayer.Play(song, song.Duration - TimeSpan.FromSeconds(.2));
                var loopStart = MediaPlayer.PlayPosition;
                for (var i = 0; i < 250 && (MediaPlayer.State != MediaState.Playing || MediaPlayer.PlayPosition.TotalSeconds >= 5); i++)
                { Thread.Sleep(20); FrameworkDispatcher.Update(); }
                Console.WriteLine($"Loop {song.Name}: duration {song.Duration}; {loopStart} -> {MediaPlayer.PlayPosition}; {MediaPlayer.State}; repeat {MediaPlayer.IsRepeating}");
                check(MediaPlayer.State == MediaState.Playing && MediaPlayer.PlayPosition.TotalSeconds < 5,
                    $"{song.Name} actually repeats at the end of the streamed asset");
            }
            foreach (var id in new[] { 8, 17, 27 })
            {
                Scene(id); Step();
                check(Current() == town && MediaPlayer.State == MediaState.Playing, $"Town map {id} uses Woodland Fantasy");
            }
            MediaPlayer.Play(town, town.Duration - TimeSpan.FromSeconds(.2));
            var endPosition = MediaPlayer.PlayPosition;
            for (var i = 0; i < 40; i++) { Thread.Sleep(20); FrameworkDispatcher.Update(); }
            Console.WriteLine($"Music repeat boundary: {endPosition} -> {MediaPlayer.PlayPosition}; {MediaPlayer.State}");
            check(endPosition.TotalSeconds > 140 && MediaPlayer.State == MediaState.Playing && MediaPlayer.PlayPosition.TotalSeconds < 10,
                "The real streamed recording repeats after reaching its end");

            Set("paused", true); Set("pauseAmount", 1f);
            var page = type.GetField("pausePage", flags)!;
            page.SetValue(game, Enum.Parse(page.FieldType, "Audio")); Set("selected", 3); Set("selectionY", 36f); Set("menuMessage", "");
            Call("ActivateMenu");
            check(!(bool)Property(data, "Music"), "Sound menu toggles the music preference through production controls");
            Call("ActivateMenu"); Set("selected", 4); Call("UpdatePauseMenu", new InputFrame(new(1, 0), false, false, false));
            var profilePath = Path.ChangeExtension(Path.Combine(output, "test-saves", "startup-import.y3m"), ".profile.json");
            var reopened = Activator.CreateInstance(Get("profile")!.GetType(), [profilePath])!;
            check(Math.Abs((float)Property(Property(reopened, "Data"), "MusicVolume") - .6f) < .001f,
                "The music slider saves independently through the production profile path");
            Set("selected", 2);
            Call("UpdatePauseMenu", new InputFrame(new(-1, 0), false, false, false));
            reopened = Activator.CreateInstance(Get("profile")!.GetType(), [profilePath])!;
            check(Math.Abs((float)Property(Property(reopened, "Data"), "EffectsVolume") - .9f) < .001f
                && Math.Abs((float)Property(data, "MusicVolume") - .6f) < .001f,
                "Left lowers and persists effects volume without changing music volume");
            Call("UpdatePauseMenu", new InputFrame(new(1, 0), false, false, false));
            check((float)Property(audio, "EffectsVolume") == 1f, "Right raises effects volume back to its upper limit");
            Call("UpdatePauseMenu", new InputFrame(new(1, 0), false, false, false));
            check((float)Property(audio, "EffectsVolume") == 1f, "Volume adjustment clamps at 100 percent");
            Set("selected", 4); Set("selectionY", 48f); Set("menuMessage", ""); Capture("sound-menu.png");
            Set("paused", false); Set("pauseAmount", 0f); Call("ShowActions");
            var actionsMenu = Get("overlay")!;
            PropertySet(actionsMenu, "Selected", 4);
            Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            check((bool)Get("paused")! && Get("pausePage")!.ToString() == "Audio" && Get("overlay") is null,
                "Actions Sound opens the same stable settings page as Options Sound");
            foreach (var (row, owner, property) in new[] { (1, audio, "Volume"), (2, audio, "EffectsVolume"), (4, data, "MusicVolume") })
            {
                var original = Property(owner, property);
                Set("selected", row); Set("selectionY", row * 12f); PropertySet(owner, property, .5f);
                for (var i = 0; i < 2; i++) Call("UpdatePauseMenu", new InputFrame(new(-1, 0), false, false, false));
                check(Math.Abs((float)Property(owner, property) - .3f) < .001f
                    && (int)Get("selected")! == row && (float)Get("selectionY")! == row * 12f
                    && ReferenceEquals(Get("soundMenuParent"), actionsMenu) && Get("overlay") is null,
                    $"Repeated left adjustments keep {property} selected without recreating the menu");
                PropertySet(owner, property, .5f);
                for (var i = 0; i < 6; i++) Call("UpdatePauseMenu", new InputFrame(new(-1, 0), false, false, false));
                check((float)Property(owner, property) == 0 && (int)Get("selected")! == row,
                    $"{property} clamps at zero without wrapping or moving focus");
                PropertySet(owner, property, .9f);
                for (var i = 0; i < 2; i++) Call("UpdatePauseMenu", new InputFrame(new(1, 0), false, false, false));
                Call("UpdatePauseMenu", new InputFrame(Point.Zero, true, false, false));
                check((float)Property(owner, property) == 1 && (int)Get("selected")! == row,
                    $"{property} clamps at 100 percent and Confirm does not cycle it back to zero");
                PropertySet(owner, property, original);
            }
            Call("SavePreferences"); Set("menuMessage", ""); Set("pauseAmount", 1f);
            Capture("actions-sound-menu.png");
            foreach (var back in new[] { new InputFrame(Point.Zero, false, true, false),
                new InputFrame(Point.Zero, false, false, true), new InputFrame(Point.Zero, true, false, false) })
            {
                Set("selected", 5); Call("UpdatePauseMenu", back);
                check(!(bool)Get("paused")! && ReferenceEquals(Get("overlay"), actionsMenu)
                    && (int)Property(actionsMenu, "Selected") == 4 && Get("soundMenuParent") is null,
                    "Back, Start and the Back row restore Actions with Sound still selected");
                Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            }
            Call("BackPausePage");
            Set("paused", false); Set("pauseAmount", 0f); Call("ShowHelp");
            PropertySet(Get("overlay")!, "Selected", 4); Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            check((string)Property(Get("overlay")!, "Title") == "Credits", "Help opens the full categorized Credits menu");
            Capture("credits-menu.png");
            PropertySet(Get("overlay")!, "Selected", 1); Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            var credits = string.Join("\n", (string[])Property(Get("overlay")!, "Lines"));
            check(new[] { "Matthew Pablo", "Kevin MacLeod", "cynicmusic", "Brandon Morris", "FoxSynergy",
                "CC BY 3.0", "CC BY 4.0", "CC0 1.0", "Adapted" }.All(credits.Contains),
                "In-game Music credits retain all five authors, licenses and adaptation notices");
            Capture("music-credits.png");
            var creditLines = (string[])Property(Get("overlay")!, "Lines");
            foreach (var (author, file) in new[] { ("cynicmusic", "battle-credits.png"),
                ("Brandon Morris", "cave-credits.png"), ("FoxSynergy", "crystal-credits.png") })
            {
                var line = Array.FindIndex(creditLines, l => l.Contains(author));
                PropertySet(Get("overlay")!, "Page", line / (int)Property(Get("overlay")!, "LinesPerPage"));
                Capture(file);
            }
            Call("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            PropertySet(Get("overlay")!, "Selected", 2); Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            var soundCredits = string.Join("\n", (string[])Property(Get("overlay")!, "Lines"));
            check(new[] { "Nox_Sound", "Kenney", "Fantozzi", "qubodup", "JaggedStone", "Blender Foundation", "PagDev",
                "Independent.nu", "ecfike", "isaiah658", "Thimras", "Wolfgang_ / Ted Kerr" }.All(soundCredits.Contains),
                "Sound credits include every recorded-footstep and ambient source creator");
            Call("UpdateOverlay", new InputFrame(new(1, 0), false, false, false));
            check((int)Property(Get("overlay")!, "Page") == 1, "Long credits can be paged with the standard controls");
            Capture("sound-credits.png");
            Call("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            PropertySet(Get("overlay")!, "Selected", 3); Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            var technology = string.Join("\n", (string[])Property(Get("overlay")!, "Lines"));
            check(new[] { "MonoGame", ".NET", "Sam Lantinga", "Christopher Robinson", "Andrew Ward", "Sean Barrett" }.All(technology.Contains)
                && Directory.GetFiles(Path.Combine((string)Get("contentDirectory")!, "Credits", "Licenses")).Length == 8,
                "Technology credits and complete bundled notices reach the local build");
            Call("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            Call("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            check((string)Property(Get("overlay")!, "Title") == "Help & credits", "Credits Back returns through its parent menu");
            using var missing = (IDisposable)Activator.CreateInstance(music.GetType(), [directory])!;
            check(((string)Property(missing, "Error")).Length > 0 && (string)Property(audio, "Error") == "",
                "Unavailable music assets fail locally without disabling the existing sound system");
            check(before.SequenceEqual(GameSaveCodec.Encode((GameSession)saved["session"]!, catalog)),
                "Music playback and options preserve the complete adventure and RNG");
        }
        finally
        {
            PropertySet(audio, "Enabled", enabled); PropertySet(audio, "Volume", volume); PropertySet(audio, "EffectsVolume", effectsVolume);
            PropertySet(data, "Music", musicEnabled); PropertySet(data, "MusicVolume", musicVolume);
            foreach (var pair in saved) Set(pair.Key, pair.Value);
            Call("SavePreferences");
        }
        void Capture(string name)
        {
            Call("Draw", new GameTime());
            var pp = game.GraphicsDevice.PresentationParameters;
            using var image = new Texture2D(game.GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
            var pixels = new Color[image.Width * image.Height]; game.GraphicsDevice.GetBackBufferData(pixels); image.SetData(pixels);
            using var stream = File.Create(Path.Combine(directory, name)); image.SaveAsPng(stream, image.Width, image.Height);
        }
    }
}
