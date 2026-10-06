# MonoGame desktop port

The chosen direction is a C# / MonoGame desktop game with controller support,
preserving the 2D gameplay while improving art, menus, controls, and quality of
life. Official Nintendo integration and hardware testing come later, once
approved platform access or a porting partner is available.

This is a **playable complete campaign port**, with P1–P4 implemented and verified.
The retro town connects to every surface/cave region, with creation, combat,
shops, spells, casinos, quests, statistics and the ending. A fresh earned
campaign matches 43,080 C++ state/RNG observations and 66 complete saves;
real frontend opening and late-game routes survive separate-process restarts.
Desktop release work remains in P5/P6. The C++ reconstruction at the repository
root remains the gameplay reference.

Use the [port plan and progress tracker](PORTING_PLAN.md) for the full-game
milestones, acceptance criteria, current task, and session resume instructions.

## Run

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet run --project monogame/Yipe.Desktop -c Release
```

MonoGame DesktopGL 3.8.5.1 is pinned in the desktop project. NuGet restores the
framework and native dependencies. `global.json` accepts stable .NET 10 SDK
feature bands. The core library targets .NET 8; the executable and checks use
.NET 10. The eventual console backend may require different target frameworks
and build tools, which must be verified against its approved documentation.

| Action | Keyboard | Controller |
| --- | --- | --- |
| Move / select menu row | Arrow keys or WASD | D-pad or left stick |
| Open pause menu | Escape | Start |
| Open actions / select menu item | Enter or Space | South face button |
| Open / close map | M | View / Back button |
| Switch area / world map | Enter or Space | South face button |
| Change world-map layer | Left / Right | D-pad / stick left or right |
| Back / resume | Escape | East face button or Start |

The first connected controller is used. Button descriptions refer to physical
positions because printed labels vary between controller brands. Movement supports
eight directions: combine two arrow/WASD keys, press a D-pad diagonal, or aim the
stick diagonally. The stick uses eight directional sectors and a dead zone;
slight off-axis input remains cardinal. Held input retains its repeat delay.
Losing application focus or disconnecting the only connected controller pauses play.

The game fills a **16:9 view**, starting in a 1280 x 720 window. A camera follows
the player on both axes across each map, keeping tiles square and
clamping at the map edges. There is no permanent sidebar or menu. Escape / Start
opens a pause overlay containing save/load, new adventure, **Fullscreen**, optional
**Scanlines** (on by default), and **Miniature effect** (Strong by default).
Select Miniature effect with Enter / the south face button to cycle through
**Off / Subtle / Strong**. Scanline, miniature, vignette, pixel-lighting and sound preferences persist
separately from adventure saves. **Vignette** toggles a soft darkening around the
edges and corners, leaving the center clear. It defaults to **Off** and remembers
your choice. It works with every miniature setting and with scanlines; it does
not blur the pixel art or darken menus, messages, or the map.
**Pixel lighting** defaults to **On** and can be toggled in the same menu.
Forge and magic-shop windows, cave wall torches, and fire/lava cast restrained
warm light on nearby terrain and characters. The light uses four native-pixel
shade bands, with quiet six-Hz fire flicker and steady magic-shop windows.
Solid walls and closed doors block it; hidden sources and unexplored cells stay
unlit. Overlapping sources are capped, and the effect leaves the existing ambient
brightness, sharp outlines and readable paths intact. Small cave torches are
decorative, add no collision or hazards, and disappear when the setting is Off.
Menus and maps keep their original colors. Pausing freezes the flicker.
Enter / the south button opens Map, Inventory, Spells,
Character, Other, Read Sign and Sound. Dialogues pause exploration and can span
multiple pages; confirm advances them. Complete an interaction before saving.
The character menu includes high scores, the original score code, equipment and
monster history, and the Classic/Silly equipment-name toggle.
The world and bitmap UI share a **320 x 180** canvas with point sampling.
Whole-number enlargement fills 720p, 1080p, and 1440p exactly. Other window sizes
can have black borders to preserve equally sized pixels and 16:9; windows
smaller than the native canvas use proportional downscaling. The mouse is
visible but menus currently use keyboard or controller input.

## Area and world map

Press **M** or controller **View/Back**, or choose **Map** from Actions. The map
opens as a full-screen overlay showing the entire current region in the game's
retro artwork, with a gold player/facing marker. Enter/South switches to a world
overview; Left/Right selects among visited surface, upper-cave, deep-cave and
Yipe Hell layers. M, Escape, View or East closes the map. Start opens pause over
it. Movement, encounters, hazards and animation remain paused while reading.

Visible shops/services, cave entrances, stairs and bridges receive markers.
Unvisited regions and unrevealed cave cells stay dark; secrets are not marked.
Surface regions become fully charted on entry, matching their existing visibility.
The world view shows visited regions and connections actually traveled, including
cross-layer routes in the current region's known-links list. It is an overview;
the detailed area view always shows the region you are currently in.

Discoveries are saved beside the adventure in `game.map.json` (or the matching
name for a selected `.y3m`). Keep both files together when copying an adventure.
Existing saves work without map notes and begin charting from their current area.
Generated caves discard their previous layout when regenerated. See
[save format](SAVE_FORMAT.md#map-discovery-companion) for compatibility and
failure behavior.

## Retro presentation

An accepted move updates the logical grid position once, then animates the
character to it over 250 milliseconds. Held movement repeats at the same cadence;
menu navigation keeps its 120-millisecond repeat. One pending direction waits for
the current step to finish before the core checks its destination. Diagonals move
both axes together over the same 250 ms grid step and use horizontal-facing poses
from the existing four-direction artwork. Collision follows the original game:
only the destination tile decides a diagonal move, even beside blocked cells. Walking frames
advance at **4 frames/second** through four poses per direction. An idle pose
briefly blinks every four seconds. Game updates remain at 60 Hz, independent of
the deliberately lower animation cadence. The camera follows the visual position
and rounds both axes to whole pixels;
pause freezes the world and its animations while the overlay can still animate.
Loading or resetting snaps the character and camera to the restored location.
Saving during a visual step records the already accepted destination tile.

While walking, the hero leaves a faint motion-blur trail extending at most three
world pixels behind the current pose. Three low-opacity samples draw beneath
the opaque character, with a smooth fade over the final quarter of each step.
The trail uses only the current accepted movement, so turns cannot leave stale
ghosts, and standing, blocked movement, loading, or resetting clears it. Pause
freezes the exposure with the character. This effect works with every miniature
setting and adds no blur to terrain or interface text.

The active terrain and sprite atlases live in `Yipe.Desktop/Content/Art/Retro`.
Their [art contract and exact generation prompts](Yipe.Desktop/Content/Art/Retro/README.md)
document the approved town sheets; the [world atlas manifest](Yipe.Desktop/Content/Art/Retro/World/README.md) records the new terrain, objects and monsters. They compile on load to native 32-pixel terrain,
hero, and decorative prop cells and 48-pixel building cells, using a shared 40-color palette and
binary transparency. Fixed sprite canvases and integer anchor alignment avoid
per-frame resizing. Buildings and the character draw in ground-position order.
Water and smoke change in six-Hz steps; shadows and sparkles use solid pixels.
Ambient effects use bounded drawing loops with no per-frame particle allocations.
Earlier illustrated source art remains archived under `Content/Art`, outside the
packaged content set.

Maynard's scenery includes four quiet meadow variants, deliberately placed
flowers, stones, grass tufts, reeds, and shrubs. Worn turf softens doorsteps.
Stone roads have rounded bends and junctions, with lightly irregular earth
edges. Their native-pixel shapes compile once at load time and preserve the
original routes and movement rules.
The same paving atlas now renders roads outside Maynard, including all four
rounded bends, bridge approaches and building/cave doorsteps. Authored tile
directions carry paths through map edges without shortening their ends.
The river has irregular banks, rounded bends, a southward current that follows
its course, sparse foam, and bridge shadows. Its shape is compiled once at
load time; moving ripples are clipped to the water. Crossing either bridge shows
a faint, blue-green reflection of the current character pose in the water below
the deck. It mirrors and compresses the sprite with a one-pixel ripple, fades
smoothly at the approaches, and never draws on the banks or over the bridge.
The other rivers and lakes now use the village's same water texture, irregular
banks, subtle shallows and sparse six-Hz currents/foam. Shorelines are compiled
across each connected map, with grassy surface banks and subdued cave edges;
adjacent lake tiles have no artificial internal borders. Reflections and ripples
share the rendered water mask. Water layouts update when a map or water tile
changes, without consuming gameplay RNG or changing collision.

Desert borders now have rounded, irregular contours and a scattered dry-grass
fringe. A cached map-wide sand layer softens isolated patches, long boundaries
and concave turns without seams between tiles. The authored terrain and movement
rules stay intact. Palms and dry bushes sway by at most one native pixel while
their roots stay fixed. Occasional tumbleweeds roll and bounce along clear sand,
with quiet gaps between gusts; faint dust puffs and trailing motes drift over
open ground. Wind stays away from water, vegetation and buildings, and actors
draw over it so gameplay remains legible. Effects use scene time (eight-Hz wind,
six-Hz vegetation), freeze with menus and consume no gameplay RNG. The area map
uses the same softened terrain, with airborne weather omitted for readability.

Buildings cast consistent southeast shadows, with roof-overhang shading and
restrained warm light in the forge and magic-shop windows.

Each shop has a pictogram sign beside its front corner, aligned with the facade's
ground line. Each board clears the windows and door; its support draws behind
the facade so both the sign and building details stay visible.
The original hanging shop markers are removed from the house artwork to avoid
duplicate signs. The castle keeps its small wall-mounted blue banners without
extra freestanding flagpoles around the entrance. The cave meets the bottom edge
of its mountain tile and casts no freestanding building shadow, keeping the
opening grounded at the path.
Signs and foliage move by at most one pixel; occasional leaves drift
over grass. A small doorstep cue appears when standing next to and facing an
entrance. Walking briefly disturbs grass or kicks up faint dust on paving;
wooden bridges produce neither effect. These details follow existing movement
and scene time, freeze when paused, and clear movement effects on load/reset.
Signs and buildings draw in depth order with the hero. All original map
collision and entrance behavior stays in the core.

The miniature effect simulates tilt-shift depth of field with a horizontal focus
band following the hero's center, including at the map edges. Nearby pixels stay
sharp. Both blur radius and opacity increase gradually across a broad Gaussian
falloff, retaining original detail even at the edges. Subtle keeps the softening
light; Strong increases its radius and blend strength. Both retain the original
art colors and add deeper contact shadows. Off restores the crisp presentation.
Pause panels and location messages draw afterward and remain sharp; scanlines
can be combined with any miniature setting.

`MiniatureEffect` first enlarges the native world to 1280 x 720 with point
sampling. Two weighted GPU passes then sample fractional offsets using the
built-in BasicEffect and linear filtering, so blur radii vary smoothly below the
size of a world pixel. It composites at that higher resolution before drawing
the separately enlarged pixel UI. Clamped edges prevent dark borders and
normalized weights preserve brightness. The three large render targets and
vertex storage are reused; no shader compiler or per-frame CPU pixel transfers
are needed. The post-processed view can contain colors beyond the source art's
40-color palette.

Hell keeps crisp terrain at every miniature setting: it retains contact shadows
but bypasses the blur passes. Its heat shimmer uses point sampling, so moving
edges retain their pixel colors. The Gatekeeper entrance uses native-pixel stone
blocks with clear outlines and highlights instead of an enlarged painted sprite.

## Saves

Use **Save game** and **Load game** in the pause menu. Manual saves include the
complete character, inventory, progression, world position, RNG, persistent map
changes, opened chests and defeated-placement IDs. They live at:

```text
<LocalApplicationData>/YipeIII-MonoGame/game.y3m
```

On macOS this is normally
`~/Library/Application Support/YipeIII-MonoGame/game.y3m`. The desktop captures a
snapshot before asynchronous writing, flushes a unique temporary file, then
replaces the destination. Loading checks the version, checksum, structure and
state before changing the live session. Saving during movement records the
accepted destination. Failed saves retain the previous file; failed loads retain
the current game. Static and generated world saves open in their corresponding regions.

The version 1 format is documented in [SAVE_FORMAT.md](SAVE_FORMAT.md). Existing
`town-preview.json`, C++ `.y3r` and original `.yp3` files remain untouched. They
use the explicit import command below; **Load game** accepts only `.y3m`. Display preferences and transient map/animation state are separate.

To convert a preview `.json`, current C++ `.y3r` (version 20), or original `.yp3`,
choose a new destination and then open it explicitly:

```sh
dotnet run --project monogame/Yipe.Desktop -c Release -- --import-save "/path/to/old.yp3" "/path/to/imported.y3m"
dotnet run --project monogame/Yipe.Desktop -c Release -- --load-save "/path/to/imported.y3m"
```

Import leaves the source untouched and refuses to overwrite an existing output.
Preview migration creates a default character because the old JSON stored only
position. Original saves use the recovered field mapping and seed 1 for the absent
RNG state. Older C++ saves must first be re-saved by the C++ rebuild. Read the
[full import policy](SAVE_FORMAT.md#explicit-import-policy) for validation and
encoding limits. Wider-world and generated saves load into their corresponding playable regions.

## Scope and next milestones

P1–P4 are complete. The desktop now plays the surface, caves and generated
regions with the approved retro presentation. `Adventure` connects original
arrival ordering, combat, quests, shops, casinos, secrets, visibility and timers
to keyboard/controller overlays. All twelve appearance choices and all world
tile families are implemented. Character statistics include equipment ratings,
monster history, special items, high scores and score codes; Classic/Silly names
can be switched during play without changing appearance.

`Yipe.Core` retains the original RNG, character rules, 112 static maps, three
generated maps and full-state persistence. Independent C++ reference suites
cover movement, map state, services, spells, combat, quests and encounters.
The complete earned opening also runs through the real graphics frontend and
reopens in a separate process. Current diagnostic coverage includes 502 core/
input/storage checks and 633 graphics checks, including all 115 map instances,
97 tile mappings, native artwork, casinos and timed platform/fire-wall routes.

The [complete campaign evidence](evidence/2026-10-04-p234-complete/README.md)
retains the actual inputs, native saves, compressed observations and exact replay
commands. The late frontend independently defeats the final boss with earned
resources, saves, reopens in a fresh process and earns the final rescue and ending.
The [feature dispositions](FEATURE_DISPOSITIONS.md) account for remaining original
desktop conveniences and inherited adaptations.
Desktop release packaging, physical controllers, other operating systems and
remaining desktop conveniences belong to P5/P6. Official console integration
follows separately when platform access is available.

Keep game rules in `Yipe.Core`, with no MonoGame or native platform references.
`Yipe.Desktop` owns rendering, input polling, and desktop storage. Use small,
concrete platform adapters when a real difference appears. Full-state encoding
is explicit and independent of runtime reflection; the retained legacy preview
reader uses source-generated JSON metadata.

## Verification

From the repository root:

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.Desktop -c Release --no-build -- --smoke-test
```

The checks exercise keyboard/controller state transitions, input repeat, analog
dead zones, focus changes, controller reconnection, save replacement, forced
write failures, invalid saves, and animation timing, step completion, facing,
and reset behavior. A native C++ oracle supplies the expected
digest for all 624 town movement cases (156 cells and four cardinal directions).
Its source and reproduction command are in `Yipe.Tests/town_reference.cpp`.
The old town-only preview restriction has been removed from gameplay; the
reference still verifies the original town movement rules.

The [gameplay check documentation](Yipe.Tests/README.md) explains the independent
RNG, character and map/world references, diagnostic traces and checked-overflow build.

The graphics smoke test opens a real window, loads the packaged assets, renders
three frames, checks the rendered pixel variety, and exits. It needs a working
desktop graphics session. An optional path captures the rendered world (1280 x
720 with the miniature effect enabled):

```sh
dotnet run --project monogame/Yipe.Desktop -c Release --no-build -- --smoke-test /tmp/yipe-town.png
```

Historical P1 movement checkpoint on Apple Silicon/macOS: checked Release build,
188 automated checks, all 624 C++ town movement cases, 8,192 C++ RNG output/state
records, 11,322 character records and 311,728 map/world records. See the
[world and movement evidence](evidence/2026-10-03-world-movement/README.md).
Earlier preview work also included visual inspection and a user-confirmed
in-game save. Controller checks use synthetic input states;
physical-controller testing and Windows/Linux runtime testing remain open.

The [graphics runner](Yipe.GraphicsChecks/README.md) verification passed all 147
checks, including diagonal steps, bridge reflections and the slower walking pace, with isolated
saves and optional FFmpeg recording. It writes unique artifact directories and
does not need FFmpeg unless `--record` is requested:

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Both normal and recording runs passed, including a run from outside the checkout.
See the [current evidence](evidence/2026-10-03-port-baseline/README.md) and
[plan's baseline record](PORTING_PLAN.md#current-baseline).

The retro build was also exercised through the real graphics device: all
four facing directions, walking/standing transitions, pause during movement,
buffer clearing on pause, saving mid-step and loading the accepted destination,
camera clamps on both axes, fullscreen entry/exit, scanline presentation,
native texture dimensions and palette/alpha constraints, and a rendered 16:9
animation capture. All 24 hero cells were inspected for fixed foot placement;
each direction has four distinct walk poses and a distinct idle alternate.
The revised miniature effect passed 34 graphics integration checks, including all three
strengths, progressively reduced distant detail, exact sharp focus bands at the
center and north/south map edges, uniform-color brightness and border coverage,
and unchanged pixels in menu and location-message overlays. The same run covered
pause, saving/loading during movement, camera limits, fullscreen, and scanlines.
A high-contrast stripe probe also verifies gradual contrast falloff, no sudden
change between adjacent display rows, and smooth subpixel focus movement.
The motion-blur update passed 57 graphics checks in total, adding all four
directions, a three-pixel rearward limit, low trail opacity, an opaque character,
fade-out at step completion, and absence of stale trails after blocking or reset.
A six-second 60 fps capture exercises motion alongside the miniature effect.

In a development graphics fixture, the village-scenery update passed 78 graphics
integration checks, adding native
palette/alpha validation for both new atlases, surface-aware footsteps, a connected
river through both bends and bridges, irregular widths, ripples constrained to
water, deterministic pause behavior, entrance-cue conditions, moving shop signs,
and clear paved routes around shops and planted details. The full map, camera-edge views, and pause overlay
were visually reviewed; a new six-second capture covers the combined effects.

To make a framework-dependent desktop folder for the current machine:

```sh
dotnet publish monogame/Yipe.Desktop -c Release -o /tmp/yipe-monogame-retro
```

The output includes assets and resolves them relative to the executable rather
than the working directory. The .NET 10 runtime is required. Native installers,
self-contained releases, signing, and notarization are later packaging work.
