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

The first standalone MonoGame macOS preview is built separately from the C++
release. See [packaging and verification](packaging/README.md) for the
self-contained Apple Silicon app, extraction checks and supported-platform limits.

Use the [port plan and progress tracker](PORTING_PLAN.md) for the full-game
milestones, acceptance criteria, current task, and session resume instructions.

## Run

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet run --project monogame/Yipe.Desktop -c Release
```

During development, build and test locally. Create a new distributable package
only when requested; ordinary visual iterations do not need repackaging.

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

Pause now includes **Help & controls**, **About Quest of Yipe!** and **Edit
controls**. Help shows current gameplay bindings, fixed menu/text controls, the
21-topic gameplay guide and current stat/save information. About
retains recovered game credits and identifies the reconstruction.

Edit controls changes seven gameplay keyboard shortcuts and four controller
shortcuts (Actions, Back, Map and Pause). Controller choices use physical button
positions; D-pad and stick movement stay fixed. Duplicate assignments, movement
alias conflicts, modifiers and unsupported buttons are rejected. Escape or East cancels
key capture; reset requires confirmation. Changes save immediately in the profile,
and a failed write leaves them active for the session with visible feedback.
Older profiles receive standard bindings without rewriting the original file.
Menus, maps and text entry always keep the controls in the table above, so you
can return to the editor after changing gameplay keys. WASD and Space remain
gameplay convenience aliases. Remapping does not change adventure saves.

Normal launch begins with a **Casbah-inspired pixel-art icosium labs studio ident**,
crediting the port/remaster developer before the title menu. It lasts 3.2 seconds,
with a short fade in/out; Enter/South, Escape/East or Start skips it. Losing focus
freezes the display timer. Logo art renders on the same crisp native canvas,
without world blur, vignette or scanlines. Direct save loading and graphics smoke
checks bypass the title sequence. About also credits icosium labs.

The first connected controller is used. Button descriptions refer to physical
positions because printed labels vary between controller brands. Movement supports
eight directions: combine two arrow/WASD keys, press a D-pad diagonal, or aim the
stick diagonally. The stick uses eight directional sectors and a dead zone;
slight off-axis input remains cardinal. Held input retains its repeat delay.
Losing application focus or disconnecting the only connected controller pauses play.

The game fills a **16:9 view**, starting in a 1280 x 720 window. A camera follows
the player on both axes across each map, keeping tiles square and
clamping at the map edges. There is no permanent sidebar or menu. Escape / Start
opens a pause overlay containing save/load, new adventure and **Graphics**.
The Graphics submenu contains Fullscreen, Scanlines, Miniature effect, Vignette,
Pixel lighting, Sun shafts, Cloud shadows, Rain, Screen shake and Time of day.
Both menus fit on one page. Escape / East or Start returns from Graphics to the
pause menu without resuming play. Battle pacing remains in the main pause menu.
**Scanlines** is on by default and **Miniature effect** defaults to Strong.
Select Miniature effect with Enter / the south face button to cycle through
**Off / Subtle / Strong**. Scanline, miniature, vignette, pixel-lighting, sun-shaft,
cloud-shadow, rain, time-of-day, screen-shake, battle-pacing and sound preferences persist
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

**Sun shafts** cycles through **Off / Subtle / Strong**, defaulting to **Subtle**.
Warm diagonal bands drift slowly over outdoor scenery, with stepped native-pixel
edges and unequal widths suggesting openings in cloud cover. They stay anchored
to the world as the camera moves, preserve dark outlines, and freeze with the
scene while paused, in menus or unfocused. Caves, Hell, maps and UI are unaffected.
The setting saves independently of localized lighting and other effects.
Walking through a shaft adds a warm upper-edge highlight to the character and
an occasional one- or two-pixel glint on existing silver-colored equipment
details. The highlight follows each pose and fades within about 0.2 seconds
after leaving the light. Lit footsteps on dry grass, roads and sand release two
small dust motes that rise a few pixels and fade within 0.65 seconds. These
effects share the sun-shaft setting and freeze with the scene; switching it Off
also removes the character highlights and dust.

**Cloud shadows** has its own **Off / Subtle / Strong** setting, defaulting to
**Subtle**. Small, separated pixel cloud silhouettes shade outdoor terrain, buildings
and actors. Rounded crowns, side puffs and flatter bases follow the familiar
clip-art cloud shape; size and handedness vary, with open gaps between clouds.
Feathered edges fade gradually into clear ground while the centers retain gentle
density variation. Only the shadow mask is softened; terrain and sprites remain
sharp. Subtle shades by up to 7%; Strong by up to 14%. They move diagonally at a
different speed from the sun shafts, with fractional edge blending for smooth
travel. Passing clouds attenuate the shafts; character highlights, equipment
glints and illuminated dust sample the same moving cloud cover. The field stays
anchored as the camera moves and freezes with the scene. Caves, Hell, maps and
UI are unaffected. Cloud shadows also work when sun shafts are Off. The user
reopened the previously deferred cloud work to request feathered edges; this
revision awaits visual review.

**Time of day** defaults to **Auto**, including older profiles without that setting.
Day, dusk, night and dawn repeat over twenty minutes of elapsed time. A new
adventure starts halfway through the ten-minute day; dusk and dawn last two minutes
each, and night lasts six. Night keeps paths and creatures readable, with cool
ambient color and stronger warm window light. Sun shafts, their character highlights,
cloud shade and outdoor heat fade as daylight disappears. Auto keeps advancing
during pause, menus, combat, maps, storage and focus loss. If the app is suspended,
it catches up the elapsed time on its next update; movement, encounters and combat
keep their existing pause behavior. Underground, the clock continues while caves
and Hell retain their existing lighting. Fixed **Day / Dusk / Night**
modes suspend Auto's phase; preferences, successful saves and normal exit retain
that cosmetic phase in the profile. [Plan and verification](DAY_NIGHT_PLAN.md).

**Rain** offers **Off / Auto Subtle / Auto Strong**, defaulting to **Auto Subtle**
for profiles without a rain preference. Existing Subtle/Strong choices now control
automatic shower intensity; an explicitly saved Off choice stays Off. Clear spells
last 5–10 minutes of active outdoor play, followed by 30–60 second showers with
four-second transitions. Indoors, menus, map views, focus loss and Rain Off suspend
the weather timer. It starts clear when the app launches and continues across
outdoor map changes; weather progress is separate from adventure saves.

Rain adds short diagonal pixel streaks, tiny two-pixel ground splashes, and
expanding rings clipped to the actual shoreline. Strong doubles the peak number
of drops. Impacts draw beneath characters and buildings; bridge decks receive no
water rings. Rain follows the world camera and preserves gameplay and its random
stream. Caves, Hell, map views and UI remain dry. Rain audio is not included.

Combat keeps the hero and enemies visible through commands, spell/item selection
and results. The hero lunges and strikes, then each surviving enemy counterattacks
in the original slot order. Impact sounds, recoil, flashes and outlined damage
numbers follow those events: gold for outgoing damage, coral for incoming damage,
and green for healing. Zero-damage attacks receive a neutral label. HP, SP and AP
stay visible, and enemy health bars animate with a trailing damage segment.

Walking now produces quiet footsteps matched to grass/dirt, stone, sand,
wooden bridge decks, visible water and snow. Three short variations per material
use CC0 recordings by Nox_Sound, Kenney and Fantozzi, replacing the initial
synthesized sounds. [Sources and credits](Yipe.Desktop/Content/Audio/Footsteps/README.md)
ship with the game content. Footsteps follow the existing Sound and Volume
settings. Each accepted walking step
produces one planted contact; pause, maps, action panels and focus loss silence
it. Important effects keep priority. Idle time, blocked movement, restores and
passive platform travel do not produce footsteps. [Sound previews and checks](evidence/2026-10-08-recorded-footsteps/README.md).

The **Monster Breeding Grounds** (upper A4/B4/A5/B5 and deep A5/B5) have a dedicated
pixel-art terrain pass: connected slate rock with Hell-scale stone detail, shaded
cliff faces, broad slate beds with sparse shallow joints and worn-earth deposits,
fine grit, nests, shells, scales, bones and claw marks. World creatures share a subtle cool cave tint and stronger contact
shadows; their existing animation and battle artwork are retained. The
entrance, communal nests, feeding corner, holding cell, brood chamber and large
monster den each have authored details. Small fungi cast cool local light;
entrance and holding-area lamps cast warm light. Drips, tiny ripples, spores and
falling grit follow the pausable scene clock. Map layouts, secret appearances,
encounters and rescue positions retain their original behavior.

Light occlusion and short ground-contact shadows follow the visible chipped
stone contours. A one-native-pixel light feather preserves the pixel-art style
without square tile cutoffs. Closed doors and undiscovered areas still block light.

A quiet wind-and-drips loop uses JaggedStone's CC0
[Loopable Dungeon Ambience](https://opengameart.org/content/loopable-dungeon-ambience).
The full loop is retained as stereo PCM audio, fades in on exploration, pauses
with menus/maps/focus loss, follows Sound/Volume and releases when leaving these
six rooms. [Credits and conversion record](Yipe.Desktop/Content/Audio/Ambience/sources.json)
ship with the content. [Room preview](evidence/2026-10-09-breeding-floor-soft-seams/gameplay-59.png)
and [floor verification](evidence/2026-10-09-breeding-floor-soft-seams/review.json).

The **Fire Cave** now feels volcanic across all eleven upper and deep rooms:
finely fractured basalt, shaded rock faces, quiet charcoal/ash floors and animated
magma recessed into existing blocked terrain. Cooling crust, small bubbles,
sulfur flecks and cooled shards give the rock a volcanic material palette.
The floor uses irregular cooled-lava plates, shallow folded flow ridges, fine
ash/grit and sparse sealed ember seams. Long unstable-floor warning cracks remain
distinct. [Floor preview](evidence/2026-10-09-fire-floor/gameplay-67.png).
The wall joints glow ember red, with brighter orange near magma, and red ambient
light reaches the entire room. The deep chambers have a stronger red cast.
Local illumination and contact shadows follow the visible contours; localized
heat moves whole pixels while preserving the hero and UI. Embers, intermittent
smoke and falling ash follow scene time, with stronger heat and activity below.
World monsters share warm cave light and grounded shadows. Original routes,
doors, unstable floors, secrets and rescues retain their behavior.

A 58-second ambience mix combines Blender Foundation's CC-BY-3.0
[Lava loop](https://opengameart.org/content/lava-loop) and PagDev's CC0
[Fireplace Sound loop](https://opengameart.org/content/fireplace-sound-loop).
It grows stronger near magma and in the deep chambers, follows Sound/Volume,
pauses with menus or focus loss and yields to action sounds. Existing sampled
stone/grit footsteps match the new ground. [Credits](Yipe.Desktop/Content/Audio/Ambience/FIRE-CAVE-CREDITS.txt)
ship alongside the audio. [Entrance preview](evidence/2026-10-09-fire-floor/gameplay-72.png)
and [implementation evidence](evidence/2026-10-09-fire-cave/README.md).

The **Final Cave** has an ancient, ominous treatment across all fifteen upper/deep
rooms. Connected dark rock surrounds broad worn bedrock slabs, shallow abrasion,
and small chips at the wall feet. Embedded crimson crystals illuminate the actual
rock contours; native-pixel ash, falling grit and thin ground haze remain sparse.
Creature tint, contact shadows and landing dust share the cave palette. Collapsing
floors retain clearly visible fissures and open-hole stages, and secrets keep the
same appearance as ordinary terrain.

A quiet 58-second loop combines recorded dungeon air and low rumbles with sparse,
slowed crack recordings for distant stone creaks. Both sources are credited CC0
recordings already used by the port. It follows Sound/Volume, pause, menus, focus
and cave transitions through the existing audio system. [Credits and processing](Yipe.Desktop/Content/Audio/Ambience/final-cave-sources.json).
Original routes, doors, water, platforms, encounters and rescues remain intact.
[Gameplay preview](evidence/2026-10-11-final-cave/gameplay-92.png),
[atmosphere clip](evidence/2026-10-11-final-cave/atmosphere.mp4), and
[verification](evidence/2026-10-11-final-cave/review.json).

The **Water Cave** now has a dedicated flooded-limestone treatment across thirteen
upper/deep rooms. Connected eroded rock and dark wet faces surround quiet worn
paths, mineral deposits and silt. Dark turquoise pools have irregular banks,
subtle caustic fragments, drips, ripples and thin mist. Visible algae colonies on
front and side faces cast contour-clipped turquoise light. Hero reflections ripple
only on real water and stay below the rafts; creatures share a restrained damp
tint and contact shadows while battle artwork stays unchanged.

A dedicated 58-second ambience loop mixes the existing CC0 cave/drip recording
by JaggedStone with Thimras's recorded river, quietly filtered and crossfaded.
It becomes slightly stronger near pools and follows Sound/Volume, pause, menus,
focus and cave transitions. [Credits and processing](Yipe.Desktop/Content/Audio/Ambience/water-cave-sources.json).
Normal movement, all raft rules, routes, doors, secrets and rescue positions are
preserved. [Gameplay preview](evidence/2026-10-10-water-cave/gameplay-62.png),
[motion clip](evidence/2026-10-10-water-cave/water-cave.mp4), and
[verification](evidence/2026-10-10-water-cave/review.json).

Water Cave rafts now glide between their accepted tiles, with a shared one-pixel
bob for the logs and rider. The hero's boots sit on the visible deck and stay
planted during travel. Boarding follows the moving deck, disembarking uses normal
movement, and landing on the logs uses a wood footstep. Small pixel ripples show
buoyancy. Pausing freezes rider and raft together; loading aboard restores their
alignment. Routes, exploration-tick timing and save data remain unchanged.
[Raft preview](evidence/2026-10-11-water-raft/raft-glide.mp4) and
[verification](evidence/2026-10-11-water-raft/review.json).

The **Ice / Zephyr Cave** has a dedicated frozen interior across all eleven
upper/deep rooms: connected faceted ice and mineral rock, shaded wall faces,
broad ice sheets with sparse submerged fractures, frost pockets and icicles.
The hero and monsters cast faint blue reflections on polished ice, weakened by
frost and cracks. These follow their actual poses, travel, scale and hover height,
fade away from the feet, and stop at walls, doors and unexplored ground. Solid ice
keeps the reflection still; ordinary water retains its existing ripples.
Frozen floors now have reduced traction: a 340 ms accelerating push-off, 220 ms
held glide, and one 420 ms decelerating tile after releasing or changing direction.
Gliding holds the pixel-art foot pose; only push-offs play a footfall. Automatic
drift stops before walls, doors, stairs, room edges and other floor materials.
Encounters, menus, pause and loading clear momentum. Movement elsewhere keeps
its existing timing. [Movement preview](evidence/2026-10-10-ice-movement/ice-slide.mp4).
Small embedded crystals cast cold light around the actual rock contours;
occasional glints and drifting frost follow the pausable scene clock. Creatures
share a cool tint and grounded shadows while retaining their existing animation
and battle artwork. Original routes, secrets, stairs, doors and rescues remain.

A quiet 58-second recorded wind/ice loop follows Sound/Volume, pause, menus and
focus, and becomes quieter in deep chambers. It uses CC0 recordings by Thimras
and ecfike; [credits and processing](Yipe.Desktop/Content/Audio/Ambience/ice-sources.json)
ship with the content. Hard frozen floors retain solid stone footfalls.
[Reflective-floor preview](evidence/2026-10-10-ice-reflections/gameplay-53.png) and
[verification](evidence/2026-10-10-ice-reflections/review.json).

**Zephyr's snow basin (surface A3)** now has connected cold-white snow with small
shaded drifts, partly melted grass edges, gradually snow-dusted cliffs, two snowy pine variants
and a snow/ice-edged dark cave arch. Rock and fallen-branch details stay sparse.
The basin always has light snowfall, even in clear weather or with Rain Off.
Passing storms add heavier flurries. Precipitation becomes small
drifting flakes inside the visible cold basin, with ordinary rain outside it.
Brief boot impressions and powder puffs follow accepted planted walking contacts,
clip to actual snow and fade with the scene clock. Recorded CC0 snow crunches use
the existing Sound/Volume settings. Snow weather/contacts freeze with gameplay
while Auto day/night continues; maps, fog and UI retain their normal behavior.
[Plan](SNOW_BIOME_PLAN.md), [art, previews and verification](evidence/2026-10-08-snow-biome/README.md).

Monster movement now animates accepted roaming and pursuing steps over **250 ms**,
with ground shadows following the visible position. Animated creatures face their
travel direction. New spawns and map changes start at their own location; a
pending encounter waits for the visible
approach to finish. Exploration panels, the map and pause freeze travel.

All **41 canonical monsters** now have sixteen native pixel-art poses each for
idle, movement, wind-up/strike/recovery, hurt and defeat: **656 base poses**.
Their travel follows their anatomy: alternating foot plants for walkers, wing
assisted hops, airborne spirits, scuttling crabs, slithering eels and compressing
worms. Heavy creatures ease into each 250 ms step and hold the final plant briefly.
Dry footfalls add restrained dust; water creatures crossing actual water tiles
produce small ripples clipped to the shoreline. Hidden movement stays quiet.
The regional bosses retain their doubled world footprint. The waiting Gatekeeper
animates in the world, and its empowered encounter has a distinct palette and brace.
Battle poses follow the existing resolved turn timeline; enemy inspection stays still.
Combat rules, AI, RNG and saves are preserved. See the [full roster plan](MONSTER_ANIMATION_PLAN.md)
and [verification gallery](evidence/2026-10-07-monster-roster/README.md).

A brief outlined amber **!** appears above a roaming or pursuing creature on its
first visible step toward the hero. It follows the creature and fades within
0.7 seconds, staying quiet for the rest of that visible chase. Hidden and offscreen
approaches stay quiet. The cue resets on replacement, map changes or loss of
visibility, freezes with exploration panels/maps/pause, and stays sharp above
world effects. It adds no delay or change to encounter rules.

**Left/right** selects a living enemy on the main battle menu and damage-spell power
choices, preserving the selected command or power. High power previews every
affected enemy. Healing previews the hero; Rabbit Replacement previews the actual
companion selected by the original rule. **Escape** (or the controller's East button)
attempts to flee from the main battle menu. In spell, inventory and enemy-info
screens, it goes back instead.
**Enemy info** opens a free inspection panel from the battle commands. Left/right
browses living enemies while your actual attack target stays unchanged; Back or
Escape returns to the same command. A dedicated card shows one enlarged enemy
portrait with HP, rank, and spaced attack/defense ratings. **Details** switches to
attack-spell resistance, recorded defeats and empowered status when present;
**Stats** returns to the overview. Up/down selects these commands or Back.
Transformed enemies use their live stats. Imported incomplete histories are
labeled as recorded defeats.
Inspection uses no turn and never advances automatically, even with Auto pacing.

Spells and abilities have distinct pixel effects, including healing crosses,
insects, bite marks, suction, contracting magic, transformation puffs and auras.
Victory adds enemy breakup particles, a gold/XP panel and a celebration for actual
level-ups. Exact rewards, upgrades and level gains remain available in paged text.

**Battle pacing** is a separately saved **Manual / Auto** pause-menu preference,
defaulting to **Manual**. Auto returns to commands after ordinary single-page
results finish animating. Victory, defeat, completed escapes, failures and
multi-page results wait for confirmation. Continue can advance immediately.
**Screen shake** is a separate saved pause-menu toggle, **Off** by default; On adds
a short shake of at most one native pixel per axis. Combat feedback pauses with
the game and does not change combat rules, rewards or gameplay randomness. See
the [battle upgrade plan](BATTLE_UPGRADE_PLAN.md) for scope and verification.

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
pause freezes actors and scene animations while overlays can still animate and
Auto day/night time continues.
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

Quizilog (surface B2) and Ervyn (surface C5) now share Maynard's shop signs,
facade shading, cast shadows, glowing forge windows and chimney smoke.
Quizilog has warm paved lanes through its existing palm groves, with trade
crates, pottery, an anvil and desert planting. Ervyn has a connected garden
loop, flower beds, shrubs and small drifting butterflies. These additions use
the existing pixel-art assets. Paths appear in the area map and select stone
footsteps; the original collision, shop approaches and Quizilog quest remain
intact. [Updated views](evidence/2026-10-08-town-upgrades/Quizilog.png) and
[Ervyn](evidence/2026-10-08-town-upgrades/Ervyn.png).

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
