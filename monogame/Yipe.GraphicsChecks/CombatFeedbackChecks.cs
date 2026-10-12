using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Yipe.Core;
using Yipe.Desktop;

namespace Yipe.GraphicsChecks;

internal static class CombatFeedbackChecks
{
    public static void Run(Game game, string output, Action<bool, string> check)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object? Get(string name) => type.GetField(name, flags)!.GetValue(game);
        void Set(string name, object? value)
        {
            var field = type.GetField(name, flags)!;
            field.SetValue(game, field.FieldType.IsEnum && value is string page ? Enum.Parse(field.FieldType, page) : value);
        }
        object? Call(string name, params object?[] args) => type.GetMethod(name, flags)!.Invoke(game, args);
        T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        var saved = new[] { "session", "adventure", "cameraPosition", "paused", "pauseAmount", "overlay", "mapOpen",
            "sceneTime", "miniatureMode", "scanlines", "vignette", "pixelLighting", "sunShaftStrength",
            "cloudShadowStrength", "rainStrength", "screenShake", "pausePage", "selected", "selectionY", "menuMessage", "messageRemaining" }
            .ToDictionary(name => name, Get);
        var catalog = (MapCatalog)Get("catalog")!;
        var audio = Get("audio")!; var wasAudible = Property<bool>(audio, "Enabled");
        var oldEffectsVolume = Property<float>(audio, "EffectsVolume");
        var directory = Path.Combine(output, "combat-feedback"); Directory.CreateDirectory(directory);
        var json = new JsonSerializerOptions { IncludeFields = true };
        var profileData = Property<object>(Get("profile")!, "Data");
        var wasAutomatic = Property<bool>(profileData, "AutoBattle");
        void Auto(bool enabled) => profileData.GetType().GetProperty("AutoBattle")!.SetValue(profileData, enabled);
        object Presentation() => Property<object>(Get("overlay")!, "Presentation");
        double CounterAt(int slot) => (double)Presentation().GetType().GetMethod("CounterAt")!.Invoke(Presentation(), [slot])!;
        float HealthAt(int actor, double time) => (float)Presentation().GetType().GetMethod("HealthAt")!.Invoke(Presentation(), [actor, time])!;
        uint combatSeed = 1;
        using var capture = new Texture2D(game.GraphicsDevice, 1280, 720);
        try
        {
            audio.GetType().GetProperty("Enabled")!.SetValue(audio, false); Auto(false);
            Set("paused", false); Set("pauseAmount", 0f); Set("mapOpen", false); Set("screenShake", false);
            Set("miniatureMode", 0); Set("scanlines", false); Set("vignette", false); Set("messageRemaining", 0d);
            Set("pixelLighting", false); Set("sunShaftStrength", 0); Set("cloudShadowStrength", 0); Set("rainStrength", 0);
            combatSeed = Enumerable.Range(1, 200).Select(value => (uint)value).First(value =>
            {
                var turn = Fixture(false, value).Attack();
                return turn.PlayerDamage > 0 && turn.MonsterDamage > 0;
            });

            var actual = Fixture(); var expected = Fixture(false);
            check(Property<Array>(Get("overlay")!, "Options").Cast<object>().Select(option => Property<string>(option, "Label"))
                .SequenceEqual(new[] { "Attack", "Ability: None", "Spells", "Inventory", "Enemy info" }),
                "Battle commands omit Flee and Change target");
            Save(Frame(0), "choices.png");
            var attack = expected.Attack(); Choose("Attack");
            Result(expected, [attack.PlayerDamage, 0, 0], attack.MonsterDamage, "Attack");
            check(attack.PlayerDamage > 0 && attack.MonsterDamage > 0, "Combat fixture exercises both outgoing and incoming hits");
            var snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            var battleBefore = JsonSerializer.Serialize(actual.Battle, json);
            check(Property<int[]>(Presentation(), "DamageFromMonsters").SequenceEqual(attack.MonsterDamageBySlot)
                && CounterAt(0) < CounterAt(1) && CounterAt(1) < CounterAt(2),
                "Counterattacks retain each enemy's actual damage and original slot order");
            check(HealthAt(0, .1) == 600 && HealthAt(1, .1) == 240 && HealthAt(1, .5) == actual.Battle!.Monsters[0].Health,
                "Health bars retain the pre-action values until impact and settle on the recorded result");
            var remainingHealth = 600;
            for (var slot = 0; slot < 3; slot++)
            {
                remainingHealth -= attack.MonsterDamageBySlot[slot];
                check(HealthAt(0, CounterAt(slot) + .22) == remainingHealth,
                    $"Hero HP responds separately to counterattack {slot + 1}");
                Save(Frame(CounterAt(slot) + .05), $"counterattack-{slot + 1}.png");
            }
            var hit = Frame(.22); var response = Frame(.74); var settled = Frame(4);
            Save(hit, "enemy-hit.png"); Save(response, "hero-hit.png"); Save(settled, "settled.png");
            check(!hit.SequenceEqual(settled) && !response.SequenceEqual(settled), "Enemy and hero hit feedback appear before settling");
            check((from y in Enumerable.Range(101 * 4, 59 * 4) from x in Enumerable.Range(24 * 4, 272 * 4)
                select y * 1280 + x).All(i => hit[i] == response[i] && response[i] == settled[i]),
                "Combat effects keep result text and Continue clear and stationary with shake Off");
            check(Enumerable.Range(0, hit.Length).All(i => hit[i] == hit[i / 1280 / 4 * 4 * 1280 + i % 1280 / 4 * 4]),
                "Combat flashes, sparks and damage numbers retain exact native pixels");
            check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog))
                && battleBefore == JsonSerializer.Serialize(actual.Battle, json), "Drawing combat feedback preserves full gameplay, battle state and RNG");
            check(settled.SequenceEqual(Frame(6)), "Combat effects expire without leaving particles or floating numbers behind");

            Frame(0); var worldTime = (double)Get("sceneTime")!;
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.05)));
            check(Property<double>(Get("overlay")!, "CombatTime") == .05 && (double)Get("sceneTime")! == worldTime
                && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), "Combat feedback animates while exploration and gameplay stay frozen");
            Set("paused", true); Set("pauseAmount", 1f);
            Call("Update", new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.1)));
            check(Property<double>(Get("overlay")!, "CombatTime") == .05, "Pausing also freezes the combat feedback clock");
            Set("paused", false); Set("pauseAmount", 0f); Frame(.22);
            check((Point)Call("CombatShake")! == Point.Zero, "Screen shake Off gives an exact zero offset during a hit");
            Set("screenShake", true);
            var shake = (Point)Call("CombatShake")!;
            check(shake != Point.Zero && Math.Abs(shake.X) <= 1 && Math.Abs(shake.Y) <= 1
                && !hit.SequenceEqual(Frame(.22)), "Optional combat shake is bounded to one native pixel per axis");
            Frame(4); check((Point)Call("CombatShake")! == Point.Zero, "Screen shake settles with the hit");
            Set("screenShake", false);
            for (var frame = 0; frame < 96; frame++) Save(Frame(frame / 30d), $"animation-{frame:D3}.png");
            Choose("Continue");
            check(Property<object?>(Get("overlay")!, "Presentation") is null, "Continuing to the next combat choice clears all transient hit data");

            var missSeed = Enumerable.Range(1, 200).Select(value => (uint)value).First(value => Fixture(false, value).Attack().PlayerDamage == 0);
            actual = Fixture(seed: missSeed); expected = Fixture(false, missSeed);
            attack = expected.Attack(); Choose("Attack"); Result(expected, [0, 0, 0], attack.MonsterDamage, "Zero-damage attack");
            Set("screenShake", true); Frame(.22);
            check((Point)Call("CombatShake")! == Point.Zero && HealthAt(1, .5) == 240,
                "A zero-damage attack shows its neutral outcome without a false hit, HP loss or screen shake");
            Save(Frame(.3), "zero-damage.png"); Set("screenShake", false);

            actual = Fixture(); Choose("Attack");
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            audio.GetType().GetProperty("Enabled")!.SetValue(audio, true);
            var voices = (List<SoundEffectInstance>)audio.GetType().GetField("voices", flags)!.GetValue(audio)!;
            Call("UpdateCombatPresentation", .1d);
            check(voices.Count == 0, "Attack sound waits for the animated impact");
            Call("UpdateCombatPresentation", .12d); var hitSound = voices.Single();
            check(hitSound.State == SoundState.Playing, "Attack impact starts the original hit sound");
            var firstHit = Array.FindIndex(Property<int[]>(Presentation(), "DamageFromMonsters"), damage => damage > 0);
            Call("UpdateCombatPresentation", CounterAt(firstHit) - .22 + .01);
            check(voices.Count == 2 && voices.All(voice => voice.State == SoundState.Playing) && !hitSound.IsDisposed,
                "Enemy counterattack audio overlaps the player's impact instead of cutting it off");
            var played = voices.ToArray(); Choose("Continue", finish: false);
            check(voices.SequenceEqual(played) && Property<bool>(Get("overlay")!, "ContinueRequested"),
                "Early Continue remembers the request without replaying an impact already heard");
            var pendingTime = Property<double>(Get("overlay")!, "CombatTime");
            Set("paused", true); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(.2)));
            check(Property<double>(Get("overlay")!, "CombatTime") == pendingTime,
                "Pause freezes a buffered combat completion without losing its request");
            Set("paused", false); Set("pauseAmount", 0f);
            FinishConfirmed();

            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            actual = Fixture(); expected = Fixture(false);
            for (var round = 0; round < 3; round++)
            {
                attack = expected.Attack(); Choose("Attack");
                Call("UpdateCombatPresentation", .02d);
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                battleBefore = JsonSerializer.Serialize(actual.Battle, json);
                var result = Get("overlay");
                Choose("Continue", finish: false);
                check(ReferenceEquals(result, Get("overlay")) && Property<bool>(result!, "ContinueRequested"),
                    $"Rapid confirm {round + 1} buffers Continue while retaining the hit animation");
                var frames = 0;
                while (ReferenceEquals(result, Get("overlay")) && frames++ < 60)
                {
                    Choose("Continue", finish: false);
                    Call("UpdateCombatPresentation", 1d / 60);
                }
                check(frames is > 3 and < 60 && Property<object?>(Get("overlay")!, "Presentation") is null
                    && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog))
                    && battleBefore == JsonSerializer.Serialize(actual.Battle, json)
                    && snapshot.SequenceEqual(GameSaveCodec.Encode(expected.Session, catalog)),
                    $"Rapid confirm {round + 1} finishes within one second without queued attacks or changed combat/RNG");
            }
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            actual = Fixture(); Choose("Attack");
            Call("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            var pendingBack = Get("overlay"); Call("UpdateCombatPresentation", .08d);
            check(voices.Count == 1 && voices[0].State == SoundState.Playing
                && ReferenceEquals(pendingBack, Get("overlay")) && Property<bool>(pendingBack!, "ContinueRequested"),
                "Back also buffers completion and retains the animated impact with its sound");
            FinishConfirmed();

            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            actual = Fixture(seed: missSeed);
            Choose("Attack"); Choose("Continue", finish: false); Call("UpdateCombatPresentation", .1d);
            check(voices.Count == 0, "Fast-forwarding a zero-damage attack does not invent an impact sound");
            FinishConfirmed(); audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);

            foreach (var fps in new[] { 24, 60 })
            {
                actual = Fixture(); actual.Battle!.Monsters[0].Health = 1;
                actual.Battle.Monsters[1] = new(); actual.Battle.Monsters[2] = new();
                Choose("Attack"); var result = Get("overlay")!;
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                Choose("Continue", finish: false);
                SoundEffectInstance? deathSound = null;
                var deathCues = 0; var total = 0;
                var frames = 0;
                while (ReferenceEquals(result, Get("overlay")) && frames++ < fps)
                {
                    // Every simulated frame receives another Enter, as in aggressive button mashing.
                    Choose("Continue", finish: false);
                    var beforeTime = Property<double>(result, "CombatTime");
                    var beforeVoices = voices.Count;
                    Call("UpdateCombatPresentation", 1d / fps);
                    var afterTime = Property<double>(result, "CombatTime");
                    total += voices.Count - beforeVoices;
                    if (beforeTime < .4 && afterTime >= .4)
                    {
                        deathCues += voices.Count - beforeVoices;
                        deathSound = voices[^1];
                    }
                }
                check(frames >= fps / 4 && frames < fps / 2 && Get("overlay") is null && total == 3
                    && deathCues == 1 && deathSound is { IsDisposed: false, State: SoundState.Playing },
                    $"Rapid victory at {fps} FPS shows a brief defeat and plays impact, death and reward once; the death tail survives exit");
                check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)),
                    $"Buffered victory at {fps} FPS never grants rewards or advances gameplay twice");
                audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            }

            actual = Fixture();
            foreach (var monster in actual.Battle!.Monsters) monster.Health = 1;
            Choose("Spells"); Choose(Spells.Definitions[6].Name); Choose("High power");
            var areaResult = Get("overlay")!;
            check(Property<CombatTurn>(Presentation(), "Turn").Outcome == CombatOutcome.Victory,
                "Rapid area-defeat fixture kills all three enemies");
            Choose("Continue", finish: false); Call("UpdateCombatPresentation", .13d);
            var deaths = new List<SoundEffectInstance>();
            for (var slot = 0; slot < 3; slot++)
            {
                Call("UpdateCombatPresentation", .08d / 3);
                deaths.Add(voices[^1]);
            }
            check(deaths.Distinct().Count() == 3 && deaths.All(voice => voice.State == SoundState.Playing),
                "A fast-forwarded area kill retains one distinct death sound per defeated monster");
            FinishConfirmed();
            check(Get("overlay") is null && deaths.All(voice => !voice.IsDisposed),
                "Reward audio and battle exit preserve all three monster-death tails");
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);

            actual = Fixture(); actual.Battle!.Monsters[0].Health = 1;
            actual.Battle.Monsters[1] = new(); actual.Battle.Monsters[2] = new();
            Choose("Attack"); Call("UpdateCombatPresentation", .5d);
            var existingDeath = voices[^1];
            Choose("Continue");
            check(voices.Count == 3 && voices.Contains(existingDeath) && !existingDeath.IsDisposed,
                "Confirming after the death cue adds only rewards and never duplicates the monster sound");
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);

            // Stress the real audio device without waiting between requests or queuing stale combat sounds.
            var started = new List<SoundEffectInstance>();
            var bounded = true;
            for (var index = 0; index < 24; index++)
            {
                audio.GetType().GetMethod("Play")!.Invoke(audio, [130, true]);
                started.Add(voices[^1]);
                bounded &= voices.Count <= 4 && voices[^1].State == SoundState.Playing;
            }
            check(bounded && started.Take(20).All(voice => voice.IsDisposed) && voices.Count == 4,
                "A 24-hit burst starts immediately and retires oldest voices to stay bounded at four");
            audio.GetType().GetProperty("EffectsVolume")!.SetValue(audio, .25f);
            audio.GetType().GetMethod("Update")!.Invoke(audio, [true]);
            check(voices.All(voice => Math.Abs(voice.Volume - Property<float>(audio, "Volume") * .25f) < .0001f),
                "Effects volume changes apply to every overlapping combat voice");
            audio.GetType().GetProperty("EffectsVolume")!.SetValue(audio, 0f);
            audio.GetType().GetMethod("Update")!.Invoke(audio, [true]);
            check(voices.Count == 0 && started.All(voice => voice.IsDisposed),
                "Muting effects releases every overlapping voice");
            audio.GetType().GetProperty("EffectsVolume")!.SetValue(audio, oldEffectsVolume);
            for (var index = 0; index < 3; index++) audio.GetType().GetMethod("Play")!.Invoke(audio, [130, true]);
            played = voices.ToArray(); audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            check(voices.Count == 0 && played.All(voice => voice.IsDisposed),
                "Focus loss stops and releases all combat voices");
            audio.GetType().GetMethod("Play")!.Invoke(audio, [130, true]);
            var completedVoice = voices.Single();
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (completedVoice.State == SoundState.Playing && deadline.Elapsed.TotalSeconds < 3) Thread.Sleep(10);
            audio.GetType().GetMethod("Update")!.Invoke(audio, [true]);
            check(voices.Count == 0 && completedVoice.IsDisposed,
                "Completed impact audio is reclaimed by the regular update");
            for (var index = 0; index < 2; index++) audio.GetType().GetMethod("Play")!.Invoke(audio, [130, true]);
            played = voices.ToArray(); audio.GetType().GetMethod("Play")!.Invoke(audio, [131, false]);
            check(voices.Count == 1 && played.All(voice => voice.IsDisposed),
                "Noncombat cues retain their existing replacement behavior");
            audio.GetType().GetProperty("Enabled")!.SetValue(audio, false);
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            actual = Fixture(); Choose("Attack"); Choose("Continue");
            check(voices.Count == 0, "Rapid confirms respect Sound Off");

            foreach (var quick in new[] { false, true })
            {
                actual = Fixture(); actual.Battle!.Monsters[0].Health = 1;
                actual.Battle.Monsters[1] = new(); actual.Battle.Monsters[2] = new();
                Choose("Attack"); var result = Get("overlay")!;
                var variant = quick ? "quick" : "normal";
                for (var frame = 0; frame < 90; frame++)
                {
                    if (quick && frame >= 3 && ReferenceEquals(result, Get("overlay")))
                        Choose("Continue", finish: false);
                    Call("UpdateCombatPresentation", 1d / 60);
                    Save(Frame(Get("overlay") is { } shown ? Property<double>(shown, "CombatTime") : 0),
                        $"{variant}-victory-{frame:D3}.png");
                }
            }

            actual = Fixture();
            var menu = Get("overlay")!;
            menu.GetType().GetProperty("Selected")!.SetValue(menu, 2);
            actual.Battle!.Monsters[1].Health = 0;
            snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            Call("UpdateOverlay", new InputFrame(new Point(-1, 0), false, false, false));
            check(actual.Battle.SelectedTarget == 2 && ReferenceEquals(menu, Get("overlay")) && Property<int>(menu, "Selected") == 2,
                "Left selects the previous living enemy without replacing the menu or selected command");
            Call("UpdateOverlay", new InputFrame(new Point(-1, 0), false, false, false));
            check(actual.Battle.SelectedTarget == 0 && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)),
                "Direct targeting wraps, skips dead enemies and consumes no turn, resource or RNG");
            actual = Fixture(); Choose("Spells"); Save(Frame(0), "spell-list-hud.png"); Choose("Insect Attack");
            var powerMenu = Get("overlay")!;
            check((bool)Call("CombatPreview", powerMenu, actual.Battle, 1)! && !(bool)Call("CombatPreview", powerMenu, actual.Battle, 2)!,
                "Low-power damage spells highlight only the selected target");
            Call("UpdateOverlay", new InputFrame(new Point(0, 1), false, false, false));
            check(Enumerable.Range(1, 3).All(actor => (bool)Call("CombatPreview", powerMenu, actual.Battle, actor)!),
                "High-power damage spells highlight all living targets before casting");
            Save(Frame(0), "area-target-preview.png");
            Call("UpdateOverlay", new InputFrame(new Point(1, 0), false, false, false));
            check(actual.Battle!.SelectedTarget == 1 && Property<int>(powerMenu, "Selected") == 1,
                "Left/right targeting preserves the chosen spell power");
            Choose("Back"); Choose("Small Heal");
            check((bool)Call("CombatPreview", Get("overlay"), actual.Battle, 0)!
                && !(bool)Call("CombatPreview", Get("overlay"), actual.Battle, 1)!, "Healing previews the hero, not an enemy");
            Save(Frame(0), "healing-preview.png");
            Choose("Back"); Choose("Rabbit Replacement");
            check((bool)Call("CombatPreview", Get("overlay"), actual.Battle, 3)!
                && !(bool)Call("CombatPreview", Get("overlay"), actual.Battle, 2)!, "Rabbit preview matches the core's last-companion replacement rule");
            Choose("Back"); Choose("Back"); Choose("Inventory"); Save(Frame(0), "inventory-hud.png"); Choose("Back");

            foreach (var submenu in new[] { "Spells", "Inventory" })
            {
                actual = Fixture();
                snapshot = GameSaveCodec.Encode(actual.Session, catalog);
                battleBefore = JsonSerializer.Serialize(actual.Battle, json);
                Choose(submenu);
                var escapeInput = new GameInput();
                Call("UpdateOverlay", escapeInput.Update(new KeyboardState(Keys.Escape), default, .016));
                check(Property<string>(Get("overlay")!, "Title") == "Combat"
                    && Property<object?>(Get("overlay")!, "Presentation") is null
                    && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog))
                    && battleBefore == JsonSerializer.Serialize(actual.Battle, json),
                    $"Escape returns from {submenu} without attempting to flee or taking a turn");
            }

            actual = Fixture(); actual.Battle!.SelectedTarget = 1;
            actual.Player.MonsterDefeats[2] = 7;
            actual.Player.MonsterDefeatHistoryComplete = false;
            var battleMenu = Get("overlay");
            Choose("Enemy info");
            var inspector = Get("overlay")!;
            snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            battleBefore = JsonSerializer.Serialize(actual.Battle, json);
            check(Property<int?>(inspector, "InspectedEnemy") == 1 && Property<bool>(inspector, "Combat")
                && !Property<bool>(inspector, "EnemyDetails") && Property<object?>(inspector, "Presentation") is null,
                "Enemy info opens on the selected enemy's focused stats view without an action animation");
            var overview = Frame(0); Save(overview, "enemy-info.png");
            actual.Player.Health--; actual.Player.SpellPoints--; actual.Player.AbilityPoints--;
            actual.Battle.Monsters[0].Health--;
            check(overview.SequenceEqual(Frame(0)), "Enemy info excludes hero resources and other opponents from the focused card");
            actual.Player.Health++; actual.Player.SpellPoints++; actual.Player.AbilityPoints++;
            actual.Battle.Monsters[0].Health++;
            var inspectedMonster = actual.Battle.Monsters[1];
            inspectedMonster.Health--; inspectedMonster.Rank++; inspectedMonster.AttackRating++; inspectedMonster.DefenseRating++;
            var changedStats = Frame(0);
            check(new[] { 56, 85, 104, 123 }.All(y => !Region(overview, new(134, y, 162, 7)).SequenceEqual(Region(changedStats, new(134, y, 162, 7))))
                && Region(overview, new(20, 48, 96, 96)).SequenceEqual(Region(changedStats, new(20, 48, 96, 96))),
                "The spaced HP, rank, attack and defense rows render live encounter values independently of the portrait");
            inspectedMonster.Health++; inspectedMonster.Rank--; inspectedMonster.AttackRating--; inspectedMonster.DefenseRating--;
            Choose("Details"); var detailsFrame = Frame(0); Save(detailsFrame, "enemy-info-details.png");
            check(Property<bool>(inspector, "EnemyDetails") && !detailsFrame.SequenceEqual(overview)
                && Region(overview, new(20, 48, 96, 96)).SequenceEqual(Region(detailsFrame, new(20, 48, 96, 96))),
                "Details replaces the stats with secondary information while keeping the same enemy portrait");
            inspectedMonster.SpecialRating = 1;
            actual.Player.MonsterDefeats[2]++; actual.Player.MonsterDefeatHistoryComplete = true;
            var changedDetails = Frame(0);
            check(new[] { 68, 87, 99 }.All(y => !Region(detailsFrame, new(134, y, 162, 7)).SequenceEqual(Region(changedDetails, new(134, y, 162, 7)))),
                "Details reflects spell resistance, incomplete-history labeling and the recorded defeat count");
            inspectedMonster.SpecialRating = 0;
            actual.Player.MonsterDefeats[2]--; actual.Player.MonsterDefeatHistoryComplete = false;
            Choose("Stats");
            check(!Property<bool>(inspector, "EnemyDetails") && overview.SequenceEqual(Frame(0)), "Stats returns to the same uncluttered overview");
            Choose("Details");
            Call("UpdateOverlay", new InputFrame(new Point(1, 0), false, false, false));
            check(Property<int?>(inspector, "InspectedEnemy") == 2 && actual.Battle.SelectedTarget == 1
                && Property<bool>(inspector, "EnemyDetails") && !detailsFrame.SequenceEqual(Frame(0)),
                "Browsing changes the inspected enemy, retains the Details view, and leaves the attack target unchanged");
            Call("UpdateOverlay", new InputFrame(new Point(1, 0), false, false, false));
            check(Property<int?>(inspector, "InspectedEnemy") == 0, "Enemy inspection wraps between living opponents");
            Auto(true); var inspectionFrame = Frame(0);
            Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(20)));
            check(ReferenceEquals(inspector, Get("overlay")) && inspectionFrame.SequenceEqual(Frame(0))
                && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)) && battleBefore == JsonSerializer.Serialize(actual.Battle, json),
                "Inspection consumes no turn, resource, gameplay/RNG state or battle selection, even with Auto pacing");
            Choose("Back");
            check(ReferenceEquals(battleMenu, Get("overlay")) && Property<int>(Get("overlay")!, "Selected") == 4
                && actual.Battle.SelectedTarget == 1, "Back restores the same command and original attack target");
            Auto(false);
            actual.Battle.Monsters[0].Health = 0; actual.Battle.Monsters[2] = new();
            Choose("Enemy info"); inspector = Get("overlay")!;
            Call("UpdateOverlay", new InputFrame(new Point(-1, 0), false, false, false));
            Call("UpdateOverlay", new InputFrame(new Point(1, 0), false, false, false));
            check(Property<int?>(inspector, "InspectedEnemy") == 1, "Inspection skips defeated and empty slots and stays on the sole survivor");
            Call("UpdateOverlay", new InputFrame(Point.Zero, false, true, false));
            check(ReferenceEquals(battleMenu, Get("overlay")), "Escape/controller Back also restores the battle command menu");

            actual = Fixture(); Choose("Spells"); Choose("Rabbit Replacement"); Choose("Low power"); Choose("Continue");
            actual.Battle!.SelectedTarget = 2; Choose("Enemy info");
            var rabbitFrame = Frame(0); Save(rabbitFrame, "enemy-info-rabbit.png");
            var rabbit = actual.Battle.Monsters[2]; rabbit.AttackRating = 40;
            check(rabbit.Name == "Small Bunny" && !Region(rabbitFrame, new(134, 104, 162, 7)).SequenceEqual(Region(Frame(0), new(134, 104, 162, 7))),
                "Transformed-rabbit inspection uses the current encounter rating instead of catalog or pre-transformation stats");
            rabbit.AttackRating = 5;
            Choose("Details"); Save(Frame(0), "enemy-info-rabbit-details.png"); Choose("Back");
            actual = Fixture();
            actual.Battle!.Monsters[0] = Combat.StartMonsterEncounter(41, 1, new MsvcRandom(1),
                new EncounterSetup { PlayerLevel = 45, Difficulty = 3, GatekeeperState = 1 }).Monsters[0];
            Choose("Enemy info"); Save(Frame(0), "enemy-info-empowered.png"); Choose("Details");
            var empoweredFrame = Frame(0); actual.Battle.Monsters[0].GatekeeperEmpowered = false;
            check(!Region(empoweredFrame, new(134, 118, 162, 19)).SequenceEqual(Region(Frame(0), new(134, 118, 162, 19))),
                "The empowered trait appears in Details instead of crowding the primary stats");
            actual.Battle.Monsters[0].GatekeeperEmpowered = true;
            Save(Frame(0), "enemy-info-empowered-details.png");
            var inspectedFrame = Frame(0);
            check(Enumerable.Range(0, inspectedFrame.Length).All(i => inspectedFrame[i]
                == inspectedFrame[i / 1280 / 4 * 4 * 1280 + i % 1280 / 4 * 4]), "Enemy information retains exact native pixel scaling");
            Choose("Back");

            actual = Fixture(); expected = Fixture(false);
            actual.Battle!.SelectedTarget = expected.Battle!.SelectedTarget = 1;
            actual.Battle.Monsters[1].Health = expected.Battle.Monsters[1].Health = 1;
            attack = expected.Attack(); Choose("Attack");
            Result(expected, [0, attack.PlayerDamage, 0], attack.MonsterDamage, "Defeated target");
            check(actual.Battle.SelectedTarget != 1 && Property<int[]>(Presentation(), "DamageToMonsters")[1] > 0,
                "A killing blow stays attached to the struck slot after combat selects another target");
            Save(Frame(.22), "defeated-target.png");

            foreach (var (spell, power) in new[] { (2, SpellPower.Low), (3, SpellPower.Low), (3, SpellPower.High), (1, SpellPower.Low), (5, SpellPower.High), (6, SpellPower.High), (7, SpellPower.High), (9, SpellPower.Low), (9, SpellPower.High), (10, SpellPower.Low), (11, SpellPower.Low) })
            {
                actual = Fixture(); expected = Fixture(false);
                var cast = expected.CastCombat(spell, power);
                Choose("Spells"); Choose(Spells.Definitions[spell].Name); Choose(power == SpellPower.High ? "High power" : "Low power");
                Result(expected, cast.DamageBySlot, cast.Turn.MonsterDamage, $"Spell {spell}/{power}");
                check(HealthAt(0, 4) == actual.Player.Health
                    && Property<int>(Presentation().GetType().GetMethod("ResourcesAt")!.Invoke(Presentation(), [4d])!, "SP") == actual.Player.SpellPoints,
                    $"Spell {spell}/{power}: animated health and resources settle on actual gameplay values");
                if (spell == 3 && power == SpellPower.High)
                {
                    check(Property<int[]>(Presentation(), "DamageToMonsters").All(damage => damage > 0),
                        "Area spells show individual damage for all three struck monsters");
                    Save(Frame(.26), "area-spell.png");
                }
                Save(Frame(.3), $"spell-{spell}-{power}.png");
                if (spell is 1 or 5 or 9 or 10 or 11)
                    check(Property<int[]>(Presentation(), "DamageToMonsters").All(damage => damage == 0),
                        $"Non-damaging spell {spell} creates no false enemy hits from healing, shuffle or transformation");
            }
            foreach (var ability in new[] { SpecialAbility.Backstab, SpecialAbility.Frighten, SpecialAbility.Invisibility, SpecialAbility.Enrage })
            {
                actual = Fixture(); expected = Fixture(false);
                actual.Player.SpecialAbility = expected.Player.SpecialAbility = ability;
                var turn = expected.UseAbility(); Choose("Ability:");
                Result(expected, [turn.PlayerDamage, 0, 0], turn.MonsterDamage, $"Ability {ability}");
                check(!Property<bool>(Presentation(), "Counters"), $"Ability {ability} preserves the original no-counterattack rule");
                Save(Frame(.3), $"ability-{ability}.png");
            }
            actual = Fixture(); expected = Fixture(false);
            var item = expected.UseCombatItem(0);
            Choose("Inventory"); Choose(ItemNames.Consumable(1)); Choose("Use");
            Result(expected, [0, 0, 0], item.Turn!.MonsterDamage, "Item counterattack");
            check(HealthAt(0, .5) > 600 && HealthAt(0, 4) == actual.Player.Health,
                "Item healing is displayed before the enemies retaliate");
            Save(Frame(.4), "healing-item.png");
            foreach (var itemId in new[] { 9, 10 })
            {
                actual = Fixture(); expected = Fixture(false);
                actual.Player.Inventory[0] = expected.Player.Inventory[0] = (ushort)itemId;
                item = expected.UseCombatItem(0); Choose("Inventory"); Choose(ItemNames.Consumable(itemId)); Choose("Use");
                Result(expected, [0, 0, 0], item.Turn!.MonsterDamage, $"Maximum-resource item {itemId}");
                var shownResources = Presentation().GetType().GetMethod("ResourcesAt")!.Invoke(Presentation(), [.5d])!;
                check(itemId == 9 ? HealthAt(0, .5) == 600 + item.Item.Amount
                    : Property<int>(shownResources, "MaximumSP") == actual.Player.MaximumSpellPoints,
                    $"Maximum-resource item {itemId} updates the HUD at use, before counterattacks");
            }

            foreach (var escape in new[] { true, false })
            {
                var seed = Enumerable.Range(1, 200).Select(value => (uint)value).First(value => Fixture(false, value).Flee().Escaped == escape);
                actual = Fixture(seed: seed); expected = Fixture(false, seed);
                var flee = expected.Flee();
                Call("UpdateOverlay", new GameInput().Update(new KeyboardState(Keys.Escape), default, .016));
                Result(expected, [], flee.MonsterDamage, $"Flee escaped={escape}");
                Auto(true); var resultOverlay = Get("overlay");
                Call("UpdateCombatPresentation", 20d);
                check(escape ? ReferenceEquals(resultOverlay, Get("overlay")) : Property<object?>(Get("overlay")!, "Presentation") is null,
                    "Auto pacing returns from failed escape but awaits confirmation after a successful escape");
                Auto(false);
            }
            actual = Fixture(); expected = Fixture(false);
            actual.Player.Health = expected.Player.Health = 1;
            attack = expected.Attack(); Choose("Attack");
            Result(expected, [attack.PlayerDamage, 0, 0], attack.MonsterDamage, "Defeat");
            check(actual.BattleResult == BattleResolution.Defeat && Property<bool>(Get("overlay")!, "Combat"),
                "A defeated hero retains the combat stage for its final hit response");
            Save(Frame(1.8), "defeat.png");
            Auto(true); var defeat = Get("overlay"); Call("UpdateCombatPresentation", 20d);
            check(ReferenceEquals(defeat, Get("overlay")), "Defeat always waits for confirmation under Auto pacing"); Auto(false);
            actual = Fixture(); expected = Fixture(false);
            foreach (var adventure in new[] { actual, expected })
            {
                adventure.Battle!.Monsters[0].Health = 1;
                adventure.Battle.Monsters[1] = new(); adventure.Battle.Monsters[2] = new();
            }
            attack = expected.Attack(); Choose("Attack");
            Result(expected, [attack.PlayerDamage, 0, 0], attack.MonsterDamage, "Victory");
            check(Property<string>(Get("overlay")!, "Title") == "Victory", "Victory keeps its final hit and reward text together");
            Save(Frame(1), "victory.png"); Choose("Continue");
            check(Get("overlay") is null, "Dismissing the final result removes the combat presentation");
            Call("ShowActions"); check(Frame(0).SequenceEqual(Frame(.5)), "No hit animation leaks into unrelated overlays");

            actual = Fixture(); expected = Fixture(false);
            foreach (var adventure in new[] { actual, expected })
            {
                adventure.Player.Experience = Player.ExperienceRequiredForNextLevel(adventure.Player.Level)!.Value;
                adventure.Battle!.Monsters[0].Health = 1;
                adventure.Battle.Monsters[1] = new(); adventure.Battle.Monsters[2] = new();
            }
            attack = expected.Attack(); Choose("Attack");
            Result(expected, [attack.PlayerDamage, 0, 0], 0, "Level-up victory");
            check(attack.LevelUp.LeveledUp && HealthAt(0, 4) == actual.Player.Health, "Level-up celebration uses the real level-up and final HP");
            check(Property<int>(Presentation().GetType().GetMethod("ResourcesAt")!.Invoke(Presentation(), [.5d])!, "SP") == 200,
                "Level-up resource gains appear with the reward celebration, after the final impact");
            Save(Frame(.9), "level-up.png");
            Auto(true); var victory = Get("overlay"); snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            Call("UpdateCombatPresentation", 20d);
            check(ReferenceEquals(victory, Get("overlay")), "Victory and paged rewards always wait for confirmation under Auto pacing");
            Choose("Continue"); check(snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), "Acknowledging rewards never grants them twice");

            actual = Fixture(); Auto(false); Choose("Attack");
            var manual = Get("overlay"); Call("UpdateCombatPresentation", 20d);
            check(ReferenceEquals(manual, Get("overlay")), "Manual pacing keeps ordinary results until confirmed");
            actual = Fixture(); Choose("Attack"); Auto(true);
            var automatic = Get("overlay"); Frame(0); snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            Call("UpdateCombatPresentation", .4d);
            check(ReferenceEquals(automatic, Get("overlay")), "Auto pacing waits for counterattacks and readable results");
            Set("paused", true); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(20)));
            check(ReferenceEquals(automatic, Get("overlay")) && Property<double>(automatic!, "CombatTime") == .4,
                "Pausing suspends Auto progression and all combat animation");
            Set("paused", false); Set("pauseAmount", 0f); Call("Update", new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(20)));
            check(Property<object?>(Get("overlay")!, "Presentation") is null && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)),
                "Auto returns once to commands without taking an action or advancing gameplay/RNG");
            var returned = Get("overlay"); Call("UpdateCombatPresentation", 20d);
            check(ReferenceEquals(returned, Get("overlay")), "The command menu never auto-selects its next action");
            actual = Fixture(); Choose("Attack");
            var longPresentation = Presentation();
            Call("ShowTurn", Property<CombatTurn>(longPresentation, "Turn"), longPresentation,
                "One\nTwo\nThree\nFour\nFive\nSix");
            var longResult = Get("overlay"); Call("UpdateCombatPresentation", 20d);
            check(ReferenceEquals(longResult, Get("overlay")) && Property<int>(longResult!, "PageCount") > 1,
                "Auto pacing never discards a multi-page ongoing result");
            actual = Fixture(); actual.Player.SpellPoints = 0;
            snapshot = GameSaveCodec.Encode(actual.Session, catalog);
            Choose("Spells"); Choose("Insect Attack"); Choose("High power");
            var failure = Get("overlay"); Call("UpdateCombatPresentation", 20d);
            check(ReferenceEquals(failure, Get("overlay")) && Property<bool>(failure!, "Combat")
                && snapshot.SequenceEqual(GameSaveCodec.Encode(actual.Session, catalog)), "Failed casts retain the battle HUD and await confirmation without spending resources");
            Auto(false);

            Set("overlay", null); Set("paused", true); Set("pauseAmount", 1f); Set("pausePage", "Display"); Set("selected", 4); Set("selectionY", 48f);
            for (var i = 0; i < 2; i++)
            {
                Call("ActivateMenu");
                var profile = Get("profile")!;
                var path = (string)profile.GetType().GetField("path", flags)!.GetValue(profile)!;
                var reopened = Activator.CreateInstance(profile.GetType(), [path])!;
                check((bool)Get("screenShake")! == (i == 0)
                    && Property<bool>(Property<object>(reopened, "Data"), "ScreenShake") == (i == 0),
                    $"Screen shake menu persists {(i == 0 ? "On" : "Off")} independently");
            }
            Save(Frame(0), "screen-shake-menu.png");
            Set("pausePage", "Options"); Set("selected", 3); Set("selectionY", 36f);
            for (var i = 0; i < 2; i++)
            {
                Call("ActivateMenu");
                var profile = Get("profile")!;
                var path = (string)profile.GetType().GetField("path", flags)!.GetValue(profile)!;
                var reopened = Activator.CreateInstance(profile.GetType(), [path])!;
                check(Property<bool>(profileData, "AutoBattle") == (i == 0)
                    && Property<bool>(Property<object>(reopened, "Data"), "AutoBattle") == (i == 0),
                    $"Battle pacing independently persists {(i == 0 ? "Auto" : "Manual")}");
            }
            Save(Frame(0), "battle-pacing-menu.png");
        }
        finally
        {
            Auto(wasAutomatic);
            foreach (var entry in saved) Set(entry.Key, entry.Value);
            audio.GetType().GetMethod("Update")!.Invoke(audio, [false]);
            audio.GetType().GetProperty("EffectsVolume")!.SetValue(audio, oldEffectsVolume);
            audio.GetType().GetProperty("Enabled")!.SetValue(audio, wasAudible);
            Call("SavePreferences"); Set("menuMessage", saved["menuMessage"]);
        }

        IEnumerable<Color> Region(Color[] frame, Rectangle bounds) =>
            from y in Enumerable.Range(bounds.Y, bounds.Height)
            from x in Enumerable.Range(bounds.X, bounds.Width)
            select frame[y * 4 * 1280 + x * 4];

        Adventure Fixture(bool install = true, uint? seed = null)
        {
            var hero = new Player { Name = "Combat Check", Health = 600, MaximumHealth = 700, Level = 15,
                Strength = 40, Dexterity = 1, Intelligence = 35, WeaponRank = 6, SpellPoints = 200,
                MaximumSpellPoints = 200, AbilityPoints = 9, MaximumAbilityPoints = 9 };
            hero.Inventory[0] = 1;
            foreach (var index in Enumerable.Range(0, 12)) hero.Spells[index] = 1;
            var session = new GameSession(catalog, hero, randomState: seed ?? combatSeed);
            var adventure = new Adventure(session);
            var battle = new CombatEncounter();
            for (var i = 0; i < 3; i++) battle.Monsters[i] = new Monster { TypeId = i + 2,
                Name = CombatCatalog.Monster(i + 2).Name, Health = 240 + i * 30, MaximumHealth = 240 + i * 30,
                AttackRating = 40, DefenseRating = 1, Rank = 1 };
            typeof(Adventure).GetProperty("Battle")!.SetValue(adventure, battle);
            if (install) { Set("session", session); Set("adventure", adventure); Call("ShowBattle"); }
            return adventure;
        }
        void Result(Adventure expected, int[] dealt, int received, string label)
        {
            var actual = (Adventure)Get("adventure")!; var current = Get("overlay")!;
            check(Property<bool>(current, "Combat") && Property<int[]>(Presentation(), "DamageToMonsters").SequenceEqual(dealt.Length == 0 ? new int[3] : dealt)
                && Property<int[]>(Presentation(), "DamageFromMonsters").Sum() == received, $"{label}: feedback uses the actual per-target combat results");
            check(GameSaveCodec.Encode(actual.Session, catalog).SequenceEqual(GameSaveCodec.Encode(expected.Session, catalog))
                && JsonSerializer.Serialize(actual.Battle, json) == JsonSerializer.Serialize(expected.Battle, json)
                && actual.BattleResult == expected.BattleResult, $"{label}: UI actions exactly match core gameplay and RNG");
        }
        void Choose(string label, bool finish = true)
        {
            var current = Get("overlay")!;
            current.GetType().GetProperty("Page")!.SetValue(current, Property<int>(current, "PageCount") - 1);
            var options = Property<Array>(current, "Options").Cast<object>().ToArray();
            var index = Array.FindIndex(options, option => Property<string>(option, "Label").StartsWith(label, StringComparison.Ordinal));
            if (index < 0) throw new InvalidOperationException("Missing combat option: " + label);
            current.GetType().GetProperty("Selected")!.SetValue(current, index);
            Call("UpdateOverlay", new InputFrame(Point.Zero, true, false, false));
            if (finish) FinishConfirmed();
        }
        void FinishConfirmed()
        {
            var current = Get("overlay");
            for (var frame = 0; current is not null && ReferenceEquals(current, Get("overlay"))
                && Property<bool>(current, "ContinueRequested") && frame < 120; frame++)
                Call("UpdateCombatPresentation", 1d / 60);
        }
        Color[] Frame(double time)
        {
            Get("overlay")?.GetType().GetProperty("CombatTime")!.SetValue(Get("overlay"), time);
            Call("Draw", new GameTime());
            var pixels = new Color[1280 * 720]; game.GraphicsDevice.GetBackBufferData(pixels); return pixels;
        }
        void Save(Color[] pixels, string name)
        {
            capture.SetData(pixels); using var file = File.Create(Path.Combine(directory, name)); capture.SaveAsPng(file, 1280, 720);
        }
    }
}
