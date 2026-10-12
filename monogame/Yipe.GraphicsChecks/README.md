# MonoGame graphics checks

The latest full run passed **3,862 checks**, including all 41 animated monsters,
the eleven remastered Ice Cave rooms, thirteen Water Cave rooms and fifteen Final Cave rooms. It includes the original 139 visual checks, eight diagonal
dispatch/animation checks, and six session checks for logical position/storage,
world transitions, shop handoff and RNG isolation, plus eight full-save checks
for complete state/renderer replacement and non-destructive failure handling,
and three P1 completion checks for explicit imported-file startup, generated-load
RNG behavior and refusal to auto-migrate raw preview bytes. Ten integration
checks additionally cover the shop-exit RNG draw, audio asset loading, creation,
inventory cancellation, spell failure, score-menu/code navigation and exploration timer/overlay behavior. Another 64
checks validate the shared palette and transparent borders of all 44 creature/NPC
cells and sixteen map-object cells. Thirteen additional checks cover the flora atlas; 115 render all static/generated maps, 97 cover initial/linked/timed tile artwork, and eight compare secret tiles with ordinary scenery.
Another 112 checks validate all character poses/palettes/anchors; twelve exercise every Classic/Silly appearance through creation.
Five more cover world bridge reflections, five cover the quest atlas, three verify real audio playback/lifecycle, and six exercise casino transactions through overlays. Six exercise moving-platform and fire-wall rescue/return routes, including saving aboard; two verify surface-potion relocation/audio through the inventory. Five cover complete statistics, hidden monster names, imported history and toggling Classic/Silly names during play.
Two check cancellation of New adventure during text entry and action menus.
Four cover connected textured water, changed-map cache invalidation, deterministic shores and paused/masked wave animation.
Fifteen map checks cover area/world navigation, pause and mid-step freezing, sharp rendering, fog masking, discovery save/load, alternate layers, generated rooms, and menu/creation handoff. The appearance checks also verify fresh discovery journals. The earned opening runner preserves both save files and checks map history in its fresh-process continuation, including a real combat interruption guard.
Nine desert checks cover frozen/moving weather, clear-sand clipping, rolling silhouettes, full-state/RNG isolation, stable terrain, biome scope, cache invalidation and anchored palm roots. The runner also retains 96 native desert animation frames under `desert-wind/`.
The 360-frame tour sends
commands through game update and the session.
It uses a real MonoGame graphics device and the current game
renderer. It is separate from `Yipe.Tests`, which runs without a game window.

## Run

From the repository root, with the .NET 10 SDK installed:

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

An active graphical desktop is required. Keep the test window focused and avoid
opening a second graphics runner or rebuilding during a run. For local monster
art iterations, use `--monsters-only --capture-monsters 10,11`: all registered
monster art, motion and real combat checks still run, while full clips are
limited to the selected IDs. Omit `--monsters-only` for full-suite verification;
`--capture-monsters` only limits exported clips, not monster coverage. For a full
terrain regression run, use `--capture-monsters 1` to avoid exporting videos
for the entire unchanged roster while retaining its checks.

For cloud iterations, use `--clouds-only`. This runs the existing cloud, sun-shaft,
hero/dust, rain and day/night checks with isolated saves and settings. It captures
all three towns, open meadow and desert, including a 24-second cloud sequence.

For overworld iterations, use `--outdoors-only`. This renders all 36 surface maps,
checks deterministic scenery, fog, secret aliases and gameplay/RNG preservation,
and exercises habitat mixing, positional water, night/cold wildlife, mute/focus
and town road access. It also exports day/night views, rain views, motion clips
and a region coverage CSV under `outdoors/`. All saves are isolated test fixtures.

For local menu iterations, use `--menus-only`. This checks entering Graphics,
wrapping its cursor, changing settings while paused, returning one level with
Escape or controller Start, and resuming from the root. It also runs the existing
help and control-remapping checks. Captures under `help-controls/` show both menus
at 640×360, 1024×768 and 1920×1080. The full suite additionally exercises every
moved graphics setting and its existing persistence behavior. Controller actions
are synthesized; physical-controller verification remains separate.

For local breeding-ground iterations, use `--breeding-only`. This renders all six
rooms and checks palette/contrast, cache invalidation, secret equivalence, fog,
authored lighting, matching region overlaps, unchanged gameplay/RNG and the real
audio lifecycle. Creature checks also cover cave tint/contact, unchanged grounded
/hovering/large silhouettes, battle-art isolation and exact appearance restoration
after leaving the biome. Seven contour checks cover round lamp reach, light grazing
a carved corner, stepped shadow edges, secret equivalence, closed/open doors and
bare earth at cut stone corners. Native, isolated-light, lit/unlit gameplay captures
and a six-second frame sequence are written under `breeding-grounds/`. The normal suite includes these
checks too.

For local Stone Cave iterations, use `--stone-only`. This renders upper
E2/F2–F6 and deep F4–F6 (51/52/58/64/70/76/104/110/116), checks the native
palette and wall/floor contrast, deterministic sparse atmosphere, secret
aliases, original water coverage, cache restoration, all seven matching room
overlaps, contour light/doors/fog, stone/water footsteps, creature appearance,
and the production audio lifecycle. Full saves and RNG remain unchanged.
Native, default gameplay, isolated-light captures and a five-second gameplay
sequence are written under `stone-cave/`. The full suite includes these checks.
Thirty-five additional movement checks cover all eleven rooms, release/turn/reverse
traction, blocked pushes, floor aliases, doors/depth exits/room edges, menu and
restore cancellation, real encounters and unaffected neighboring biomes. A 60 Hz
authored-room push/glide/brake sequence is retained under `ice-movement/`.

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --stone-only --output build/stone-checks
```

For Final Cave work, use `--final-only`. The focused run passes **265 checks** across all fifteen
upper/deep rooms and retains native, gameplay, unlit and isolated-light captures.
Checks cover palette/readability, visible crystal sources, secret aliases,
cache invalidation, matching overlap continuity, door/corner/fog occlusion,
collapsing-floor warnings, creature integration, recorded ambience lifecycle,
and complete session/RNG preservation. Reference captures include eight other
regions. All fixtures use isolated adventures.

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --final-only --output build/final-cave-checks
```

For local Water Cave work, use `--water-only`. The focused run passes **257 checks**
covering all thirteen rooms (44/45/46/50/56/62/68/74/90/96/102/108/114) and eight
unaffected references. It checks wall/floor contrast, palette/cache invalidation,
secret aliases, matching authored overlap neighborhoods, contour light and door
occlusion, fog, native atmosphere, creature/battle isolation, pool reflection
clipping, actual footstep surfaces, recorded audio lifecycle, and full gameplay/RNG
preservation. Captures are retained under `water-cave/`, including a six-second
atmosphere sequence. Some authored overlap neighborhoods have differing banks;
those original layouts are preserved and excluded from exact material comparisons.
The `raft-motion/` captures additionally cover all eight authored Water Cave
rafts, shared rider/deck interpolation, actual sprite support under the boots,
boarding during a raft tick, wood contacts, silent passive travel, disembarking,
pause/map/action panels, save/load aboard and isolation from Ice Cave. A three-second
60 Hz clip retains the authored D4 terrain and original raft movement commands.

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --water-only --output build/water-cave-checks
```

For local Ice / Zephyr Cave iterations, use `--ice-only`. The focused run passes
254 checks across all eleven upper/deep rooms (41/42/47/53/81/82/87/88/93/94/99)
and six comparison scenes. It covers frozen-floor/wall contrast, cache restoration,
secret aliases, all nine matching walkable overlaps, sparse frost animation and
fog, cold contour lighting and door occlusion, creature/battle isolation, solid
footsteps, the recorded ambience lifecycle, and unchanged full saves/RNG. Native,
lit/unlit gameplay, isolated-light captures and an eight-second gameplay sequence
are retained under `ice-cave/`. Seventeen reflection checks also cover animated hero,
grounded/hovering/large monsters, roaming and pursuit motion, restrained frost
masking, secret aliases, wall/door/stair/fog clipping and changed-terrain restoration.
The final focused pass also verifies reflection at a rock's carved open corner.
Isolated reflections and a complete walking-step sequence are retained there too.
The full suite includes these checks.
Thirty-five additional movement checks cover all eleven rooms, release/turn/reverse
traction, blocked pushes, floor aliases, doors/depth exits/room edges, menu and
restore cancellation, real encounters and unaffected neighboring biomes. A 60 Hz
authored-room push/glide/brake sequence is retained under `ice-movement/`.

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --ice-only --output build/ice-checks
```

For local Earth Cave iterations, use `--earth-only`. This covers all seven rooms
(57/63/69/75/103/109/115), native/default gameplay captures, wall/floor contrast,
puddle reflections, secret aliases, fog, deterministic atmosphere, connected
floor overlaps, contour light and door-cache restoration, granular/water
footsteps, creature tint restoration, and upper/deep ambience lifecycle through
the production pause, map, focus, master-volume and biome-transition gates.
It uses isolated sessions and checks full gameplay/RNG preservation.

```sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --earth-only --output build/earth-checks
```

For local Fire Cave iterations, use `--fire-only`. The focused run renders all
eleven upper/deep rooms and checks magma containment within blocked rock, native
palette and animation, fog, secret aliases, cache invalidation, shared walkable
overlaps, contour lighting and doors. It also exercises crisp heat, day/night
isolation, matching stone/grit footsteps, warm creature integration and the real
ambience lifecycle. Full saves and RNG remain unchanged. Native, isolated-light,
default gameplay captures and six-second native/gameplay sequences are retained
under `fire-cave/`. The normal suite includes these checks too.

Keep the test window focused and avoid
keyboard/controller input while it runs. The checks exercise fullscreen and
return to the 1280 × 720 window. A failure returns exit code 1; invalid arguments
return 2. `--help` returns 0 without opening a window.

Each invocation creates a unique `yipe-graphics-...` subdirectory beneath
`--output`. Omitting `--output` uses the operating system's temporary directory.
It prints the full artifact location and retains:

- `checks.log`, including runtime/OS information, results and failure details;
- screenshots of world, UI, effects and native atlases;
- an isolated `test-saves/game.y3m` produced by the full save/load check;
- `tour.mp4` only when recording is requested.

No checkout path is embedded. Content is loaded beside the compiled executable,
so the executable can also run from a different working directory. Choose an
output directory appropriate for the machine. Move small logs or selected
captures into `monogame/evidence/` when using them as milestone evidence.

Heat presentation checks render desert, mixed terrain and every Hell region
through the actual frontend. They verify smooth and paused refraction, sharp
hero/UI areas, biome scope and gameplay-state isolation. `heat/` also retains
144 frames each of desert and Hell at 24 fps for animation review, plus an area
map and miniature-Off capture. These use isolated diagnostic scene fixtures.
Every Hell region additionally checks that Strong miniature preserves all native
pixels exactly before heat, and that heat samples only existing pixel colors.

Hell biome checks cover the authored region and nine generated layouts, new atlas
integrity, animated/frozen frames, hidden-cell masking, lava containment, light,
atmosphere, door removal, terrain-cache changes and Gatekeeper/flame readability.
`hell-biome/` retains those layouts, the compiled atlas and the area-map view.

Vignette checks exercise the pause-menu toggle and profile reload, every miniature
and scanline combination, unchanged central pixels, edge darkening, and unaffected
pause/action/map panels. Heat captures include desert, Hell and cave vignette
views, including heat with miniature disabled. `yipe-vignette-on.png`,
`yipe-vignette-off.png` and `yipe-vignette-menu.png` show the feature and menu layout.

`pixel-lighting/` retains lighting On/Off and combined-effect captures for town,
Earth Cave and all Hell maps. Checks cover real framebuffer changes, frozen
flicker, full-state/RNG preservation, light overlap limits, exact black outlines,
fog, occluding doors and cache invalidation, secret-wall equivalence, persisted
menu settings and unaffected map/UI colors. The mask and native light target are
cached; tests use isolated sessions and profiles.

`sun-shafts/` retains Off/Subtle/Strong outdoor captures, combined effects, the
pause-menu control and 72 animation frames at six frames per second. Thirty-two
checks cover outdoor/cave/Hell scope, saved settings, unchanged state/RNG,
bounded warm brightness, black outlines, camera anchoring, texture wrapping,
native-pixel scaling, paused animation and unaffected action/pause/map UI.
Existing effect checks disable sunlight after its dedicated checks to retain
their isolated measurements.

`sun-actor/` adds 35 checks for sun-mask sampling, all 13 rendered hero appearances,
directional/animated sprite silhouettes and outlines, equipment glint size and
cooldown, highlight fade, dry/wet/bridge/fire/platform dust scope, shade exclusion,
frozen/rising/fading motes, teleport and setting resets, indoor scope, production
pause, map exclusion and full gameplay/RNG preservation. It retains close-up
sprite/dust captures and 96 frames of a controlled outdoor walk at 24 fps.

`cloud-shadows/` adds 50 checks for outdoor/cave/Hell scope, bounded shade and
density variation, exact Off/black-pixel behavior, frozen/drifting shade,
smooth fractional movement, camera anchoring and texture wrapping, independently
moving clouds attenuating shafts and matching hero/dust light samples,
independent saved strength, production pause, sharp UI/maps and complete
gameplay/RNG isolation. It retains Off/Subtle/Strong and clouds-only town/desert
captures, the raw cloud silhouette alpha mask, a neutral-background feathering
capture, combined effects, the Graphics submenu and 288 animation frames at
12 frames per second. Cloud shadows are disabled after these checks to preserve
the existing effects' isolated tests.

`rain/` covers the separate weather preference, outdoor/indoor scope, full gameplay
and RNG isolation, native pixel scaling, world anchoring, fixed-clock animation,
shoreline clipping, bridge/building exclusions, pause and UI/map isolation.
It retains Off/Subtle/Strong town/desert captures, combined effects, the Rain menu
and 192 animation frames at 24 fps showing a shower arriving. Automatic weather
checks cover clear/shower transitions, fractional density, completion back to a
clear frame, and active-outdoor-only timing through pause, indoor, map, overlay
and Off states. Rain defaults to Auto Subtle; the runner disables rain after its
isolated checks before checking the other effects.

`combat-feedback/` checks the production combat menus against independent core
results for attacks, single/area spells, non-damaging spells, abilities, item
counterattacks, fleeing, victory and defeat. It covers target changes after a
killing blow, sequential enemy damage and sound timing, interpolated health,
healing before retaliation, resource-increase potions, deferred level-up gains,
direct targeting, single/area/self/replacement previews, native pixels, fixed text,
full gameplay/RNG isolation and clearing between overlays. Every combat spell and
ability has a representative capture. Auto pacing checks cover ordinary returns,
pause, failures, multi-page results, terminal confirmations and no repeated action.
Profile checks retain the separate Screen shake (Off) and Battle pacing (Manual)
defaults and independently saved choices. Captures include menus, targets, HUDs,
individual counterattacks, spells, abilities, victory/level-up, and 96 animation
frames at 30 fps.

Enemy information checks also cover live encounter ratings, imported incomplete
defeat counts, independent inspection/attack selections, living-enemy wrap/skip,
Back and Escape restoration, unchanged full gameplay/battle/RNG state, Auto
pacing isolation, transformed-rabbit stats, empowered traits, and native-pixel
rendering. The focused card excludes hero resources and other opponents;
rendered regions respond to live stats and Details values, and switching views
preserves the portrait and browsed enemy. Captures include Stats and Details for
normal, transformed and empowered enemies.

`monster-animation/` adds checks for all 656 poses across the 41-monster roster,
palette and ground anchors, distinct cycles, mirrored facing, static-art fallback, smooth
roaming and pursuing travel, spawn/removal/map/relocation boundaries, pause and
modal freezing, approach-to-battle synchronization, animated combat-menu idles,
actual counterattack and defeat sequences, and full session/battle/RNG isolation.
The moose and golem's eased start and contact settle are checked alongside their
unchanged 250 ms arrival time. Pursuit checks cover the first accepted visible
approach, silent spawning/retreat, no repeated chase pulse, pause/modal/map
freezing, offscreen rearming, fog clipping, actor/map resets, stable native
rendering and full gameplay/RNG isolation. The amber glyph fades without residue;
its presentation layer keeps it sharp above heat and other world effects.
Artifacts include compiled native sheets, four-second exploration clips for each
creature, real battle/defeat frame sequences, and a three-second pursuit capture.
The revised golem walk adds four renderer checks for alternating swing/plant
poses across diagonal steps, stable unchanged-tile synchronization, independent
actors and replacement resets. A separate 96-frame continuous stride/turning
capture at 24 fps also verifies full session/RNG preservation. The same rendered
foot-lead checks now cover all suitable bipeds and the horse's opposing hoof pairs.
Additional coverage checks detached stray marks, hops and hovering shadows, wet/dry shore crossings,
pixel-level ripple masking, fog, native borders, doubled boss footprints, the
waiting guard's face/key visibility and its quest-state removal. The empowered
palette/brace follows the resolved actor snapshot. Ten ordinary/empowered boss
parties use real encounter initialization; their supplemental defeat fixtures
set HP/defense low to exercise each slot with real attacks. Campaign acceptance
still uses the earned routes below.

## Earned frontend routes

For the earned opening and automatic fresh-process save/reload continuation:

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --opening-route --output build/graphics-checks
```

This separately compares all 42 retained C++ opening checkpoints and the King
return while using the production menus, combat, movement and save/load flow.
It retains an isolated mid-route save and input cursor under `restart/`; the
runner automatically launches `--opening-resume DIRECTORY` in a new process.
See [earned frontend evidence](../evidence/2026-10-04-earned-frontend/README.md)
for input-clock control and the single cosmetic portrait normalization.
Do not combine this option with `--record`.

For the late boss/rescue/save and automatic fresh-process final rescue/ending,
first produce the earned C# saves using the [campaign replay](../Yipe.Tests/README.md#complete-earned-campaign):

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --late-route build/campaign-earned-saves --output build/graphics-checks
```

This begins at `hell-finale-boss-ready.y3m`, earned by the compared fresh campaign.
It navigates real combat/item/spell/quest overlays and uses 50 ms updates with the
approved 250 ms walking pace. After the production save, the runner starts a
separate `--late-resume DIRECTORY` process. No character or map prerequisites are
injected. See [completion evidence](../evidence/2026-10-04-p234-complete/README.md)
for scope, retained screenshots and the distinction from an identical RNG replay.
These earned modes run separately from the ordinary 657 diagnostic checks.

## Optional recording

The 360-frame deterministic tour renders during every run. Recording requires
FFmpeg on `PATH` with the `libx264` encoder; ordinary checks do not require FFmpeg.

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks --record
```

Recording produces a six-second, 60 fps, 1280 × 720 MP4. FFmpeg errors fail the
run rather than silently omitting the recording. No fixed Homebrew or system
installation path is assumed.

## Scope and implementation

Checks cover miniature blur falloff, sharp UI and focus bands, scanlines, camera
bounds, movement trails, footprints, river masks, bridge reflections, building
occlusion, shop signs, cave grounding, pause, fullscreen and save/load during a
step, including real game dispatch for all four diagonals and both-axis animation.
They compare the current rendering with isolated layers and controlled
inputs, rather than depending on byte-identical screenshots across GPU drivers.

The runner uses reflection to exercise existing internal drawing boundaries and
controlled state. No production visibility, constructors or timing settings were
changed solely to support it. The selected-save constructor option is the production
`--load-save` path, also exercised here. Test-only VSync is disabled for the manual `RunOneFrame`
driver, avoiding a macOS swap wait. Startup opens an explicitly selected imported
preview file in the isolated directory and stays paused. Subsequent save/load
actions use another isolated store; the user's save is not read or written. Test files remain with the artifacts, including after a failure.

Help/control checks additionally verify current-binding help and paging, all 21
gameplay guide topics, recovered About credits, keyboard capture/conflicts/cancel,
controller remapping, reset, profile reopen and non-destructive write failures.
They retain captures at 640×360, 1024×768 and 1920×1080 under `help-controls/`,
while comparing full gameplay snapshots/RNG before and after. The connected
physical-controller count is reported separately; synthetic controller inputs
do not claim physical-device verification.

The checked-in runner has been exercised on Apple Silicon macOS. Other platforms,
headless display configurations and physical controller behavior require their
own validation. Building this project does not run graphics checks in CI.

Studio branding checks retain `help-controls/icosium-labs-splash.png` and verify
the 3.2-second title handoff, Confirm/Back/Pause skip without selecting a title
choice, unchanged full gameplay/RNG snapshots, and port/remaster attribution.

## Day and night cycle

The default Auto mode uses a twenty-minute elapsed wall clock and persists its phase
in the desktop profile. The graphics suite captures Day, Dusk, Night and Dawn
across outdoor scenes, preserves underground lighting, tests wall-time progression
through modal/storage/focus pauses, suspension catch-up without gameplay changes,
clock rollback and fixed-mode suspension, and verifies bright battle/action panels, navigation maps
and hidden light sources. `day-night/` includes 240 frames of an accelerated full
cycle and phase previews at 320 × 180 sampled from the displayed frame, including
existing heat/miniature composition. [Plan](../DAY_NIGHT_PLAN.md).

## Player footsteps

Footstep checks play all six material families on the real device, verify a quiet
separate voice and original-effect priority, and exercise master volume, muting,
focus cleanup, terrain masks, accepted walking contacts, diagonal travel, pause,
modal stops and restores. The `footsteps/` artifacts contain eighteen CC0 recorded
WAV variants and a sequence at default in-game gain. Asset/license checks run in
`Yipe.Tests`. [Sources](../Yipe.Desktop/Content/Audio/Footsteps/README.md),
[plan](../FOOTSTEP_AUDIO_PLAN.md).

## Zephyr snow basin

`SnowBiomeChecks` resolves surface A3/map13 and tests native winter art, connected
snow/melting edges, hidden-gold aliases, the matching A2/A3 ridge spine, warm and
underground cache bypasses, snowfall/fog masks, rain and impact exclusion, planted
contacts, tracks/powder fade and cleanup, and same-session gameplay/RNG snapshots.
Day/Night and combined presentation captures accompany a short native animation.

For focused local iterations:

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --snow-only --output build/snow-biome/focused
```

[Plan](../SNOW_BIOME_PLAN.md), [evidence](../evidence/2026-10-08-snow-biome/README.md).

## Hell colour and ambience

`--hell-only` runs the existing Hell architecture, fog, doors, cache, flames and
molten-terrain checks across the entrance and nine generated layouts. It also
captures all four regions at gameplay scale with lights on/off and exercises the
dedicated recorded ambience: fade, master volume, mute, focus, overlays, room and
biome transitions, action-sound ducking and complete gameplay/RNG preservation.
The full suite includes these checks.
Thirty-five additional movement checks cover all eleven rooms, release/turn/reverse
traction, blocked pushes, floor aliases, doors/depth exits/room edges, menu and
restore cancellation, real encounters and unaffected neighboring biomes. A 60 Hz
authored-room push/glide/brake sequence is retained under `ice-movement/`. Generated fixtures use fixed seeds, not the
layout in the user's live adventure.

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --hell-only --output build/hell-atmosphere/focused
```

[Before/after evidence](../evidence/2026-10-09-hell-atmosphere/review.json) ·
[Audio credits](../Yipe.Desktop/Content/Audio/Ambience/HELL-CREDITS.txt)

## Maynard leaves and positional river audio

`--maynard-only` exercises surface E3/map17 using a separate town renderer and
isolated adventure. It captures native leaves, the bridges and town square by
day/night, and an eight-second leaf animation. Checks cover sparse pixel shapes,
scene-clock determinism, building/hero occlusion, actual-water proximity and
left/right panning, smooth walking gain, two-voice pause/mute/focus/overlay gates,
master volume, action ducking, day/night tracks, distant-river release, town exit
and complete gameplay/RNG preservation. The full suite includes these checks.
Thirty-five additional movement checks cover all eleven rooms, release/turn/reverse
traction, blocked pushes, floor aliases, doors/depth exits/room edges, menu and
restore cancellation, real encounters and unaffected neighboring biomes. A 60 Hz
authored-room push/glide/brake sequence is retained under `ice-movement/`.

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --maynard-only --output build/maynard-atmosphere/focused
```

[Evidence](../evidence/2026-10-09-maynard-atmosphere/review.json) ·
[Recorded audio credits](../Yipe.Desktop/Content/Audio/Ambience/MAYNARD-CREDITS.txt)
