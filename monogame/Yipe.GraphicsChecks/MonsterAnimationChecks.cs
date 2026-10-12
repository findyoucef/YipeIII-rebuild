using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class MonsterAnimationChecks
{
    public static void Run(Game game, string output, Action<bool, string> check, int[]? captureTypes = null)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value) => type.GetField(name, flags)!.SetValue(game, value);
        object? Call(string name, params object?[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        var saved = new[] { "session", "adventure", "cameraPosition", "paused", "pauseAmount", "overlay", "pendingEvent",
            "pendingDirection", "tickRemaining", "sceneTime", "mapOpen", "miniatureMode", "scanlines", "vignette",
            "pixelLighting", "sunShaftStrength", "cloudShadowStrength", "rainStrength", "messageRemaining", "renderedMap", "journal" }.ToDictionary(name => name, Get);
        var player = (PlayerMotion)Get("player")!; var oldPosition = player.Position;
        var catalog = (MapCatalog)Get("catalog")!;
        var world = Get("world")!; var renderer = world.GetType();
        var roamingMotion = renderer.GetField("roamingMotion", flags)!.GetValue(world)!;
        var pursuingMotion = renderer.GetField("pursuingMotion", flags)!.GetValue(world)!;
        (int Roaming, int Pursuer) alerts = default;
        void Sync(Adventure adventure, bool animate) => alerts = ((int, int))renderer.GetMethod("SynchronizeMonsters")!.Invoke(world, [adventure, animate, null])!;
        void Travel(double seconds) => renderer.GetMethod("UpdateMonsters")!.Invoke(world, [seconds]);
        var directory = Path.Combine(output, "monster-animation"); Directory.CreateDirectory(directory);
        using var batch = new SpriteBatch(game.GraphicsDevice);
        using var target = new RenderTarget2D(game.GraphicsDevice, 128, 96);
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        var palettes = (Color[])renderer.Assembly.GetType("Yipe.Desktop.RetroArt")!.GetField("Palette", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try
        {
            Set("paused", false); Set("pauseAmount", 0f); Set("overlay", null); Set("pendingEvent", null); Set("pendingDirection", Point.Zero);
            Set("tickRemaining", double.MaxValue); Set("mapOpen", false); Set("messageRemaining", 0d);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("pixelLighting", false);
            Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0); Set("rainStrength", 0);
            var sheets = (Dictionary<int, Texture2D>)renderer.GetField("monsterAnimations", flags)!.GetValue(world)!;
            var animatedTypes = sheets.Keys.Order().ToArray();
            check(animatedTypes.SequenceEqual(Enumerable.Range(1, 41)), "Every canonical monster ID 1–41 has all 16 registered poses");
            bool Capture(int monster) => captureTypes is null || captureTypes.Contains(monster);
            var bottoms = (int[])renderer.GetField("monsterBottoms", flags)!.GetValue(world)!;
            foreach (var monster in animatedTypes)
            {
                var sheet = sheets[monster]; var pixels = new Color[sheet.Width * sheet.Height]; sheet.GetData(pixels);
                using (var file = File.Create(Path.Combine(directory, $"monster-{monster}-native-sheet.png"))) sheet.SaveAsPng(file, sheet.Width, sheet.Height);
                var frames = Enumerable.Range(0, 16).Select(frame => (from y in Enumerable.Range(frame / 4 * 48, 48)
                    from x in Enumerable.Range(frame % 4 * 48, 48) select pixels[y * sheet.Width + x]).ToArray()).ToArray();
                check(frames.All(frame => frame.Count(p => p.A == 255) > 40 && frame.All(p => p.A == 0 || p.A == 255 && palettes.Contains(p))),
                    $"Monster {monster}: all 16 poses are complete, opaque native pixel art in the shared palette");
                check(frames.All(frame => Enumerable.Range(0, frame.Length).Where(i => frame[i].A > 0).Max(i => i / 48) == bottoms[monster - 1] - 1),
                    $"Monster {monster}: every pose uses the original ground anchor without foot drift");
                check(Enumerable.Range(0, 4).Select(row => frames.Skip(row * 4).Take(4).Select(frame => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(frame.SelectMany(p => new[] { p.R, p.G, p.B, p.A }).ToArray()))).Distinct().Count()).All(count => count >= 3),
                    $"Monster {monster}: idle, movement, action and defeat each have distinct authored poses");
                if (monster >= 10)
                {
                    check(frames.All(frame => Enumerable.Range(0, frame.Length).Where(i => frame[i].A > 0)
                        .All(i => i % 48 is >= 2 and < 46 && i / 48 is >= 2 and < 47)),
                        $"Monster {monster}: long appendages retain a clear native-cell border");
                    check(frames.All(NoDetachedMarks),
                        $"Monster {monster}: native poses contain no detached footer lines or distant stray marks");
                }
            }
            var right = Sprite(1, 5, false); var left = Sprite(1, 5, true);
            foreach (var boss in animatedTypes.Where(monster => monster is >= 35 and <= 40))
            {
                var large = LargeIdle(boss, 0);
                check(large.Any(pixel => pixel.A == 255)
                    && Enumerable.Range(0, large.Length).All(i => large[i] == large[i / 128 / 2 * 2 * 128 + i % 128 / 2 * 2]),
                    $"Monster {boss}: the authored two-tile world footprint retains exact doubled native pixels");
                check(!large.SequenceEqual(LargeIdle(boss, 1.2)),
                    $"Monster {boss}: the large authored placement idles in place without acquiring travel");
                using var file = File.Create(Path.Combine(directory, $"boss-{boss}-native-world.png"));
                target.SaveAsPng(file, 128, 96);
            }
            check((from y in Enumerable.Range(20, 48) from x in Enumerable.Range(32, 48)
                select right[y * 128 + x] == left[y * 128 + 79 - (x - 32)]).All(same => same),
                "Monster facing mirrors the pose exactly without scaling or moving its ground anchor");
            check(Sprite(42, 9, true).SequenceEqual(Sprite(42, 0, false)), "NPC atlas cells retain their complete original artwork through the static fallback");

            var actual = Scene(1);
            var actor = new RoamingMonster(1, new(3, 5)); Roamer(actual, actor); Sync(actual, false);
            actor.Position = new(4, 5); Sync(actual, true);
            var snapshot = GameSaveCodec.Encode(actual.Session, catalog); var random = actual.Session.Random.State;
            var before = Frame(0); Travel(.125); var midway = Frame(.125);
            check(Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(3.5f, 5)
                && Property<bool>(roamingMotion, "IsMoving") && actor.Position == new MapPosition(4, 5)
                && !before.SequenceEqual(midway), "A roaming monster visibly crosses the accepted tile with a half-step position");
            Travel(.125); var landed = Frame(.25);
            check(Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(4, 5) && !Property<bool>(roamingMotion, "IsMoving")
                && !midway.SequenceEqual(landed) && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)) && random == actual.Session.Random.State,
                "Landing finishes at the authoritative tile without touching gameplay or RNG");
            actor.Position = new(3, 4); Sync(actual, true); Travel(.125);
            check(Property<bool>(roamingMotion, "FacingLeft") && Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(3.5f, 4.5f),
                "Diagonal travel interpolates both axes and uses its horizontal facing");
            actor.Position = new(9, 7); Sync(actual, true);
            check(!Property<bool>(roamingMotion, "IsMoving") && Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(9, 7),
                "Relocations snap safely instead of animating across unrelated terrain");
            actor = new(2, new(4, 5)); Roamer(actual, actor); Sync(actual, true);
            check(!Property<bool>(roamingMotion, "IsMoving") && Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(4, 5),
                "A newly spawned creature starts at its own tile without inheriting the prior actor's motion");
            var pursuer = new PursuingMonster(5, new(8, 6), new(8, 6)); typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer); Sync(actual, false);
            actor.Position = new(5, 5); pursuer.Position = new(7, 6); Sync(actual, true); Travel(.125);
            check(Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(4.5f, 5)
                && Property<Vector2>(pursuingMotion, "VisualPosition") == new Vector2(7.5f, 6), "Roaming and pursuing actors animate independently in the same scene");
            Frame(1); Set("paused", true); Set("pauseAmount", 1f);
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.1)));
            check(Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(4.5f, 5) && (double)Get("sceneTime")! == 1,
                "Production pause freezes monster travel and the creature-animation clock");
            Set("paused", false); Set("pauseAmount", 0f); Call("ShowActions");
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.1)));
            check(Property<Vector2>(pursuingMotion, "VisualPosition") == new Vector2(7.5f, 6), "An exploration action panel freezes monster travel");
            Set("overlay", null); Roamer(actual, null); Sync(actual, true);
            check(!Property<bool>(roamingMotion, "IsMoving") && Property<bool>(pursuingMotion, "IsMoving"), "Removing one actor clears its motion without interrupting the other");
            var replacementScene = Scene(57); Roamer(replacementScene, actor); Sync(replacementScene, true);
            check(!Property<bool>(roamingMotion, "IsMoving") && !Property<bool>(pursuingMotion, "IsMoving"), "Changing maps resets travel and clears departed actors");

            foreach (var heavy in animatedTypes.Where(monster => monster is 4 or 8 or 9 or 11 or 19 or 20 or 33 or 36 or 37 or 39 or 40 or 41))
            {
                actual = Scene(1); actor = new(heavy, new(5, 6)); Roamer(actual, actor); Sync(actual, false);
                actor.Position = new(6, 6); Sync(actual, true); Travel(.0625);
                var firstQuarter = Property<Vector2>(roamingMotion, "VisualPosition");
                Travel(.125); var lastQuarter = Property<Vector2>(roamingMotion, "VisualPosition");
                check(firstQuarter.X is > 5 and < 5.25f && lastQuarter.X is > 5.75f and < 6,
                    $"Monster {heavy}: weighted travel eases into its step and settles without overshooting");
                Travel(.0625);
                check(!Property<bool>(roamingMotion, "IsMoving") && Property<Vector2>(roamingMotion, "VisualPosition") == new Vector2(6, 6)
                    && actor.Position == new MapPosition(6, 6), $"Monster {heavy}: weighted travel still finishes at the accepted tile within 250 ms");
            }

            foreach (var hopper in animatedTypes.Where(monster => monster is 10 or 11 or 18 or 23 or 24 or 38))
            {
                actual = Scene(1); actor = new(hopper, new(5, 6)); Roamer(actual, actor); Sync(actual, false);
                int Lift() => (int)renderer.GetMethod("MonsterLift", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [hopper, 1d, roamingMotion])!;
                check(Lift() == 0, $"Monster {hopper}: resting claws remain grounded before a wing-assisted hop");
                actor.Position = new(6, 6); Sync(actual, true); Travel(.125);
                var crest = Lift(); Travel(.125);
                check(crest == 3 && Lift() == 0 && !Property<bool>(roamingMotion, "IsMoving"),
                    $"Monster {hopper}: wing-assisted travel lifts above its ground shadow and lands within 250 ms");
            }

            foreach (var flier in animatedTypes.Where(monster => monster is 14 or 21 or 22 or 25 or 26))
            {
                var lift = renderer.GetMethod("MonsterLift", BindingFlags.Static | BindingFlags.NonPublic)!;
                var heights = Enumerable.Range(0, 12).Select(frame => (int)lift.Invoke(null, [flier, frame / 3d, null])!).ToArray();
                check(heights.Min() >= 2 && heights.Max() <= 4 && heights.Distinct().Count() > 1,
                    $"Monster {flier}: restrained flight stays above the fixed ground shadow even when idle");
                actual = Scene(1); actor = new(flier, new(1, 1)); Roamer(actual, actor); Sync(actual, false);
                actor.Position = new(2, 1); Sync(actual, true); Travel(.25); Frame(0);
                check(Contact(flier, roamingMotion).All(pixel => pixel.A == 0),
                    $"Monster {flier}: hovering travel never emits planted-foot dust or water contact");
            }

            foreach (var swimmer in animatedTypes.Where(monster => monster is 27 or 28 or 29 or 39))
            {
                actual = Scene(1);
                for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
                    actual.Map.RestoreTile(new(x, y), (ushort)(x >= 2 ? 0x0c : 2));
                actor = new(swimmer, new(1, 1)); Roamer(actual, actor); Sync(actual, false); Frame(0);
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                actor.Position = new(2, 1); Sync(actual, true); Travel(.0625);
                // The water mask includes its dry bank; sample just beyond that pixel rim.
                var dryApproach = Contact(swimmer, roamingMotion); Travel(.09375);
                var shoreline = Contact(swimmer, roamingMotion);
                var waterAt = renderer.GetMethod("WaterAt", flags)!;
                check(dryApproach.All(pixel => pixel.A == 0) && shoreline.Any(pixel => pixel.A > 0)
                    && Enumerable.Range(0, shoreline.Length).All(i => shoreline[i].A == 0
                        || (bool)waterAt.Invoke(world, [i % 128, i / 128])!),
                    $"Monster {swimmer}: mid-step wet contact follows the interpolated feet and clips each ripple pixel to water");
                Travel(.09375); var wetLanding = Contact(swimmer, roamingMotion);
                check(wetLanding.Any(pixel => pixel.A > 0), $"Monster {swimmer}: arrival on water produces a restrained ripple");
                using (var file = File.Create(Path.Combine(directory, $"water-contact-{swimmer}.png"))) target.SaveAsPng(file, 128, 96);
                actor.Position = new(1, 1); Sync(actual, true); Travel(.0625);
                var wetDeparture = Contact(swimmer, roamingMotion); Travel(.125);
                var dryDeparture = Contact(swimmer, roamingMotion); Travel(.0625);
                var dryLanding = Contact(swimmer, roamingMotion);
                check(wetDeparture.Any(pixel => pixel.A > 0) && dryDeparture.All(pixel => pixel.A == 0)
                    && dryLanding.Any(pixel => pixel.A > 0)
                    && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)),
                    $"Monster {swimmer}: water-to-land travel stops its wake at the shoreline, settles with dry contact and preserves gameplay/RNG");
            }

            if (animatedTypes.Any(monster => monster is >= 25 and <= 29 or 39))
            {
                actual = Scene(57);
                actual.Map.RestoreTile(new(3, 4), 0x0c); actual.Map.RestoreTile(new(4, 4), 0x0c);
                actual.Visibility.Initialize(new Map(57, 1, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(6, 5));
                Roamer(actual, null); Sync(actual, false); var hiddenWater = Frame(0);
                var swimmer = animatedTypes.First(monster => monster is >= 25 and <= 29 or 39);
                actor = new(swimmer, new(3, 4)); Roamer(actual, actor); Sync(actual, false);
                actor.Position = new(4, 4); Sync(actual, true); Travel(.125);
                check(!actual.Visibility.Contains(actor.Position) && hiddenWater.SequenceEqual(Frame(0)),
                    "Unexplored shoreline travel emits no visible creature, shadow, contact or pursuit alert");
                Save(Frame(0), "water-fog-boundary.png");
            }

            foreach (var walker in animatedTypes.Where(monster => monster is 7 or 8 or 9 or 12 or 15 or 16 or 19 or 20 or 30 or 31 or 33 or 34 or 36 or 37 or 39 or 40 or 41))
            {
                actual = Scene(1); actor = new(walker, new(5, 6)); Roamer(actual, actor); Sync(actual, false);
                actor.Position = new(6, 6); Sync(actual, true); Travel(.04);
                var nearLift = WalkingPose(walker, roamingMotion);
                Sync(actual, true);
                check(nearLift.SequenceEqual(WalkingPose(walker, roamingMotion)), $"Monster {walker}: unchanged-tile synchronization preserves the leading foot");
                Travel(.125); var nearPlant = WalkingPose(walker, roamingMotion); Travel(.085);
                actor.Position = new(7, 7); Sync(actual, true); Travel(.04);
                var farLift = WalkingPose(walker, roamingMotion); Travel(.125); var farPlant = WalkingPose(walker, roamingMotion);
                check(!nearLift.SequenceEqual(nearPlant) && !farLift.SequenceEqual(farPlant)
                    && !nearLift.SequenceEqual(farLift) && !nearPlant.SequenceEqual(farPlant),
                    $"Monster {walker}: successive and diagonal steps render opposite lifted and planted feet");
                pursuer = new(walker, new(8, 8), new(8, 8)); typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer); Sync(actual, true);
                pursuer.Position = new(7, 8); Sync(actual, true); Travel(.04);
                check(nearLift.SequenceEqual(WalkingPose(walker, pursuingMotion)) && farPlant.SequenceEqual(WalkingPose(walker, roamingMotion)),
                    $"Monster {walker}: independent actors keep their own leading feet");
                actor = new(walker, new(5, 6)); Roamer(actual, actor); Sync(actual, true);
                actor.Position = new(6, 6); Sync(actual, true); Travel(.04);
                check(nearLift.SequenceEqual(WalkingPose(walker, roamingMotion)), $"Monster {walker}: replacement resets the stride");
            }

            actual = Scene(57); pursuer = new(8, new(8, 6), new(8, 6));
            typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer); Sync(actual, false);
            pursuer.Position = new(9, 6); Sync(actual, true); Travel(.25);
            check(Property<double>(pursuingMotion, "AlertRemaining") == 0 && alerts == (0, 0), "Spawning and movement away from the hero do not imply pursuit");
            pursuer.Position = new(8, 6); Sync(actual, true);
            check(Property<double>(pursuingMotion, "AlertRemaining") > .6 && alerts == (0, 8), "A first visible accepted approach triggers the pursuit cue and its monster voice together");
            Travel(.1); var alert = Property<double>(pursuingMotion, "AlertRemaining");
            var alertFrame = Frame(1); Save(alertFrame, "pursuit-alert.png");
            check(Enumerable.Range(0, alertFrame.Length).All(i => alertFrame[i] == alertFrame[i / 1280 / 4 * 4 * 1280 + i % 1280 / 4 * 4]),
                "The pursuit cue retains exact native pixels alongside the moving creature");
            var alertSnapshot = GameSaveCodec.Encode(actual.Session, catalog);
            var pursuerSnapshot = JsonSerializer.Serialize(pursuer);
            check(alertFrame.SequenceEqual(Frame(1)) && alertSnapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog))
                && pursuerSnapshot == JsonSerializer.Serialize(pursuer), "Repeated cue rendering is stable and preserves encounter state and RNG");
            Set("paused", true); Set("pauseAmount", 1f); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.1)));
            Set("paused", false); Set("pauseAmount", 0f); Call("ShowActions"); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.1)));
            Set("overlay", null); Set("mapOpen", true); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.1))); Set("mapOpen", false);
            check(Property<double>(pursuingMotion, "AlertRemaining") == alert, "Pause, exploration panels and maps freeze the pursuit cue clock");
            Travel(.15); pursuer.Position = new(7, 6); Sync(actual, true);
            var continuing = Property<double>(pursuingMotion, "AlertRemaining");
            Travel(1); pursuer.Position = new(6, 6); Sync(actual, true);
            check(continuing <= alert && Property<double>(pursuingMotion, "AlertRemaining") == 0 && alerts == (0, 0),
                "A continuing chase does not restart the cue, including after its fade finishes");
            pursuer = new(8, new(8, 6), new(8, 6)); typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer); Sync(actual, false);
            pursuer.Position = new(7, 6); alerts = ((int, int))renderer.GetMethod("SynchronizeMonsters")!.Invoke(world, [actual, true, new Rectangle(0, 0, 32, 32)])!;
            check(Property<double>(pursuingMotion, "AlertRemaining") == 0 && alerts == (0, 0), "An offscreen approach stays quiet until the creature is in view");
            Travel(.25); pursuer.Position = new(6, 6); alerts = ((int, int))renderer.GetMethod("SynchronizeMonsters")!.Invoke(world, [actual, true, new Rectangle(48, 86, 320, 180)])!;
            check(Property<double>(pursuingMotion, "AlertRemaining") > 0 && alerts == (0, 8), "The first in-view approach rearms the cue and voice after an offscreen chase");
            actual = Scene(1); typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer); Sync(actual, true);
            check(Property<double>(pursuingMotion, "AlertRemaining") == 0 && alerts == (0, 0), "Changing maps clears an old pursuit cue and voice even for a retained actor");
            actual = Scene(57);
            var sealedMap = new Map(57, 1, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray());
            actual.Visibility.Initialize(sealedMap, new(6, 5)); actor = new(6, new(0, 0)); Roamer(actual, actor); Sync(actual, false);
            actor.Position = new(1, 0); Sync(actual, true);
            check(Property<double>(roamingMotion, "AlertRemaining") == 0 && alerts == (0, 0), "Unexplored creatures never announce a hidden approach");
            actual.Visibility.RevealFrom(sealedMap, new(1, 0)); Travel(.25); actor.Position = new(2, 0); Sync(actual, true);
            check(Property<double>(roamingMotion, "AlertRemaining") > 0 && alerts == (6, 0), "Roaming creatures also announce their first revealed approach with their own voice");
            Roamer(actual, null); Sync(actual, true); actor = new(6, new(2, 0)); Roamer(actual, actor); Sync(actual, true);
            check(Property<double>(roamingMotion, "AlertRemaining") == 0 && alerts == (0, 0), "Removal and replacement clear the pulse instead of transferring it to a new creature");
            actual = Scene(1); var glyph = Cue(actual, .7); var fadingGlyph = Cue(actual, .09);
            check(glyph.Any(p => p == palettes[35]) && fadingGlyph.Any(p => p.A > 0)
                && fadingGlyph.All(p => p.A < 255) && Cue(actual, 0).All(p => p.A == 0), "The amber glyph has a readable outline, fades gently and leaves no pixels behind");
            actual = Scene(57); actual.Visibility.Initialize(sealedMap, new(3, 2)); glyph = Cue(actual, .7);
            check(glyph.Any(p => p.A > 0) && Enumerable.Range(0, glyph.Length).All(i => glyph[i].A == 0 || actual.Visibility.Contains(new(i % 128 / 32, i / 128 / 32))),
                "Every cue pixel is clipped to discovered terrain when its outline crosses a fog boundary");

            var audio = Get("audio")!; var audioType = audio.GetType();
            var enabled = Property<bool>(audio, "Enabled");
            var volume = Property<float>(audio, "Volume");
            var effectsVolume = Property<float>(audio, "EffectsVolume");
            var voices = (List<SoundEffectInstance>)audioType.GetField("voices", flags)!.GetValue(audio)!;
            void StopSounds() => audioType.GetMethod("Update")!.Invoke(audio, [false]);
            try
            {
                StopSounds(); audioType.GetProperty("Enabled")!.SetValue(audio, true);
                audioType.GetProperty("Volume")!.SetValue(audio, .4f);
                audioType.GetProperty("EffectsVolume")!.SetValue(audio, .25f);
                Set("journal", new MapJournal());
                actual = Scene(1); actor = new(6, new(9, 5)); Roamer(actual, actor);
                pursuer = new(8, new(9, 6), new(9, 6)); typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer);
                Call("SynchronizeWorld", false);
                check(voices.Count == 0, "Restoring a world does not play monster alerts");
                var beforeAlert = GameSaveCodec.Encode(actual.Session, catalog);
                actor.Position = new(8, 5); pursuer.Position = new(8, 6); Call("SynchronizeWorld", true);
                check(voices.Count == 2 && voices.All(voice => voice.State == SoundState.Playing && Math.Abs(voice.Volume - .1f) < .00001f)
                    && Property<double>(roamingMotion, "AlertRemaining") > 0 && Property<double>(pursuingMotion, "AlertRemaining") > 0,
                    "Both visible pursuit alerts play their monster voices together at the effects-slider volume");
                var played = voices.ToArray(); Call("SynchronizeWorld", true); Travel(1);
                actor.Position = new(7, 5); pursuer.Position = new(7, 6); Call("SynchronizeWorld", true);
                check(voices.SequenceEqual(played), "Repeated synchronization and continuing pursuit never replay the alert sound");
                check(beforeAlert.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), "Pursuit audio preserves the saved adventure and RNG");
                StopSounds(); Call("SynchronizeWorld", false);
                audioType.GetProperty("Enabled")!.SetValue(audio, false);
                actor.Position = new(6, 5); Call("SynchronizeWorld", true);
                check(voices.Count == 0 && Property<double>(roamingMotion, "AlertRemaining") > 0, "Sound Off keeps the visual alert without starting a voice");
                audioType.GetProperty("Enabled")!.SetValue(audio, true); Call("SynchronizeWorld", true);
                check(voices.Count == 0, "Unmuting does not replay a pursuit alert that was already shown");
                Call("SynchronizeWorld", false); audioType.GetProperty("EffectsVolume")!.SetValue(audio, 0f);
                pursuer.Position = new(6, 6); Call("SynchronizeWorld", true);
                check(voices.Count == 0 && Property<double>(pursuingMotion, "AlertRemaining") > 0, "Zero effects volume suppresses pursuit audio while retaining the glyph");
            }
            finally
            {
                StopSounds(); audioType.GetProperty("Enabled")!.SetValue(audio, enabled);
                audioType.GetProperty("Volume")!.SetValue(audio, volume);
                audioType.GetProperty("EffectsVolume")!.SetValue(audio, effectsVolume);
            }

            actual = Scene(1); pursuer = new(8, new(9, 6), new(9, 6)); typeof(Adventure).GetProperty("Pursuer")!.SetValue(actual, pursuer); Sync(actual, false);
            for (var frame = 0; frame < 90; frame++)
            {
                if (frame is 10 or 35) { pursuer.Position = new(pursuer.Position.X - 1, pursuer.Position.Y); Sync(actual, true); }
                Travel(1d / 30); Save(Frame(frame / 30d), $"pursuit-{frame:D3}.png");
            }

            actual = Scene(1); actor = new(1, new(5, 5)); Roamer(actual, actor); Sync(actual, false);
            actor.Position = player.Position; Sync(actual, true);
            var battle = new CombatEncounter();
            foreach (var slot in Enumerable.Range(0, 3)) battle.Monsters[slot] = new Monster
            { TypeId = new[] { 1, 2, 5 }[slot], Name = CombatCatalog.Monster(new[] { 1, 2, 5 }[slot]).Name, Health = 200, MaximumHealth = 200, AttackRating = 15, DefenseRating = 1, Rank = 1 };
            typeof(Adventure).GetProperty("Battle")!.SetValue(actual, battle);
            Set("pendingEvent", new AdventureEvent(AdventureEventKind.Combat, "Combat"));
            snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            var battleSnapshot = JsonSerializer.Serialize(battle, new JsonSerializerOptions { IncludeFields = true });
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.125)));
            check(Get("overlay") is null && Get("pendingEvent") is not null, "A monster-triggered battle waits for its visible approach to finish");
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.125)));
            check(Get("pendingEvent") is null && Property<bool>(Get("overlay")!, "Combat") && !Property<bool>(roamingMotion, "IsMoving")
                && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)) && battleSnapshot == JsonSerializer.Serialize(battle, new JsonSerializerOptions { IncludeFields = true }),
                "The completed approach opens combat once, preserving the already resolved encounter and RNG");
            var menu = Get("overlay")!; var resting = Frame(0);
            Call("UpdateCombatPresentation", .6d); var breathing = Draw();
            check(Property<double>(menu, "CombatTime") == .6 && !resting.SequenceEqual(breathing)
                && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), "Creature idles animate on the battle command menu without taking a turn");
            Save(breathing, "battle-idles.png");
            Set("paused", true); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.2)));
            check(Property<double>(menu, "CombatTime") == .6, "Pausing also freezes battle idle poses");
            Set("paused", false); Set("overlay", null); typeof(Adventure).GetProperty("Battle")!.SetValue(actual, null);

            foreach (var monster in animatedTypes)
            {
                actual = Scene(1); actor = new(monster, new(5, 6)); Roamer(actual, actor); Sync(actual, false);
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                for (var frame = 0; frame < 120; frame++)
                {
                    if (frame is 30 or 60 or 90) { actor.Position = new(actor.Position.X + (frame == 90 ? -1 : 1), actor.Position.Y); Sync(actual, true); }
                    Travel(1d / 30);
                    if (Capture(monster)) Save(Frame(frame / 30d), $"world-{monster}-{frame:D3}.png");
                    else if (frame % 4 == 0) Frame(frame / 30d);
                }
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), $"Monster {monster}: four-second movement capture leaves the full session and RNG unchanged");
            }
            foreach (var monster in animatedTypes)
            {
                actual = Scene(1); actor = new(monster, new(5, 6)); Roamer(actual, actor); Sync(actual, false);
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                for (var frame = 0; frame < 96; frame++)
                {
                    if (frame is >= 6 and <= 48 && frame % 6 == 0)
                    {
                        actor.Position = new(actor.Position.X + (frame <= 24 ? 1 : -1), actor.Position.Y);
                        Sync(actual, true);
                    }
                    Travel(1d / 24);
                    if (Capture(monster)) Save(Frame(frame / 24d), $"stride-{monster}-{frame:D3}.png");
                    else if (frame % 4 == 0) Frame(frame / 24d);
                }
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), $"Monster {monster}: continuous strides and turning preserve the full session and RNG");
            }
            foreach (var monster in animatedTypes.Where(monster => monster is >= 25 and <= 29 or 39))
            {
                actual = Scene(1);
                for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++)
                    actual.Map.RestoreTile(new(x, y), (ushort)(x >= 7 ? 0x0c : 2));
                actor = new(monster, new(5, 6)); Roamer(actual, actor); Sync(actual, false);
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                for (var frame = 0; frame < 96; frame++)
                {
                    if (frame is 12 or 24 or 36 or 48)
                    {
                        actor.Position = new(actor.Position.X + (frame <= 24 ? 1 : -1), actor.Position.Y);
                        Sync(actual, true);
                    }
                    Travel(1d / 24);
                    if (Capture(monster)) Save(Frame(frame / 24d), $"terrain-{monster}-{frame:D3}.png");
                    else if (frame % 4 == 0) Frame(frame / 24d);
                }
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)),
                    $"Monster {monster}: rendered land/water crossings preserve terrain, the full session and RNG");
            }
            foreach (var monster in animatedTypes)
            {
                actual = Scene(1); Sync(actual, false);
                actual.Player.Strength = 40; actual.Player.Dexterity = 1; actual.Player.WeaponRank = 6;
                actual.Player.Health = actual.Player.MaximumHealth = 1000;
                battle = new CombatEncounter();
                battle.Monsters[0] = new Monster { TypeId = monster, Name = CombatCatalog.Monster(monster).Name,
                    Health = 400, MaximumHealth = 400, AttackRating = 25, DefenseRating = 1, Rank = 1 };
                typeof(Adventure).GetProperty("Battle")!.SetValue(actual, battle); Call("ShowBattle"); Choose("Attack");
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                battleSnapshot = JsonSerializer.Serialize(battle, new JsonSerializerOptions { IncludeFields = true });
                var windup = Frame(.55); var strike = Frame(.72); var recovery = Frame(.87);
                check(!windup.SequenceEqual(strike) && !strike.SequenceEqual(recovery), $"Monster {monster}: the real combat counterattack renders wind-up, strike and recovery");
                if (Capture(monster)) for (var frame = 0; frame < 72; frame++) Save(Frame(frame / 30d), $"battle-{monster}-{frame:D3}.png");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)) && battleSnapshot == JsonSerializer.Serialize(battle, new JsonSerializerOptions { IncludeFields = true }),
                    $"Monster {monster}: rendering the combat animation preserves the resolved turn and RNG");
                Choose("Continue"); battle.Monsters[0].Health = 1;
                for (var attempt = 0; attempt < 10 && battle.Monsters[0].Health > 0; attempt++)
                {
                    Choose("Attack");
                    if (battle.Monsters[0].Health > 0) Choose("Continue");
                }
                check(battle.Monsters[0].Health <= 0 && !Frame(.24).SequenceEqual(Frame(.61)), $"Monster {monster}: a real killing blow plays its defeat poses before settling");
                if (Capture(monster))
                {
                    Save(Frame(.24), $"defeat-{monster}-early.png"); Save(Frame(.61), $"defeat-{monster}-late.png");
                    for (var frame = 0; frame < 36; frame++) Save(Frame(frame / 30d), $"defeat-{monster}-{frame:D3}.png");
                }
            }
            {
                var empowered = (Texture2D)renderer.GetField("empoweredGatekeeperAnimation", flags)!.GetValue(world)!;
                var variantPixels = new Color[192 * 192]; var basePixels = new Color[variantPixels.Length];
                empowered.GetData(variantPixels); sheets[41].GetData(basePixels);
                check(!variantPixels.SequenceEqual(basePixels) && Enumerable.Range(0, variantPixels.Length)
                    .All(i => variantPixels[i].A == basePixels[i].A
                        && (variantPixels[i].A == 0 || palettes.Contains(variantPixels[i]))),
                    "Empowered Gatekeeper caches sharper key/ember accents in the same native palette and silhouette");
                using (var file = File.Create(Path.Combine(directory, "monster-41-empowered-native-sheet.png")))
                    empowered.SaveAsPng(file, 192, 192);
                foreach (var (frame, name) in new[] { (0, "idle"), (4, "travel"), (9, "strike"), (15, "defeat") })
                    Gallery(frame, name, sheets);
                actual = Scene(117); Sync(actual, false); actual.Player.HellGatekeeperState = HellGatekeeperState.Waiting;
                using (var guardTarget = new RenderTarget2D(game.GraphicsDevice, 416, 384))
                {
                    Color[] Guard(double time, bool onlySprite = false)
                    {
                        game.GraphicsDevice.SetRenderTarget(guardTarget); game.GraphicsDevice.Clear(Color.Transparent);
                        batch.Begin(samplerState: SamplerState.PointClamp);
                        if (onlySprite)
                        {
                            var idlePose = (int)renderer.GetMethod("MonsterIdleFrame", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [41, time])!;
                            renderer.GetMethod("DrawAnimatedMonster")!.Invoke(world, [batch, 41, new Rectangle(184, 0, 48, 48), idlePose, false, null]);
                        }
                        else renderer.GetMethod("Draw")!.Invoke(world, [batch, actual, player, Get("town"), time, false, 0f]);
                        batch.End(); game.GraphicsDevice.SetRenderTarget(null);
                        var pixels = new Color[416 * 384]; guardTarget.GetData(pixels); return pixels;
                    }
                    var spriteReference = Guard(0, true); var waiting = Guard(0);
                    var laterReference = Guard(1.3, true); var later = Guard(1.3);
                    var ink = Enumerable.Range(0, spriteReference.Length).Where(i => spriteReference[i].A == 255).ToArray();
                    check(ink.Length > 40 && ink.All(i => waiting[i] == spriteReference[i])
                        && Enumerable.Range(0, laterReference.Length).Where(i => laterReference[i].A == 255).All(i => later[i] == laterReference[i]),
                        "The waiting Gatekeeper animates after the arch and scenery with every face/key/body pixel unobscured");
                    check(!spriteReference.SequenceEqual(laterReference), "The special exit guard uses its authored idle cycle");
                    using (var file = File.Create(Path.Combine(directory, "gatekeeper-waiting-world.png"))) guardTarget.SaveAsPng(file, 416, 384);
                    actual.Player.HellGatekeeperState = HellGatekeeperState.Paid;
                    check(!waiting.SequenceEqual(Guard(0)), "A paid or defeated guardian disappears from the existing waiting-guard path");
                    actual.Visibility.Initialize(new Map(117, 5, Enumerable.Repeat((ushort)0x22, Map.TileCount).ToArray()), new(6, 5));
                    var hidden = Guard(0); actual.Player.HellGatekeeperState = HellGatekeeperState.Waiting;
                    check(hidden.SequenceEqual(Guard(0)), "A hidden waiting guardian does not leak through unexplored terrain");
                }
                foreach (var (boss, difficulty, empoweredState) in new[]
                    { (35, 2, false), (36, 2, false), (37, 2, false), (38, 2, false), (39, 2, false), (40, 2, false),
                      (41, 2, false), (41, 1, true), (41, 2, true), (41, 3, true) })
                {
                    actual = Scene(1); Sync(actual, false);
                    actual.Player.Health = actual.Player.MaximumHealth = 10000;
                    actual.Player.Strength = 40; actual.Player.Dexterity = 1; actual.Player.WeaponRank = 6;
                    actual.Player.Difficulty = difficulty; actual.Player.GatekeeperState = (ushort)(empoweredState ? 1 : 0);
                    var placement = Enumerable.Range(1, 128).SelectMany(map => MonsterPlacements.ForMap(map, difficulty, actual.Player.Level))
                        .First(placement => placement.TypeId == boss);
                    battle = Combat.StartMonsterEncounter(placement, actual.Player, actual.Session.Random, actual.Map.Environment);
                    var expected = empoweredState ? new[] { 41, difficulty + 37, 37 }
                        : new[] { boss, placement.CompanionType1, placement.CompanionType2 };
                    check(battle.Monsters.Select(monster => monster.TypeId).SequenceEqual(expected),
                        $"Boss {boss}, difficulty {difficulty}: the real encounter retains its canonical companion group");
                    typeof(Adventure).GetProperty("Battle")!.SetValue(actual, battle); Call("ShowBattle"); Choose("Attack");
                    snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                    battleSnapshot = JsonSerializer.Serialize(battle, new JsonSerializerOptions { IncludeFields = true });
                    var label = $"boss-{boss}-d{difficulty}-" + (empoweredState ? "empowered" : "ordinary");
                    if (empoweredState)
                    {
                        var resolved = Frame(.72); battle.Monsters[0].GatekeeperEmpowered = false;
                        var delayed = Frame(.72); battle.Monsters[0].GatekeeperEmpowered = true;
                        check(resolved.SequenceEqual(delayed), "Empowered presentation follows the resolved actor snapshot throughout its strike");
                    }
                    for (var frame = 0; frame < 72; frame++) Save(Frame(frame / 30d), $"{label}-{frame:D3}.png");
                    check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog))
                        && battleSnapshot == JsonSerializer.Serialize(battle, new JsonSerializerOptions { IncludeFields = true }),
                        $"Boss {boss}, difficulty {difficulty}: three-slot strike rendering leaves the resolved battle and RNG unchanged");
                    Choose("Continue");
                    var slots = Enumerable.Range(0, 3).Where(slot => battle.Monsters[slot].TypeId > 0).ToArray();
                    foreach (var slot in slots)
                    {
                        battle.Monsters[slot].Health = 1; battle.Monsters[slot].DefenseRating = 1; battle.SelectedTarget = slot;
                        for (var attempt = 0; attempt < 10 && battle.Monsters[slot].Health > 0; attempt++)
                        {
                            Choose("Attack");
                            if (battle.Monsters[slot].Health > 0) Choose("Continue");
                        }
                        Save(Frame(.61), $"{label}-defeat-slot-{slot}.png");
                        check(battle.Monsters[slot].Health <= 0, $"Boss {boss} party slot {slot}: a real killing blow retains a readable defeat portrait");
                        if (slot != slots[^1]) Choose("Continue");
                    }
                }
            }
        }
        finally
        {
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            player.SnapTo(oldPosition);
            Sync((Adventure)saved["adventure"]!, false);
        }

        Adventure Scene(int id)
        {
            var cell = (id - 1) % 40;
            var session = new GameSession(catalog, new Player { Name = "Monster Check", Health = 600, MaximumHealth = 600, Level = 10 },
                new WorldLocation { MapId = id, Position = new(6, 5), MapLayer = (id - 1) / 40, RegionX = cell % 6 - 4, RegionY = 2 - cell / 6 });
            var result = new Adventure(session);
            for (var x = 0; x < Map.Width; x++) for (var y = 0; y < Map.Height; y++) result.Visibility.RevealFrom(result.Map, new(x, y));
            Set("session", session); Set("adventure", result); player.SnapTo(session.Location.Position); Set("cameraPosition", new Vector2(48, 86));
            Set("paused", false); Set("pauseAmount", 0f); Set("pendingEvent", null); Set("overlay", null);
            Set("pendingDirection", Point.Zero); Set("tickRemaining", double.MaxValue);
            return result;
        }
        void Roamer(Adventure adventure, RoamingMonster? actor) => typeof(Adventure).GetField("roaming", flags)!.SetValue(adventure, actor);
        void Choose(string label)
        {
            var overlay = Get("overlay")!;
            var options = Property<Array>(overlay, "Options").Cast<object>().ToArray();
            overlay.GetType().GetProperty("Selected")!.SetValue(overlay, Array.FindIndex(options, option => Property<string>(option, "Label") == label));
            Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            for (var frame = 0; ReferenceEquals(overlay, Get("overlay"))
                && Property<bool>(overlay, "ContinueRequested") && frame < 120; frame++)
                Call("UpdateCombatPresentation", 1d / 60);
        }
        Color[] Sprite(int monster, int frame, bool facing)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp);
            renderer.GetMethod("DrawAnimatedMonster")!.Invoke(world, [batch, monster, new Rectangle(32, 20, 48, 48), frame, facing, null]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels;
        }
        Color[] WalkingPose(int monster, object motion)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp);
            renderer.GetMethod("DrawMonsterActor", flags)!.Invoke(world, [batch, monster, new Vector2(1, 1), 1, 1d, false, motion, null]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels;
        }
        Color[] Contact(int monster, object motion)
        {
            var position = Property<Vector2>(motion, "VisualPosition");
            var feet = new Point((int)MathF.Round(position.X * 32) + 16, (int)MathF.Round(position.Y * 32) + 32);
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp);
            renderer.GetMethod("DrawMonsterContact", flags)!.Invoke(world, [batch, monster, feet, 1, motion]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels;
        }
        Color[] LargeIdle(int monster, double time)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent);
            batch.Begin(samplerState: SamplerState.PointClamp);
            renderer.GetMethod("DrawMonsterActor", flags)!.Invoke(world, [batch, monster, new Vector2(1, 1), 2, time, false, null, null]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels;
        }
        void Gallery(int frame, string name, Dictionary<int, Texture2D> sheets)
        {
            using var gallery = new RenderTarget2D(game.GraphicsDevice, 1120, 800);
            game.GraphicsDevice.SetRenderTarget(gallery); game.GraphicsDevice.Clear(palettes[1]);
            batch.Begin(samplerState: SamplerState.PointClamp);
            var font = Get("font")!; var drawText = font.GetType().GetMethod("Draw")!;
            drawText.Invoke(font, [batch, $"41 MONSTERS / 656 NATIVE POSES - {name.ToUpperInvariant()}", 8, 8, palettes[7], 2]);
            foreach (var monster in sheets.Keys.Order())
            {
                var x = (monster - 1) % 7 * 160; var y = 32 + (monster - 1) / 7 * 128;
                drawText.Invoke(font, [batch, $"{monster:D2} {CombatCatalog.Monster(monster).Name}", x + 4, y, palettes[7], 1]);
                batch.Draw(sheets[monster], new Rectangle(x + 32, y + 16, 96, 96),
                    new Rectangle(frame % 4 * 48, frame / 4 * 48, 48, 48), Color.White);
            }
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            using var file = File.Create(Path.Combine(directory, $"roster-{name}.png")); gallery.SaveAsPng(file, 1120, 800);
        }
        Color[] Cue(Adventure adventure, double remaining)
        {
            game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.Transparent); batch.Begin(samplerState: SamplerState.PointClamp);
            renderer.GetMethod("DrawPursuitAlert", flags)!.Invoke(world, [batch, adventure, new Point(64, 60), remaining]);
            batch.End(); game.GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels;
        }
        Color[] Frame(double time) { Set("sceneTime", time); if (Get("overlay") is { } overlay) overlay.GetType().GetProperty("CombatTime")!.SetValue(overlay, time); return Draw(); }
        Color[] Draw() { Call("Draw", new GameTime()); var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels; }
        void Save(Color[] pixels, string name) { capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720); }
    }

    private static bool NoDetachedMarks(Color[] pixels)
    {
        var remaining = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].A > 0).ToHashSet();
        var groups = new List<(int Count, Rectangle Bounds)>();
        while (remaining.Count > 0)
        {
            var seed = remaining.First(); remaining.Remove(seed);
            var queue = new Queue<int>(); queue.Enqueue(seed);
            var left = seed % 48; var right = left; var top = seed / 48; var bottom = top; var count = 0;
            while (queue.TryDequeue(out var index))
            {
                var x = index % 48; var y = index / 48; count++;
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                        if (x + dx is >= 0 and < 48 && y + dy is >= 0 and < 48
                            && remaining.Remove((y + dy) * 48 + x + dx)) queue.Enqueue((y + dy) * 48 + x + dx);
            }
            groups.Add((count, new(left, top, right - left + 1, bottom - top + 1)));
        }
        var body = groups.MaxBy(group => group.Count).Bounds;
        return groups.All(group => Math.Max(body.Top - group.Bounds.Bottom, group.Bounds.Top - body.Bottom) < 3);
    }
}
