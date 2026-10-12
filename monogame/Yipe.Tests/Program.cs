using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Yipe.Core;
using Yipe.Desktop;

if (args.Length is 2 or 3 && args[0] is "--campaign-prefix" or "--campaign-route")
{
    CampaignRouteChecks.Run(args[1], args[0] == "--campaign-route", args.Length == 3 ? args[2] : null);
    return;
}

if (args.Length == 2 && args[0] is "--write-opening-fixture" or "--read-opening-fixture")
{
    OpeningRouteChecks.Run((passed, label) => { if (!passed) throw new InvalidOperationException(label); }, args[0], args[1]);
    return;
}

if (args.Length == 2 && args[0] is "--write-import-fixtures" or "--read-import-fixtures")
{
    await PersistenceChecks.Restart(args[0], args[1]);
    return;
}

if (args.Length == 2 && args[0] is "--write-save-fixture" or "--read-save-fixture")
{
    await GameSaveChecks.Restart(args[0], args[1]);
    return;
}

if (args.Length != 0 && (args.Length != 2 || args[0] is not ("--player-trace" or "--map-trace" or "--session-trace" or "--services-trace" or "--exploration-trace" or "--combat-trace" or "--quest-trace" or "--encounters-trace" or "--scores-trace" or "--items-trace")))
    throw new ArgumentException("Usage: Yipe.Tests [--player-trace <output-file> | --map-trace <output-file> | --session-trace <output-file> | --services-trace <output-file> | --exploration-trace <output-file> | --combat-trace <output-file> | --quest-trace <output-file> | --encounters-trace <output-file> | --scores-trace <output-file> | --items-trace <output-file>]");

var checks = 0;
ItemNameChecks.Run(Check, args.Length == 2 && args[0] == "--items-trace" ? args[1] : null);
ProfileChecks.Run(Check);
RainWeatherChecks.Run(Check);
DayNightClockChecks.Run(Check);
FootstepAssetChecks.Run(Check);
ControlChecks.Run(Check);
IceMotionChecks.Run(Check);
await MapJournalChecks.Run(Check);
ScoreChecks.Run(Check, args.Length == 2 && args[0] == "--scores-trace" ? args[1] : null);
OpeningRouteChecks.Run(Check);
await OpeningRouteChecks.Restart(Check);
var encounterRecords = EncounterChecks.Run(Check, args.Length == 2 && args[0] == "--encounters-trace" ? args[1] : null);
var questRecords = QuestChecks.Run(Check, args.Length == 2 && args[0] == "--quest-trace" ? args[1] : null);
var combatRecords = CombatChecks.Run(Check, args.Length == 2 && args[0] == "--combat-trace" ? args[1] : null);
var explorationRecords = ExplorationChecks.Run(Check, args.Length == 2 && args[0] == "--exploration-trace" ? args[1] : null);
var serviceRecords = ServiceChecks.Run(Check, args.Length == 2 && args[0] == "--services-trace" ? args[1] : null);
var playerRecords = PlayerChecks.Run(Check, args.Length == 2 && args[0] == "--player-trace" ? args[1] : null);
var mapRecords = MapChecks.Run(Check, args.Length == 2 && args[0] == "--map-trace" ? args[1] : null);
var sessionRecords = SessionChecks.Run(Check, args.Length == 2 && args[0] == "--session-trace" ? args[1] : null);
await GameSaveChecks.Run(Check);
await PersistenceChecks.Run(Check);
var defaultRandom = new MsvcRandom();
Check(defaultRandom.State == 1 && defaultRandom.Next() == 41 && defaultRandom.Next() == 18467
    && defaultRandom.Next() == 6334, "Default random seed reproduces the original sequence");

// random_reference.cpp calls the C++ implementation for every output and full 32-bit state.
// Includes zero, signed boundaries, overflowing states, and the generated-map regression seed.
uint[] seeds = [0, 1, 2, 0x7fffffff, 0x80000000, 0xffffffff, 0xdeadbeef, 2566201570];
var randomTrace = new StringBuilder();
foreach (var seed in seeds)
{
    var random = new MsvcRandom(seed);
    Check(random.State == seed, $"Constructing seed {seed} preserves its full state without drawing");
    for (var step = 0; step < 1024; step++)
    {
        var value = random.Next();
        randomTrace.Append(CultureInfo.InvariantCulture, $"{seed},{step},{value},{random.State}\n");
    }
}
Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(randomTrace.ToString()))).ToLowerInvariant()
    == "d7581cd4181ee25851126078623cb2b8cee4283505cd00255295a7b4fd1e7ff8",
    "All 8192 random outputs and states match the independently compiled C++ reference");

var uninterruptedRandom = new MsvcRandom(uint.MaxValue);
for (var i = 0; i < 517; i++) uninterruptedRandom.Next();
var restoredRandom = new MsvcRandom(uninterruptedRandom.State);
var continuationMatches = true;
for (var i = 0; i < 1024; i++)
    continuationMatches &= uninterruptedRandom.Next() == restoredRandom.Next()
        && uninterruptedRandom.State == restoredRandom.State;
Check(continuationMatches, "Restoring the complete RNG state preserves subsequent outputs and state");

using var mapStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "maps.json"));
var map = TownMap.Load(mapStream);

Check(map.Tile(new(5, 5)) == 2 && map.Tile(new(6, 6)) == 1
    && map.Tile(new(11, 10)) == 10, "Original column-major town coordinates");
Check(map.Move(new(5, 5), 1, 0) == new MovementResult(1, new(6, 5), true), "Move onto the starting bridge");
Check(map.Move(new(6, 5), 0, 1) == new MovementResult(8, new(6, 5), false), "Fred's shop entrance preserves position");
Check(!map.CanResume(new(0, 5)) && !map.CanResume(new(6, 6)) && !map.CanResume(new(int.MaxValue, 0)),
    "Saved positions cannot bypass town bounds or enter buildings");

// Expected digest comes from town_reference.cpp linked to the existing C++ yipe_core.
// All 156 starting cells and four cardinal directions: 624 independent reference cases.
var trace = new StringBuilder();
Point[] directions = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];
for (var x = 0; x < TownMap.Width; x++)
    for (var y = 0; y < TownMap.Height; y++)
        foreach (var direction in directions)
        {
            var result = map.Move(new(x, y), direction.X, direction.Y);
            trace.Append(CultureInfo.InvariantCulture,
                $"{x},{y},{direction.X},{direction.Y},{result.OriginalCode},{result.Position.X},{result.Position.Y},{(result.Moved ? 1 : 0)}\n");
        }
Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString()))).ToLowerInvariant()
    == "33cc54e7e4ab044418266b93a1af01a3c4b5f7e7d67c30d83fd9b31794309f9a",
    "All 624 town movements match the independent C++ reference");

var keyboard = new GameInput();
var typing = new GameInput();
Check(!typing.Update(new(Keys.Space), default, .016, textInput: true).Confirm,
    "Typing a space in a character name does not submit the text overlay");
Check(typing.Update(new(Keys.W), default, .016, textInput: true).Direction == Point.Zero
    && typing.Update(new(Keys.Enter), default, .016, textInput: true).Confirm,
    "Name entry reserves letters for text and Enter for confirmation");
Check(keyboard.Update(new(Keys.Right), default, 0.016).Direction == new Point(1, 0), "Keyboard moves immediately");
Check(keyboard.Update(new(Keys.Right), default, 0.1).Direction == Point.Zero, "Held movement waits before repeating");
Check(keyboard.Update(new(Keys.Right), default, 0.2).Direction == new Point(1, 0), "Held movement repeats after its delay");
Check(keyboard.Update(new(Keys.Up), default, 0.01).Direction == new Point(0, -1), "Changing direction moves immediately");
Check(keyboard.Update(new(Keys.Enter), default, 0.01).Confirm, "Keyboard confirmation has a rising edge");
Check(!keyboard.Update(new(Keys.Enter), default, 1).Confirm, "Held confirmation never repeats");
Check(keyboard.Update(new(Keys.M), default, .016).Map
    && !keyboard.Update(new(Keys.M), default, 1).Map, "Map key toggles once per press");
Check(!typing.Update(new(Keys.M), default, .016, textInput: true).Map, "Typing M in a name cannot open the map");
keyboard.Update(new(Keys.M), default, .016, active: false);
Check(!keyboard.Update(new(Keys.M), default, .016).Map, "Focus return cannot replay a held map key");
Check(keyboard.Update(new(Keys.Escape), default, 0.01).Pause, "Escape opens pause");
Check(!keyboard.Update(new(Keys.Escape), default, 0.01).Pause, "Held Escape cannot reopen pause");
keyboard.Update(new(Keys.Right, Keys.Enter), default, 10, active: false);
var resumed = keyboard.Update(new(Keys.Right, Keys.Enter), default, 0.016);
Check(resumed.Direction == Point.Zero && !resumed.Confirm, "Focus return does not replay held actions");

var controller = new GameInput();
var connected = Pad();
Check(connected.IsConnected, "Synthetic test controller is connected");
controller.Update(default, connected, 0);
Check(controller.Update(default, Pad(Buttons.DPadLeft), 0.016).Direction == new Point(-1, 0), "Controller D-pad moves");
Check(controller.Update(default, Pad(Buttons.A), 0.016).Confirm, "Controller south button selects");
Check(!controller.Update(default, Pad(Buttons.A), 1).Confirm, "Held controller selection never repeats");
Check(controller.Update(default, Pad(Buttons.B), 0.016).Back, "Controller east button returns");
Check(controller.Update(default, Pad(Buttons.Start), 0.016).Pause, "Controller Start opens pause");
Check(controller.Update(default, Pad(Buttons.Back), .016).Map
    && !controller.Update(default, Pad(Buttons.Back), 1).Map, "Controller View toggles the map once per press");
controller.Update(default, default, .016);
Check(!controller.Update(default, Pad(Buttons.Back), .016).Map, "Reconnect cannot replay a held map button");
Check(controller.Update(default, Pad(stick: new(0.1f, 0.1f)), 0.016).Direction == Point.Zero, "Analog dead zone filters drift");
Check(controller.Update(default, Pad(stick: new(0.8f, 0.2f)), 0.016).Direction == new Point(1, 0), "Analog stick chooses dominant horizontal axis");
Check(controller.Update(default, Pad(stick: new(0.2f, 0.8f)), 0.016).Direction == new Point(0, -1), "Analog vertical axis maps to screen coordinates");
Check(controller.Update(default, default, 0.016) == default, "Disconnect produces no phantom action");
Check(controller.Update(default, Pad(Buttons.A), 0.016) == default, "Reconnect with a held button produces no selection");

Point[] eightDirections = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];
foreach (var direction in eightDirections)
{
    var keys = new List<Keys>();
    var buttons = Buttons.None;
    if (direction.X < 0) { keys.Add(Keys.Left); buttons |= Buttons.DPadLeft; }
    if (direction.X > 0) { keys.Add(Keys.Right); buttons |= Buttons.DPadRight; }
    if (direction.Y < 0) { keys.Add(Keys.Up); buttons |= Buttons.DPadUp; }
    if (direction.Y > 0) { keys.Add(Keys.Down); buttons |= Buttons.DPadDown; }
    var directionalKeyboard = new GameInput();
    var state = new KeyboardState(keys.ToArray());
    Check(directionalKeyboard.Update(state, default, 0.016).Direction == direction,
        $"Keyboard supports direction {direction}");
    Check(directionalKeyboard.Update(state, default, 0.1).Direction == Point.Zero
        && directionalKeyboard.Update(state, default, 0.2).Direction == direction,
        $"Held direction {direction} uses the existing repeat delay");
    var directionalPad = new GameInput();
    directionalPad.Update(default, Pad(), 0);
    Check(directionalPad.Update(default, Pad(buttons), 0.016).Direction == direction,
        $"Controller D-pad supports direction {direction}");
    directionalPad.Update(default, Pad(), 0.016);
    Check(directionalPad.Update(default, Pad(stick: new(direction.X * 0.8f, -direction.Y * 0.8f)), 0.016).Direction == direction,
        $"Controller stick supports direction {direction}");

    var step = new PlayerMotion(new(5, 5));
    var destination = new MapPosition(5 + direction.X, 5 + direction.Y);
    step.MoveTo(destination);
    step.Update(PlayerMotion.StepDuration / 2);
    var expectedFacing = direction.X < 0 ? 1 : direction.X > 0 ? 2 : direction.Y > 0 ? 0 : 3;
    Check(step.Position == destination && step.IsMoving && step.FacingRow == expectedFacing
        && step.VisualPosition == new Vector2(5 + direction.X * 0.5f, 5 + direction.Y * 0.5f),
        $"Direction {direction} interpolates both axes and uses the approved four-direction artwork");
    step.Update(PlayerMotion.StepDuration / 2);
    Check(!step.IsMoving && step.VisualPosition == new Vector2(destination.X, destination.Y)
        && step.AnimationFrame == 1, $"Direction {direction} finishes one 250 ms step at four animation frames per second");
}
Check(new GameInput().Update(new(Keys.W, Keys.D), default, 0.016).Direction == new Point(1, -1), "WASD supports diagonals");
Check(new GameInput().Update(new(Keys.Left, Keys.Right, Keys.Up), default, 0.016).Direction == new Point(0, -1),
    "Opposing keys cancel only their own axis");
var heldDiagonal = new GameInput();
heldDiagonal.Update(new(Keys.Down, Keys.Right), default, 1, active: false);
Check(heldDiagonal.Update(new(Keys.Down, Keys.Right), default, 0.016).Direction == Point.Zero,
    "Focus return does not replay a held diagonal");
var boundedStep = new PlayerMotion(new(5, 5));
try { boundedStep.MoveTo(new(7, 6)); throw new Exception("FAIL: Accepted a non-adjacent animation step."); }
catch (ArgumentException) { checks++; }
Check(boundedStep.Position == new MapPosition(5, 5) && !boundedStep.IsMoving, "Rejected animation leaves position unchanged");

var motion = new PlayerMotion(new(5, 5));
var rider = new PlayerMotion(new(4, 5));
rider.Face(new(1, 0));
rider.CarryTo(new(4, 6)); rider.Update(.45, new Vector2(4, 5.5f));
Check(rider.Position == new MapPosition(4, 6) && rider.VisualPosition == new Vector2(4, 5.5f)
    && !rider.IsMoving && rider.FacingRow == 2 && rider.AnimationFrame == 0,
    "Raft travel follows its deck without walking in place or changing facing");
rider.Update(.45, new Vector2(4, 6)); rider.MoveTo(new(5, 6)); rider.Update(.125);
Check(rider.VisualPosition == new Vector2(4.5f, 6) && rider.IsMoving,
    "Disembarking starts an ordinary step from the visible deck");
rider.SnapTo(new(3, 5)); rider.MoveTo(new(4, 5)); rider.Update(.125, new Vector2(4, 5.25f));
Check(rider.VisualPosition == new Vector2(3.5f, 5.125f), "Boarding walks toward the moving deck rather than its logical tile");
rider.CarryTo(new(4, 6)); rider.Update(.125, new Vector2(4, 5.5f));
Check(!rider.IsMoving && rider.VisualPosition == new Vector2(4, 5.5f) && rider.Position == new MapPosition(4, 6),
    "A raft tick during boarding preserves the step and lands the boots on its deck");
rider.SnapTo(new(7, 7)); rider.Update(.2);
Check(rider.VisualPosition == new Vector2(7, 7), "Restoring a land position removes all carrier offset");
var relaxedWalk = new PlayerMotion(new(5, 5));
relaxedWalk.MoveTo(new(6, 5));
relaxedWalk.Update(0.18);
Check(relaxedWalk.IsMoving && relaxedWalk.VisualPosition.X < 5.8f && relaxedWalk.AnimationFrame == 0,
    "At the old arrival time the slower walk is still in progress with a relaxed first pose");
motion.MoveTo(new(6, 5));
Check(motion.Position == new MapPosition(6, 5) && motion.VisualPosition == new Vector2(5, 5),
    "Accepted movement commits the grid destination while the sprite starts at the previous tile");
motion.Update(PlayerMotion.StepDuration / 2);
Check(motion.VisualPosition == new Vector2(5.5f, 5) && motion.IsMoving && motion.FacingRow == 2,
    "An eastward step animates halfway along its cardinal path");
try
{
    motion.MoveTo(new(6, 6));
    throw new Exception("FAIL: A turn interrupted an unfinished step.");
}
catch (InvalidOperationException) { checks++; }
motion.Update(PlayerMotion.StepDuration / 2);
Check(motion.VisualPosition == new Vector2(6, 5) && !motion.IsMoving,
    "A completed animation lands exactly at the accepted tile");
motion.Face(new(0, 1));
Check(motion.Position == new MapPosition(6, 5) && !motion.IsMoving && motion.FacingRow == 0,
    "Facing a blocked shop entrance does not move the player");
motion.MoveTo(new(6, 4));
motion.Update(10);
Check(motion.VisualPosition == new Vector2(6, 4) && !motion.IsMoving && motion.FacingRow == 3,
    "A delayed frame completes one accepted step without overshooting");
motion.MoveTo(new(5, 4));
motion.Update(0.03);
motion.SnapTo(new(5, 5));
Check(motion.VisualPosition == new Vector2(5, 5) && motion.Position == new MapPosition(5, 5) && !motion.IsMoving,
    "Loading or resetting cancels the old visual step and snaps to the restored grid position");
var slowFrames = new PlayerMotion(new(5, 5));
var fastFrames = new PlayerMotion(new(5, 5));
slowFrames.MoveTo(new(5, 6));
fastFrames.MoveTo(new(5, 6));
slowFrames.Update(0.09);
for (var i = 0; i < 9; i++) fastFrames.Update(0.01);
Check(Vector2.Distance(slowFrames.VisualPosition, fastFrames.VisualPosition) < 0.0001f
    && slowFrames.AnimationFrame == fastFrames.AnimationFrame,
    "Walking position and animation cadence depend on elapsed time, not frame count");

var retroWalk = new PlayerMotion(new(5, 5));
var observedFrames = new List<int>();
for (var step = 0; step < 4; step++)
{
    retroWalk.MoveTo(new(5 + step + 1, 5));
    retroWalk.Update(PlayerMotion.StepDuration);
    observedFrames.Add(retroWalk.AnimationFrame);
}
Check(observedFrames.SequenceEqual(new[] { 1, 2, 3, 0 }),
    "Walking cycles through four poses across consecutive quarter-second steps");
var standingFrame = retroWalk.AnimationFrame;
retroWalk.Update(10);
Check(retroWalk.AnimationFrame == standingFrame, "Standing does not advance the walking cycle");
retroWalk.SnapTo(new(5, 5));
retroWalk.Face(new(-1, 0));
Check(retroWalk.AnimationFrame == 0 && retroWalk.FacingRow == 1,
    "Restoring position resets the walk phase and west selects its own sprite row");

var directory = Path.Combine(Path.GetTempPath(), "yipe-monogame-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "town-preview.json");
    var saves = new SaveStore(path, map);
    await saves.SaveAsync(new(5, 5));
    Check(await saves.LoadAsync() == new MapPosition(5, 5), "Save/load restores position");
    await saves.SaveAsync(new(6, 5));
    Check(await saves.LoadAsync() == new MapPosition(6, 5), "Replacing an existing save restores the new position");
    var previous = await File.ReadAllBytesAsync(path);
    await ExpectFailure(() => saves.SaveAsync(new(6, 6)), "Invalid save position is rejected");
    var afterRejectedSave = await File.ReadAllBytesAsync(path);
    Check(previous.SequenceEqual(afterRejectedSave), "Rejected save leaves previous bytes intact");

    // Force failure in the actual write path after a good save already exists.
    Directory.CreateDirectory(path + ".tmp");
    await ExpectFailure(() => saves.SaveAsync(new(5, 5)), "Write failures propagate to the UI");
    var afterFailedWrite = await File.ReadAllBytesAsync(path);
    Check(previous.SequenceEqual(afterFailedWrite), "Failed write retains the last successful save");
    Directory.Delete(path + ".tmp");

    foreach (var invalid in new[]
    {
        "{", "null", "{}",
        "{\"Version\":99,\"MapId\":17,\"X\":5,\"Y\":5}",
        "{\"Version\":1,\"MapId\":18,\"X\":5,\"Y\":5}",
        "{\"Version\":1,\"MapId\":17,\"X\":6,\"Y\":6}",
        "{\"Version\":1,\"MapId\":17,\"X\":-1,\"Y\":5}",
        "{\"Version\":1,\"MapId\":17,\"X\":2147483647,\"Y\":5}",
        new string(' ', 5000),
    })
    {
        await File.WriteAllTextAsync(path, invalid);
        await ExpectFailure(async () => { await saves.LoadAsync(); }, "Invalid or incompatible save is rejected");
    }
    File.Delete(path);
    await ExpectFailure(async () => { await saves.LoadAsync(); }, "Missing save is reported");
}
finally { Directory.Delete(directory, recursive: true); }

Console.WriteLine($"Passed {checks} checks, including 624 town movement cases, 8192 RNG output/state records, {playerRecords} player records, {mapRecords} map/world records, {sessionRecords} session records, {serviceRecords} shop/spell/casino records, {explorationRecords} exploration records, {combatRecords} combat records, {questRecords} quest records and {encounterRecords} encounter records from C++.");

void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + label);
    checks++;
}

async Task ExpectFailure(Func<Task> action, string label)
{
    try { await action(); }
    catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
                                  or System.Text.Json.JsonException)
    {
        checks++;
        return;
    }
    throw new InvalidOperationException("FAIL: " + label);
}

static GamePadState Pad(Buttons button = Buttons.None, Vector2 stick = default) =>
    new(stick, Vector2.Zero, 0, 0, button);
