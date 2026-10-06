# MonoGame graphics checks

This executable runs 657 checks: the original 139 visual checks, eight diagonal
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

The checked-in runner has been exercised on Apple Silicon macOS. Other platforms,
headless display configurations and physical controller behavior require their
own validation. Building this project does not run graphics checks in CI.
