# Yipe III MonoGame port plan and progress

Port the complete supplied Yipe! III game to C# and MonoGame, preserving its
gameplay and content while applying the visual style, controls, and presentation
already approved by the user. This document is the working checklist and handoff
record for the port. Update it during implementation so a later session can
resume from repository evidence without needing the conversation history.

Last updated: **2026-10-05**. **P1, P2, P3 and P4 are complete.** The continuous
P2–P4 goal delivered the connected opening, complete exploration and earned
campaign ending with the approved retro presentation. The [completion evidence](evidence/2026-10-04-p234-complete/README.md)
contains runnable inputs, full saves, independent comparisons, screenshots and
fresh-process frontend restart results. P5/P6 release work and P7 official console
integration remain separate; this is a playable desktop port, not a release claim.

## Resume here

| Field | Current state |
| --- | --- |
| Current milestone | P1–P4 Done. P0 support work and P5/P6 remain; P7 deferred. |
| Next task | Mountain/cave stone correction is implemented and graphics integration is complete. User review of the darker stone remains pending; other presentation features and P5/P6 remain queued. See the current GPU captures in the cave-stone evidence packet. |
| Last completed work | Matched mountain stone to the cave's darker slate gray, retained small authored highlight cores, and lifted only the cave's outer border to the same gray. Shared native art import covers town, world, area maps and desert foothills. References: Maynard E3 / 1,1 and F3 / 7,4, near the original F3 / 2,6 ridge report. See [cave-stone evidence](evidence/2026-10-05-cave-stone/README.md). |
| Latest verification | Checked Release build: zero warnings/errors. 1,203 current GPU integration checks pass, including 360 gameplay tour frames, all 36 surface mountain layouts, 40,960 matching overlap pixels, cave/stone brightness, dark doorway preservation, secret equivalence, fog and save/RNG isolation. The earlier graphics-context blocker is resolved. |
| Earned frontend evidence | Opening matches all 42 C++ checkpoints plus King reward and separate-process continuation. Late frontend opens an earned C# save, defeats the empowered Gatekeeper, rescues 19, saves, restarts in another process, rescues 20 and displays the live Maynard finale. |
| Approved presentation | 16:9 play, overlay menus, retro palette, reflections, 250 ms walking at four animation frames/second, scanlines on and Strong miniature by default. Optional vignette defaults to Off; localized pixel lighting defaults to On. |
| Work left running or half implemented | No incomplete implementation or running task for the cave-stone correction. The user's game was not navigated or restarted; graphics checks ran their own isolated game. Preserve the accepted native rock detail, cave/Hell artwork and existing effects. |
| Blocking dependencies | Physical-controller/other-OS verification needs the actual environments; official console work needs approved access. |
| Save preservation | Mountain checks use isolated saves and profiles and compare complete gameplay snapshots before/after rendering. No gameplay, collision, encounter, map-template or save-format code changed. `.y3m` version 1 and `.map.json` remain unchanged. Earlier lighting evidence retains its personal save/journal/profile hashes. |
| Repository checkpoint | The first MonoGame checkpoint on main is titled `Add complete MonoGame desktop port with retro presentation`, based on C++ commit 349fffe71e052dd4e011f1fe09c0e3080f5bb723. It includes the port, artwork, fixtures, evidence and resume documentation. Use `git log -- monogame/PORTING_PLAN.md` to locate it. Evidence hashes describe files at capture time; later documentation updates may differ. |

At the start of a resumed session:

1. Read this section, the current task checklist, [MonoGame README](README.md),
   and the relevant C++ source and tests. Check applicable repository instructions.
2. Inspect `git status --short` and the current source. Preserve existing edits;
   do not reset the working tree to the checkpoint above.
3. Confirm that referenced evidence exists. A recorded successful run is not a
   fresh verification of changed code. Use the commands below when verification
   is needed; recover missing fixtures instead of assuming they are available.
4. Select the next unchecked task whose dependencies are satisfied. Record it
   here as active and implement a bounded, reviewable part of the game.
5. Before finishing, update task status, validation evidence, unresolved issues,
   and this resume table. Leave an exact next action, including any unfinished
   edits or failing checks. No necessary handoff information should exist only
   in chat, process memory, or a temporary directory.

Suggested future instruction: **“Read `monogame/PORTING_PLAN.md`, reconcile it
with the working tree, and continue from the next unfinished task.”**

## Implemented: mountain form and cave stone — 2026-10-05

Replace the staircase of rounded tile blocks and repeated cobblestone fill with
larger coherent rock masses, long irregular slopes and distinct cliff faces.
Keep the current retro palette, crisp native pixels, meadow/desert contact,
blocked cells, narrow routes, secret equivalence, cave entrances and saves.
Use the verified map-18 capture from `yipe-graphics-20261005-072443-67d9453e`
as the before image; surface F3 / 2,6 is grass west of the diagonal ridge.

- [x] Rework contours across the connected formation, not just each small corner.
- [x] Keep authored rock detail at its original scale, with lighter tops/darker textured faces; remove the rejected large procedural facets.
- [x] Keep the shared village/world/area-map implementation and update connected desert foothill sampling.
- [x] Inspect native CPU previews of F3 plus meadow, desert, cave entrances and narrow passages.
- [x] Validate native terrain, seams, secrets, mutable terrain and gameplay isolation; retain evidence.
- [x] Match mountain stone to the cave rim and preserve the dark tunnel, native detail and silhouettes.
- [x] Run the current graphics integration suite and capture actual gameplay/post-effects after graphics-context access was restored.

Implementation: connected ridge bends use broader cuts while thin tips retain
solid centers. Following the user's rejection of the broad flat gray patches,
the 48×40 procedural facets and enlarged texture sampling were removed. Rock
uses the original atlas at native scale, matching the cave rim's small clusters.
The same contour defines 13–16-pixel front faces with darker textured shading;
only the existing rock palette is used. Palette matching alone did not preserve
the art style: future changes must also match the scale and detail of existing sprites.
No black perimeter or filtered gradients were added. Routes, collision, entrances,
encounters and saves remain untouched; the renderer still draws terrain before
actors and uses the existing per-cell visibility/fog rules.

The latest correction uses the cave's darker slate in `RetroArt.Load`, replacing
green-gray mountain fill and reducing bright highlights to the existing clusters'
cores. Only the cave sprite's exterior two-pixel shadow band is lifted to this
same slate; the recessed arch and bottom threshold keep their original shadows.
Both terrain and cave silhouettes are unchanged. The same import also covers
the isolated and connected desert foothill artwork.

The graphics-context blocker is resolved. The current checked Release build has
zero warnings/errors and **1,203 GPU integration checks** pass, including 360
gameplay tour frames, surface layouts, 40,960 matching region-overlap pixels,
native colors, solid blocked centers, secrets, cache invalidation, ground contact,
fog, brightness matching, darker fronts and full gameplay save/RNG isolation.
An independent comparison of the actual native atlases confirms that only 93
external cave shadow pixels changed; the connected dark tunnel's 143 pixels,
all alpha values and every other atlas cell are unchanged.

Current GPU captures, comparisons and logs are in [the cave-stone evidence packet](evidence/2026-10-05-cave-stone/README.md).
The earlier 530 core and 249 CPU passes remain historical evidence for the form
revision. Its CPU harness imports older native art, so it does not exercise this
latest import correction. Use the current GPU runner for resuming validation.
User review of the appearance is pending; no new shaders or art assets were added.

## Surface mountain barriers — 2026-10-05 (previous revisions)

Goal: replace separate square mountain cells with connected, irregular formations
without changing the pixel-art style. Reuse the original rock texture and palette;
use binary transparency, stepped contours and restrained ground-contact details.
Collision, encounters, cave approaches, secret routes and saves must not change.

- [x] Share the treatment between town, outdoor gameplay and area maps.
- [x] Join neighboring rock; weather exposed edges/corners and vary texture subtly.
- [x] Ground meadow/desert bases and retain walkable mountain-foot strips.
- [x] Align texture/contours across overlapping region edges, including world wrap.
- [x] Cover palette, joins, hidden variants, mutable terrain, fog and gameplay isolation.
- [x] Retain before/after evidence and record the finished implementation here.

`MountainTerrain` caches one native-resolution layer for each renderer. It samples
the unchanged mountain atlas, trims only exposed edges in small pixel steps,
and retains a solid core in every blocked cell. Sparse ground-contact accents,
moss and stone chips stay inside mountain cells. Desert cutouts reveal the
existing sand texture. Authored walkable foothills continue into the rock.
No new gradients, filtering, bloom, palette, gameplay state or art assets were added.

Both renderers use the same terrain preparation, including the sand just beyond
Maynard's northern boundary. Neighbor lookups account for the original two-cell
region overlap and world wrap. Rock facets retain their original pixel shapes;
secret mountain cells use precisely the same artwork. Tile
changes invalidate the layer; ordinary frames perform no terrain pixel upload.
Earth/Hell walls, cave sprites and existing visual-effect settings are unchanged.

Checked Release and the existing suites pass: **530 core checks**, **1,190 graphics
checks**, and **40,960 identical overlap pixels**. All 115 regions were captured;
only the 25 containing mountain tiles differ from the prior lighting build.
The new checks cover all 36 surface regions, native palette/binary alpha, joined
rock, weathered silhouettes, cache invalidation, secrets, sand cutouts, foothill
walkability, fog, town/map agreement and unchanged save/RNG snapshots.
Visual captures are retained for user review; no interactive art review was
performed with the user's live game.

Evidence: [Surface D4 before/after, left/right](evidence/2026-10-05-natural-mountains/d4-before-after.png),
[Maynard before](evidence/2026-10-05-natural-mountains/map-017-before.png) /
[after](evidence/2026-10-05-natural-mountains/map-017-after.png),
[desert F1 before](evidence/2026-10-05-natural-mountains/map-006-before.png) /
[after](evidence/2026-10-05-natural-mountains/map-006-after.png),
[village cave gameplay](evidence/2026-10-05-natural-mountains/village-cave-gameplay.png),
[graphics log](evidence/2026-10-05-natural-mountains/graphics.log),
[core log](evidence/2026-10-05-natural-mountains/core.log),
[map comparison](evidence/2026-10-05-natural-mountains/map-comparison.txt), and
[source hashes](evidence/2026-10-05-natural-mountains/source.sha256).
Full run: `build/graphics-checks/yipe-graphics-20261005-070421-ba97a3c4`.

**Outline correction:** the user found the continuous black rim unnatural.
Removed both the near-black rock border and the continuous dark ground band.
The original texture now reaches the exposed silhouette, with no uniform highlight
band. Only the brightest facets at the foot darken one existing palette shade;
sparse moss/soil accents use the surrounding ground colors. Pixel steps, binary
alpha, original artwork, passages and neighboring-region joins are preserved.

Checked Release and **1,191 graphics checks** pass. The added regression rejects
near-black outlines on an exposed formation. Exactly 25 mountain captures changed
from the first implementation; all 90 other regions remain identical. Evidence:
[outlined before / corrected after](evidence/2026-10-05-natural-mountains/edge-correction/before-after.png),
[corrected town](evidence/2026-10-05-natural-mountains/edge-correction/map-017.png),
[corrected desert](evidence/2026-10-05-natural-mountains/edge-correction/map-006.png),
[graphics log](evidence/2026-10-05-natural-mountains/edge-correction/graphics.log),
[map comparison](evidence/2026-10-05-natural-mountains/edge-correction/map-comparison.txt),
[source hashes](evidence/2026-10-05-natural-mountains/edge-correction/source.sha256).
Full corrected run: `build/graphics-checks/yipe-graphics-20261005-071213-692c8bdb`.

**Ground integration revision:** the user still found the formations out of place
after removing the outline. Grass/moss and sand now overlap broken sections of the
rock foot, using the existing meadow and desert colors. Paved approaches receive
no grass fringe, and cave mouths are excluded from this overlap. Wider pixel
steps and rounded corners reduce edge chatter; the original rock texture is no
longer displaced, preserving its authored facets. Highlights near the ground are
quieter, with no new palette, blur, continuous outline or smooth gradient.

The checked Release build and **1,194 graphics checks** pass, including grass/sand
contacts, paved approaches and all previous mountain safeguards. Exactly 25
mountain regions changed; all 90 other captures remain byte-identical. This is an
implemented revision awaiting the user's visual judgment, not recorded acceptance.
Evidence: [before/after](evidence/2026-10-05-natural-mountains/ground-integration/before-after.png),
[town](evidence/2026-10-05-natural-mountains/ground-integration/map-017.png),
[desert](evidence/2026-10-05-natural-mountains/ground-integration/map-006.png),
[graphics log](evidence/2026-10-05-natural-mountains/ground-integration/graphics.log),
[source hashes](evidence/2026-10-05-natural-mountains/ground-integration/source.sha256).
Full run: `build/graphics-checks/yipe-graphics-20261005-072443-67d9453e`.

## Accepted visual-feature backlog — 2026-10-05

Keep these improvements subtle and consistent with the crisp retro artwork.
The user approved all six recommendations and selected localized lighting first.

| Feature | Status | Intended behavior |
| --- | --- | --- |
| Localized pixel lighting | Done | Windows, wall torches and lava light nearby scenery and actors with restrained flicker, stepped shading and a saved toggle. Respects walls, fog and existing gameplay. |
| Moving cloud shadows | Queued | Broad, faint shadows drift across outdoor scenery. |
| Rain and surface splashes | Queued | Fine rain, small ground splashes and water rings, with adjustable intensity. |
| Combat feedback | Queued | Brief hit flashes, impact bursts, damage numbers and optional light screen shake. |
| Walking puddle ripples | Queued | Footsteps create small water rings and disturb the character's reflection. |
| Dusk/night palettes | Queued | Selectable sunset and moonlight palettes with readable terrain and illuminated windows; a day/night cycle can follow later. |

Weather, time-of-day lighting and screen shake should have separate controls.
Do not implement the queued features as incidental parts of localized lighting.

## Scope and fidelity

- Preserve the original maps, story and dialogue, quests, combat rules,
  equipment, consumables, spells, progression, secrets, difficulty choices,
  death recovery, and ending contained in the supplied game.
- Use the C++ reconstruction as the implementation reference. Consult recovered
  data and original observations when behavior is uncertain. Its documented
  adaptations and unresolved questions remain explicit; C++ agreement alone
  does not establish complete parity with the original executable.
- Keep the approved retro graphics and presentation. New terrain, characters,
  monsters, menus, and effects should follow the existing art contract.
- Keep the approved 250 ms walking step and four-frame-per-second walk cycle.
  Test original timed routes at that pace. Animation speed must not change RNG
  consumption, combat formulas, reward order, or exploration timer frequency.
- Rebuild interfaces as controller-friendly overlays with the original actions
  and information. Further gameplay or balance changes need a recorded design
  decision; do not introduce them as incidental translation fixes.
- Develop and verify the complete desktop game first. Official Switch integration
  is a later milestone with separate platform access and hardware dependencies.
  Emulator or homebrew builds are not the selected release path.
- Account for every original feature in [the coverage inventory](../docs/FEATURE_COVERAGE.md).
  Desktop conveniences such as printing, recent files, and historical registration
  information need explicit dispositions rather than silent omissions.

## Current baseline

| Area | Implemented and verified | Remaining work |
| --- | --- | --- |
| World | 112 static maps, three generated maps, all events/transitions/visibility, 145 chests, 186 placements, 22 pursuers, roaming and timed hazards/platforms | Other-platform and final visual/performance review in P5/P6 |
| Character and gameplay | Creation, twelve appearances, all combat/spells/shops/casinos, twenty rescues, six stamps, secrets, Hell/recovery, statistics and ending | Three additional class/difficulty/seed completion fixtures in P6.1 |
| Randomness and fidelity | Independent rule comparisons plus 43,080 earned campaign state/RNG observations and 66 complete saves | Original-runtime unknowns remain inherited; do not infer original-executable parity |
| Rendering/audio | All 115 map instances and 97 tile variants covered; monsters, quest art, audio effects/aliases, overlays, original defaults | Supported-size polish, physical input, accessibility and cost review |
| Input | Keyboard/D-pad/stick actions and synthetic input coverage; real frontend opening/late play | Physical-controller checks, remapping and remaining desktop conveniences |
| Saving | Full-state format, explicit safe imports, generated/static reopen, platform saving and early/late fresh-process continuations | Wider lifecycle/OS matrix and file chooser/history/Save As |
| Profiles | Scanline/miniature/vignette/pixel-lighting/audio settings, score tables, original score code and Silly Mode | Remaining display/input preferences and clipboard/help/profile information UI |

The user approved the overall appearance and direction after the reflection and
walking-speed changes. Preserve that baseline while expanding functionality.
The detailed asset and animation contract is in
[Retro artwork](Yipe.Desktop/Content/Art/Retro/README.md).

Historical P1 baseline verification on 2026-10-04 is recorded in
[the P1 completion evidence](evidence/2026-10-04-p1-complete/README.md):

- Locked restore and checked Release rebuild: zero warnings/errors.
- **478 core/input/save checks**, retaining the 624 town, 8,192 RNG, 11,322
  character, 311,728 map/world and 21,511 session C++ reference records. Added
  2,640 generated-map/RNG comparisons and 24 C++ native/original fixture cases,
  each with four reopen/item cycles comparing complete persistent state and maps.
- Separate writer/reader processes verify conversion and 96 reopen/item/save
  cycles against those C++ results. The original version 1 compatibility fixture
  and static full-state restart remain unchanged. No campaign completion is claimed.
- **164 graphics checks**, 360 tour frames, recording off. Explicit imported-file
  startup works; generated/unsupported or raw preview loads preserve the live
  session/RNG. All earlier effects and full-load failure checks remain passing.
- Pre-existing user save-directory files retain their fingerprints. The
  [town](evidence/2026-10-04-p1-complete/default-town.png) and
  [menu](evidence/2026-10-04-p1-complete/save-game-menu.png) captures retain the
  approved presentation and pacing; the menu was visually inspected.
- Physical controllers, Windows/Linux and console execution remain unverified.
  Root CI/release workflows still package C++. Import uses a CLI until the P2
  new/load screens exist; policy and usage are in [SAVE_FORMAT.md](SAVE_FORMAT.md).

## Milestones

Statuses are **Planned**, **Ready**, **In progress**, **Blocked**, **Done**, and
**Deferred**. Dependencies are noted beside the status. A checked task
means implemented and verified in its stated scope. A milestone is Done only
when its acceptance criteria are satisfied and evidence is linked. Do not
derive an overall completion percentage from lines of code or map counts.

| ID | Milestone | Status | Completion criterion |
| --- | --- | --- | --- |
| V0 | Approved visual preview | Done | Existing Maynard presentation accepted by the user; baseline described above |
| P0 | Reproducible baseline | In progress | Repository checks, reference inputs and feature tracking support continued work |
| P1 | Character and game state | Done | Real character state, inventory, progression and safe full-state saves work independently of rendering |
| P2 | Opening adventure | Done | Create a character, meet the King, shop, leave town, fight, rescue, return for a reward, and save/reload earned progress |
| P3 | Complete exploration | Done | Every recovered map and movement/event mechanism works and has appropriate presentation |
| P4 | Complete campaign | Done | All gameplay systems and content allow earned progression through the ending |
| P5 | Complete presentation and controls | In progress | Every reachable interaction has finished artwork, readable overlays, audio and complete input coverage |
| P6 | Desktop release verification | Planned; needs P3 through P5 | Reproducible complete runs, restart tests and verified MonoGame packages pass |
| P7 | Official console integration | Deferred | Approved backend, platform services and target-hardware testing satisfy the eventual release requirements |

### P0 Reproducible baseline

- [x] **P0.0** Save this plan, link it from both READMEs, and preserve the available
  preview graphics log and reference captures in the repository tree.
- [x] **P0.1** Recovered the temporary fixture into
  [Yipe.GraphicsChecks](Yipe.GraphicsChecks/README.md), with relative project
  references, unique configurable outputs, isolated saves and optional recording.
  Both 139-check runs passed; the recorded run worked outside the checkout.
  [Evidence](evidence/2026-10-03-port-baseline/README.md).
- [ ] **P0.2** Locate or regenerate the C++ completion inputs and expected traces
  referenced by `docs/PROGRESSION_AUDIT.md`. Verify their source revision and
  repeatability. Store durable inputs, reproduction instructions and summaries;
  large traces may use an explicitly documented artifact location. This can run
  alongside P1 and must not hold up independently testable core work.
  **Partial recovery:** a new Combat/Medium/1 completion is retained and freshly
  reproduced in the P2–P4 evidence. The three other historical fixtures remain
  absent; regenerate or recover them for P6.1 before checking P0.2.
- [ ] **P0.3** Add C# build/test CI and a separate graphics-check procedure where
  a real display is required. Clearly distinguish C++ and MonoGame artifacts.
- [x] **P0.4** Reconcile every coverage-inventory feature with a port task or an
  explicit presentation/platform disposition. Record inherited adaptations from
  `docs/DECISIONS.md`, especially D7 and D8, and retain unresolved evidence limits.
  Completed in [FEATURE_DISPOSITIONS.md](FEATURE_DISPOSITIONS.md); assigned P5/P6
  work stays open until implemented and verified.
- [ ] **P0.5** Establish a source-control checkpoint containing the approved
  preview, plan and evidence when committing is requested or otherwise authorized.
  Local files currently exist outside the recorded HEAD; do not treat the old
  commit as a recoverable copy of the MonoGame baseline.

### P1 Character and game state

- [x] **P1.1** Ported [MsvcRandom](Yipe.Core/MsvcRandom.cs) with explicit 32-bit
  wrapping. Verified 8,192 outputs and post-draw states against C++, boundary seeds,
  default sequence and restored continuation, including checked compilation.
  [Reference and reproduction](Yipe.Tests/README.md). Rendering does not use it.
- [x] **P1.2** Ported [Player](Yipe.Core/Player.cs): presets and random rerolls,
  difficulty/specialty, stats, HP/SP/AP, XP, level growth, equipment, inventory,
  consumable outcomes and original counters. Preserved signed values, explicit
  counter wrapping and 13 carried versus 30 stored inventory slots. All 11,322
  reference records matched C++; [evidence](evidence/2026-10-03-character-state/README.md).
  Core rules are complete in this scope; creation/item screens, equipment
  transactions and combat remain their later tasks. P1.4 applies queued surface
  relocations and P1.5 persists the character as part of the session.
- [x] **P1.3** Added shared [Map](Yipe.Core/Map.cs), [MapCatalog](Yipe.Core/MapCatalog.cs)
  and [WorldLocation](Yipe.Core/WorldLocation.cs). All 112 static maps and original
  event/chest identities, column-major tiles, full movement/equipment gates,
  edge/depth/relocation rules and instance mutations match C++. `TownMap` now
  delegates loading/movement to this model while retaining preview-save limits.
  Keyboard and controller support eight directions; diagonal steps interpolate
  both axes over 250 ms using horizontal facing from the existing four-direction
  artwork. Destination-only diagonal collision follows C++; side neighbors do
  not add a new restriction. [Evidence](evidence/2026-10-03-world-movement/README.md).
  P1.4 adds session dispatch below; P1.7 now includes generated-map construction
  and restart. At this P1 checkpoint, visibility/timers and wider-world rendering
  were deferred; P2/P3 below now complete those integrations.
- [x] **P1.4** Completed the concrete [GameSession](Yipe.Core/GameSession.cs)
  foundation. It owns Player, WorldLocation, RNG, current map and remembered tile
  changes. Entry filters chests and applies original quest/entry tile changes,
  then persistent edits; reopen restores D7 platform support afterward. Movement
  loads static edge/depth destinations, records surface returns, remembers doors
  and both switch tiles, and executes restoration, fire and linked travel.
  Exploration items use session context and apply queued relocation. Unavailable
  destinations are explicit and do not partially move the session; P1.7 adds
  generated construction and its startup RNG ordering. The frontend now uses session positions for commands
  and preview saves, retaining its town boundary and approved animation.
  [Evidence](evidence/2026-10-03-game-session/README.md).
  Original action codes and authored map events are the handoff to later systems.
  **Arrival hint → message → princess → encounter → chest execution, encounter
  suppression of chests, transient monster reset, visibility, timers and finale
  completion are not implemented by this task.** Connect these in P2/P3 when
  their rules exist; do not infer full event or campaign parity from this mark.
- [x] **P1.5** Added [GameSaveCodec](Yipe.Core/GameSaveCodec.cs), a separately
  identified, versioned binary `.y3m` format with SHA-256 integrity checking.
  Preserves all 65 Player scalars/seven arrays, location, RNG, persistent map
  changes and permanent defeated-placement IDs. Counts, coordinates, progression
  and supported destinations are validated before constructing a new session.
  Desktop storage snapshots before async I/O, flushes a unique same-directory
  temporary file and replaces the destination. Pause-menu Save/Load game now uses
  this format; successful loads replace renderer/session together, while failures
  retain live state. Town-position/C++/original files remain untouched.
  A fixed version 1 fixture and separate-process static-map restart passed.
  [Format contract](SAVE_FORMAT.md),
  [evidence](evidence/2026-10-03-full-state-saves/README.md).
  Reopening reapplies original entry behavior, including water rearming and D7.
  Transient map state and display preferences are excluded. Generated-map
  loading/restart and import are completed below without changing the v1 layout.
- [x] **P1.6** Implemented explicit source-preserving preview-position migration,
  C++ version 20 `.y3r` import and mapped original `.yp3` import. Older native
  versions require re-saving with the supplied C++ rebuild; unknown versions,
  Unicode CString archives and invalid validation/state are rejected. Preview
  migration creates a default Warrior because only position was stored. Original
  import retains C++ defaults for absent state (including RNG seed 1), chooses
  Windows-1252 name decoding and leaves unknown archive fields in the source.
  `--import-save source destination.y3m` creates a new file only; `--load-save`
  explicitly opens it. Malformed/truncated/cross-format, competing/existing-output,
  source-preservation and CLI failure cases passed. No personal file was converted.
  [Policy](SAVE_FORMAT.md#explicit-import-policy),
  [evidence](evidence/2026-10-04-p1-complete/README.md).
- [x] **P1.7** Completed C++ comparisons for translated P1 rules/state and
  static/generated fresh-process restart. Ported generated construction early
  to satisfy this acceptance criterion: all 2,640 seed/map/interior-entry cases
  match C++ tiles/chests/RNG, including the regression seed. Twenty native and four
  synthetic original fixtures cover full Player/location/RNG/edits/placements,
  queued relocation, entry behavior and four reopen/item cycles each. Separate
  conversion and read processes match 96 save/reopen/item cycles. A saved cave
  regenerates from the saved RNG before pending relocation; the conversion itself
  consumes no RNG. All prior rule comparisons and v1 fixture remain passing.
  **P1 covers the translated session actions. Full arrival quest/encounter/chest
  execution was assigned to P2.5/P2.6/P3.1, as scoped in P1.4. The P1 milestone
  alone does not establish that integration; completed P2–P4 evidence is below.**
  [Oracle/reproduction](Yipe.Tests/README.md#p1-import-generated-maps-and-restart-comparisons),
  [evidence](evidence/2026-10-04-p1-complete/README.md).

### P2 Opening adventure

This milestone must produce a connected adventure using earned resources. Select
and record a real initial rescue route from the reference before implementing
its dependencies; do not assume every cave is an early-game destination.

- [x] **P2.1** Record the chosen start, map transitions, encounters, rescue and
  reward path, including required equipment or abilities and expected checkpoints.
  [Opening route](OPENING_ROUTE.md) and [earned replay evidence](evidence/2026-10-04-p234-core/README.md): all 42 reference checkpoints and first-stamp King return match, with a separate-process earned restart. Frontend acceptance is completed below.
- [x] **P2.2** Add character creation, new/load game flow, King introduction and
  dialogue/choice overlays with keyboard and controller navigation.
- [x] **P2.3** Implement Maynard's required shops and services, purchasing,
  equipment changes, consumable use and status/inventory overlays.
- [x] **P2.4** Open the route out of Maynard and its first destination maps.
  Implement their terrain, entrances, visibility and relevant events.
- [x] **P2.5** Port the required combat loop: targets, attacks, applicable spells
  and abilities, items, fleeing, enemy turns, defeat, rewards and level-up choices.
  Preserve RNG call order, including failed actions and missed attacks. Connect
  session arrival encounters before chests: a resolved or collocated encounter
  suppresses chest opening on that visit.
- [x] **P2.6** Implement the chosen rescue, its reward choices and return-to-King
  progression with the same one-time and repeat-visit behavior as the reference.
  Connect arrival hint/message and princess handling before the encounter phase.
- [x] **P2.7** Finish art, feedback and sound for this route in the approved style.
  Check the complete route with keyboard and controller navigation.
- [x] **P2.8** Complete the adventure from a new character, save midway, reopen in
  a fresh process and finish. Compare rule checkpoints with C++; record any
  deliberate presentation differences separately.

The checked opening tasks are supported by [earned frontend/restart evidence](evidence/2026-10-04-earned-frontend/README.md): controller-style overlay inputs, 116 victories, 14 chests, two rescues, all 42 complete C++ state/RNG checkpoints and the King return. P2.7 is now covered by [the completed opening presentation evidence](evidence/2026-10-04-p2-complete/README.md): 614 graphics checks, all portraits, final route captures and real audio playback/lifecycle. Physical-controller verification remains P5/P6.

P2 acceptance requires ordinary movement, purchases and victories. Debug warps,
injected money, quest flags or loaded completion snapshots cannot establish that
the adventure is completable. They may still support isolated diagnostic tests.

### P3 Complete exploration

- [x] **P3.1** Load and validate all **112 static maps** and their event records.
  Port surface/depth/linked transitions, alternate map sets and return locations.
  P1.3 verifies the catalog and coordinate rules; P1.4 adds static session
  transitions, entry tile/chest initialization and linked travel. Complete the
  remaining arrival, encounter/finale and visibility dispatch with P2/P3 systems.
  Adventure now supplies that dispatch, with the earned ending and full-world checks.
- [x] **P3.2** Completed the **three generated maps** as P1.7's save/reopen
  dependency. Tile output, event chest tables and RNG match 2,640 C++ cases;
  repeated process restarts preserve regeneration/advancement and persistent edits.
  This was the core-loader checkpoint; P3.7 now completes playable cave presentation.
  [Evidence](evidence/2026-10-04-p1-complete/README.md).
- [x] **P3.3** Port visibility, doors, terrain restrictions, movement items,
  strength/key gates, map-entry changes, hidden actions and secrets. P1.3/P1.4
  cover tile restrictions, keys, persistent doors and entry tile changes; visibility
  and hidden actions/secrets now match the independent exploration reference, with live Other/tree-code overlays.
- [x] **P3.4** Port all **145 chests**, location messages and their persistence,
  including guaranteed progression items, full-inventory outcomes and reward order.
- [x] **P3.5** Port exploration timers, regeneration, repellent, moving platforms
  and fire hazards. Dialogues and combat pause exploration without accumulated
  catch-up ticks. Verify puzzle routes and saving aboard moving platforms.
- [x] **P3.6** Port all **186 monster placements**, **22 authored pursuers** and
  roaming encounters, including respawns, bounds, visibility and 2 × 2 monsters.
  Initialize pursuers and clear roaming/temporary defeats when a session loads a map.
- [x] **P3.7** Track map presentation coverage by map/environment. Validate every
  tile and event mapping, camera edge, entrance, route and return path. Keep
  decoration and effect placement appropriate to each map.

P3 is verified by the catalog/movement/generated-map and exploration/encounter
oracles, [all-map presentation audit](evidence/2026-10-04-world-coverage/README.md),
[timed routes](evidence/2026-10-04-timed-routes/README.md) and the
[earned complete campaign](evidence/2026-10-04-p234-complete/README.md).
All maps render without missing tile mappings; gameplay gates and transitions
retain their native rules. Diagnostic map coverage and earned route proof are
recorded separately.

### P4 Complete campaign

- [x] **P4.1** Complete all **41 monster definitions**, weapon damage tables,
  multi-opponent combat, rewards, difficulty rules and special abilities.
- [x] **P4.2** Port all **12 learned spells**, exploration/combat restrictions,
  power levels, resource costs and relocation effects.
- [x] **P4.3** Complete every shop, service, training interaction, equipment tier,
  miscellaneous stock list and progression-dependent price or availability change.
- [x] **P4.4** Port all three casino games and their wager/payout rules.
- [x] **P4.5** Complete all **20 princess rescues**, **six stamps**, King states,
  reward sequences, secret-tree puzzle, movement prerequisites and final victory.
- [x] **P4.6** Complete defeat, Yipe Hell, Gatekeeper fees/fights, key rewards,
  recovery and return-to-town behavior.
- [x] **P4.7** Complete statistics, equipment and special-progress views, kill
  history, high scores/codes, Silly Mode and all remaining inventory features.

P4 is verified by all seven domain reference suites and the
[complete fresh campaign and late frontend restart](evidence/2026-10-04-p234-complete/README.md).
The ending is earned from ordinary purchases, combat and movement, including six
deaths/recoveries, movement-item acquisition, generated Hell and timed puzzles.
No injected quest flags or completion saves establish this acceptance.

### P5 Presentation and controls

Implement these alongside each playable region; P5 is the final completeness
check, not a reason to postpone presentation until all rules are translated.

- [ ] **P5.1** Extend shared terrain families and architecture across all maps.
  Preserve palette, native pixel sizes, anchors, grounded entrances and readable
  paths; record any map-specific art exceptions.
- [ ] **P5.2** Complete monster, boss, NPC and character-action artwork with clear
  targeting and combat feedback. Document new assets and generation/edit prompts.
- [ ] **P5.3** Finish dialogue, inventory, equipment, spells, shops, combat,
  creation and results overlays. Test long text, focus, back/cancel and readable
  layouts at supported display sizes.
- [ ] **P5.4** Integrate sound IDs, effect/monster aliases and playback behavior;
  persist volume/sound and display/input preferences with migration handling.
- [ ] **P5.5** Verify all original actions have reachable keyboard/controller
  mappings, including diagonals and Other. Test held input, reconnect, focus loss,
  pause, saving mid-step and resuming with a physical controller.
- [ ] **P5.6** Close the P0.4 feature inventory, including explicit dispositions
  for original desktop menu conveniences, help, history and profile information.
- [ ] **P5.7** Review new regions against V0 and check effect costs on large
  encounters and dense scenes. Preserve the user's chosen visual defaults.

### P6 Desktop verification and packaging

- [ ] **P6.1** Establish durable command-driven C# completion runs. Reproduce the
  reference's Combat/Medium/1, Quickness/Easy/2, Magic/Hard/3 and Random/Medium/4
  cases after P0.2 recovers or regenerates their inputs. Compare normalized
  gameplay state and RNG independently of rendering speed.
  Combat/Medium/1 now passes with durable inputs; the other three remain open.
- [x] **P6.2** Verify full completion from earned play: all rescues, stamps, keys,
  movement items, strength passage, required Gatekeeper progression and live finale.
  Completed as P4 support; see the P2–P4 completion evidence.
- [ ] **P6.3** Verify fresh-process restarts in early/late game, generated maps,
  Hell and timed-map locations; test imports and interrupted/failed writes.
- [ ] **P6.4** Run full map/asset coverage, overlay/input checks and physical play
  sessions. Separate automated results from manual observations and unresolved
  original-runtime comparisons.
- [ ] **P6.5** Build clearly named MonoGame desktop packages; test extracted
  assets, runtime/native dependencies and startup outside the checkout. Verify
  each claimed operating system separately and document signing/install behavior.
- [ ] **P6.6** Update user documentation and this plan with the actual supported
  feature/platform matrix, remaining limitations and reproducible release evidence.

### P7 Official console integration

- [ ] **P7.1** Confirm the official platform integration route and approved
  MonoGame/runtime requirements when platform access or a porting partner exists.
- [ ] **P7.2** Integrate platform storage, input, user/lifecycle behavior and
  packaging using the approved toolchain and documentation.
- [ ] **P7.3** Test performance, readability, save reliability, suspend/resume and
  controller behavior on the intended hardware, then complete release requirements.

Desktop progress does not depend on completing P7. Desktop or emulator tests do
not establish console compatibility or release readiness.

## Source map

| Port area | Existing implementation and evidence |
| --- | --- |
| Random and character rules | [random.cpp](../src/random.cpp), [player.cpp](../src/player.cpp), [player.hpp](../include/yipe/player.hpp) |
| Items, equipment, shops and spells | [shop.cpp](../src/shop.cpp), [item_names.cpp](../src/item_names.cpp), [spell.cpp](../src/spell.cpp), [casino.cpp](../src/casino.cpp) |
| World and timers | [map.cpp](../src/map.cpp), [exploration.cpp](../src/exploration.cpp), [map catalog](../assets/maps/original-map-tables.json), [map documentation](../docs/MAPS.md) |
| Encounters and combat | [encounter.cpp](../src/encounter.cpp), [monster placements](../assets/maps/original-monster-placements.json), [monster definitions](../assets/game-data/original-monster-definitions.json), [weapon damage](../assets/game-data/original-weapon-damage.json) |
| Quests and progression | [quest.cpp](../src/quest.cpp), [progression audit](../docs/PROGRESSION_AUDIT.md) |
| Session and interface behavior | [app/main.cpp](../app/main.cpp); map loading, defeat, reward, timer and relocation orchestration are also here |
| Saving and imports | [save.cpp](../src/save.cpp), [save.hpp](../include/yipe/save.hpp), [legacy_save.cpp](../src/legacy_save.cpp), [legacy format](../docs/LEGACY_SAVE.md) |
| Audio and settings | [audio.cpp](../src/audio.cpp), [settings.cpp](../src/settings.cpp), [auxiliary_data.cpp](../src/auxiliary_data.cpp) |
| Regression reference | [core tests](../tests/core_tests.cpp), [application tests](../tests/app_shortcut_tests.cpp), [completion driver](../tests/full_game_driver.hpp), [current town oracle](Yipe.Tests/town_reference.cpp) |
| Fidelity limits | [decisions](../docs/DECISIONS.md), [unknowns](../docs/UNKNOWNS.md), [original reference cases](../docs/REFERENCE_CASES.md) |

Keep rules and serializable state in `Yipe.Core` without MonoGame dependencies.
Keep rendering, input polling and desktop services in the frontend. Reuse the
recovered catalogs, retaining IDs and ordering. Introduce abstractions only for
concrete shared behavior or an actual platform difference.

## Verification and evidence

Run from the repository root. Record commands, source revision/working-tree
state, environment, results and evidence paths when marking work verified.

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.Desktop -c Release --no-build
```

The last command launches the playable desktop adventure. Graphics checks need a real
display. The existing smoke entry point is:

```sh
dotnet run --project monogame/Yipe.Desktop -c Release --no-build -- --smoke-test /tmp/yipe-port-smoke.png
```

The supported [graphics runner](Yipe.GraphicsChecks/README.md) exercises the full
game and presentation without requiring recording:

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Add `--record` to create a six-second MP4 using FFmpeg on `PATH`. The runner
disables test-only VSync for its manual `RunOneFrame` driver; production VSync
is unchanged. See [core reproduction](Yipe.Tests/README.md) for the independent
RNG, Player, map/world and session C++ references, diagnostic traces and checked-arithmetic commands.

For a fresh C++ comparison build, after installing the README's dependencies:

```sh
cmake -S . -B build/port-reference -DCMAKE_BUILD_TYPE=Release
cmake --build build/port-reference --parallel
ctest --test-dir build/port-reference --output-on-failure
```

Do not rerun unrelated suites for every documentation or art adjustment. Select
checks appropriate to the change, then run the broader completion/restart suites
at milestones or when shared rules change. Add focused cases for real behavioral
risks, including integer overflow, signed fields, RNG call order, event ordering
and saved/transient-state distinctions.

Use `monogame/evidence/` for small durable logs and reference captures, or record
a stable artifact location and reproduction instructions for large outputs.
Temporary paths may be working outputs but must not be the only completion
evidence. A test count without its scope and runnable fixture is insufficient.

Protect both `town-preview.json` and `game.y3m` under
`<LocalApplicationData>/YipeIII-MonoGame/` (normally
`~/Library/Application Support/YipeIII-MonoGame/` on this Mac). Use isolated test
saves. Do not overwrite C++ `.y3r` or original `.yp3` files during migration.
The full codec/storage contract is in [SAVE_FORMAT.md](SAVE_FORMAT.md).

## Open decisions and known risks

| Item | Current understanding | Next action |
| --- | --- | --- |
| Missing historical replay artifacts | The docs report corrected completion runs with 108,879 matching observations, but `evidence/corrected-completion/`, `evidence/persistence-parity/` and `evidence/verification-milestone/` are absent from this checkout. This is historical documentation, not a newly verified local result. | One new default completion now passes; P0.2/P6.1 retain the missing three-fixture matrix |
| Graphics runner portability | P0.1 is complete with configurable paths, isolated saves and optional recording. Real graphics checks have passed on Apple Silicon macOS. | Keep the window focused; validate other platforms separately |
| Uncommitted baseline | The approved MonoGame code and this plan are not represented by the recorded HEAD. | Preserve the working tree; record a real checkpoint when committing is authorized |
| Original diagonal movement | P1.3 restores eight-direction rules and preview input/animation. Diagonals use destination-only collision, horizontal-facing art and one 250 ms grid step. | P5.5: physical-controller validation and full-action/overlay coverage remain |
| Slower walking and timed gameplay | The approved pace can affect the practical difficulty of timed routes. | P3.5 passed timed platform/fire routes at 250 ms; retain this regression coverage |
| Save compatibility | `.y3m` v1 and explicit preview/C++ v20/original imports are complete. Original unknown/absent fields and selected Windows-1252 decoding are documented; earlier C++ versions require upgrading in the rebuild. | Keep formats/source files distinct; extend supported variants only with reference evidence. See `SAVE_FORMAT.md`. |
| Inherited adaptations | C++ D7 restores platform support on reopen; D8 sets new-character stat cap 50 and weapon upgrade ceiling 29. Other fidelity limits remain documented. | Dispositions are recorded in FEATURE_DISPOSITIONS.md; retain original-runtime limits |
| Menu coverage | Every original menu/system row is mapped in FEATURE_DISPOSITIONS.md; convenience features remain explicit. | P5.6: implement and verify the assigned remaining desktop work |
| Current CI and release labels | The root workflow builds/packages C++/SDL; it does not validate or publish the MonoGame game. | P0.3 and P6.5: add distinct coverage and artifacts |

## Session record

Append a short entry after each meaningful work session. Keep the resume table
current even when a task remains unfinished. Record decisions near their affected
task, and do not replace a failed result with a success claim without new evidence.

| Date | Tasks and result | Verification | Resume action |
| --- | --- | --- | --- |
| 2026-10-03 | V0 already approved. P0.0 completed: created this plan, linked both READMEs and copied available graphics evidence into the repository tree. No gameplay code changed. | Reviewed source, current git state, catalogs and documentation; checked plan links and task IDs. Prior 53 core and 139 graphics results were recorded as historical preview evidence. | Start P0.1; begin P1.1 after confirming baseline checks. Missing completion artifacts can be recovered alongside core translation. |
| 2026-10-03 | Completed P0.1 and P1.1. Added `Yipe.GraphicsChecks`, its locked dependencies, `MsvcRandom`, C++ reference generator, tests and documentation. Approved presentation unchanged. | Locked restore; checked Release build; 64 checks including 624 movement cases and 8,192 RNG records; two 139-check graphics runs; video probe; save isolation; CLI help/error handling. [Evidence](evidence/2026-10-03-port-baseline/README.md). | Begin P1.2 with character defaults and creation/reroll comparisons. P0.2–P0.5 remain open and do not prevent independently verified core translation. |
| 2026-10-03 | Completed P1.2. Added the complete `Player` state and rules, C++ oracle and C# comparison runner. Inherited D8 stat-cap behavior retained. No desktop or C++ gameplay source changes. | Locked restore; checked Release rebuild with zero warnings/errors; 84 checks; 11,322 character records matched byte for byte, including complete snapshots and relevant RNG states. Earlier presentation inputs retain their fingerprints. [Evidence](evidence/2026-10-03-character-state/README.md). | Begin P1.3 with world location, shared map catalog and full movement comparisons. UI/session integration and full saves remain later tasks; P0.2–P0.5 stay open. |
| 2026-10-03 | Completed P1.3. Shared maps/events and world transitions replace duplicated town rules; diagonal keyboard/controller steps use approved artwork and timing. Retained preview boundary/save format and inherited D7 platform-support restoration. | Locked restore; checked Release rebuild, zero warnings/errors; 188 core/input/save checks; 311,728 map/world records matched C++ byte for byte; 147 graphics checks; user-save fingerprint unchanged. [Evidence](evidence/2026-10-03-world-movement/README.md). | Begin P1.4 session ownership/command dispatch and map-entry initialization from C++ application behavior. P0.2–P0.5 and later gameplay/presentation tasks remain open. |
| 2026-10-03 | Completed P1.4 session foundation and preview integration. Added entry rules, static transitions, persistent door/switch changes, immediate tile actions and exploration-item relocation. Preserved town scope and position-save format. | Locked restore; checked Release rebuild, zero warnings/errors; 226 checks; 21,511 new C++ records matched byte for byte; 153 graphics checks; user save unchanged. [Evidence](evidence/2026-10-03-game-session/README.md). | Begin P1.5 full-state save schema, validation and atomic storage from `GameSession` and C++ `GameState`. Arrival quests/encounters/rewards, transient monsters, generated maps and timers remain P2/P3; P0 support tasks stay open. |
| 2026-10-03 | Completed P1.5. Added native full-state saves, integrity/validation, permanent placement persistence, atomic storage and pause-menu Save/Load game. Version 1 fixture and separate-process restart included; original files preserved. | Locked restore; checked Release rebuild, zero warnings/errors; 367 checks retaining earlier C++ references; 161 graphics checks; user-save fingerprints unchanged. [Evidence](evidence/2026-10-03-full-state-saves/README.md). | Begin P1.6 explicit preview migration and legacy import policy from existing readers. Keep `.y3m`, preview, `.y3r` and `.yp3` distinguishable. Generated maps and complete C++ persistence comparisons remain P3.2/P1.7. |
| 2026-10-04 | Completed all remaining P1 (P1.6/P1.7) and its generated-map dependency P3.2 under one goal. Added explicit safe imports, CLI selected-save startup, C++ map/RNG and full-state persistence oracles, and separate-process conversion/reopen tests. | Locked restore; checked Release rebuild, zero warnings/errors; 478 core/input/save checks; 2,640 new generated comparisons; 24 save fixtures × four reopen/item cycles; 164 graphics checks; user saves unchanged. [Evidence](evidence/2026-10-04-p1-complete/README.md). | Begin P2.1 by recording a real initial rescue route and its equipment/combat/quest prerequisites from the C++ reference. P0 support tasks and full arrival/timer/presentation systems remain explicitly open. |

For subsequent entries include: task IDs; changes and affected files; exact
checks and outcomes; evidence/commit references; unfinished work or blocker;
and the next concrete action. Update the top checkpoint if code is committed.

### Completed P2–P4 continuous goal

Implemented the entire connected desktop game under one goal, preserving the
approved appearance and controls. Domain logic stays in Yipe.Core; Adventure
orders movement, encounters, rewards and paused interactions. The MonoGame
frontend supplies complete gameplay overlays, world terrain/object/monster/hero
art, original audio, persisted preferences and statistics. The final simplicity
review kept concrete existing boundaries, shared the duplicate graphics restart
launcher, and corrected new-adventure cancellation losing its previous overlay.

[Durable completion evidence](evidence/2026-10-04-p234-complete/README.md) records:

- Checked Release build, 502 core checks and seven independent C++/C# rule suites.
- 629 real graphics checks, including all 115 maps, 97 tile mappings, twelve
  appearances, casinos, stats, sound lifecycle, timed routes and cancellation.
- The full fresh Combat/Medium/1 completion: 43,080 observations, 66 full saves,
  twenty rescues, six stamps, all required movement items/keys, strength 50,
  all twelve spells, six retained deaths and the live ending at level 45.
- Independent fresh native replay with identical observations, commands and saves.
- Earned opening frontend with all 42 checkpoints and fresh-process King return;
  earned late frontend boss/rescue/save, separate-process fire puzzle, twentieth
  rescue and live town-entry finale. Captures were visually inspected.
- Original personal save fingerprint unchanged. Native game/test sources unchanged.

Older evidence directories record intermediate checkpoints and retain their own
bounded claims. The earlier incomplete campaign prefix is superseded by the
complete fixture; it is not the resume source. No temporary process or ignored
build output is required to recover progress. Follow the top resume table and
feature dispositions for P5/P6; official console integration remains P7.

### P5 world-water polish — 2026-10-04

User testing identified flat rivers/lakes outside Maynard. World water now reuses
the approved village texture, adds irregular banks and subdued cave shores, and
animates sparse currents/foam at six Hz. A map-wide cached layer avoids borders
between connected water tiles; water changes invalidate it. Bridge reflections
use the same pixel mask as the water. Collision, timers, RNG and saves are unchanged.

Checked Release build: zero warnings/errors. All **633 graphics checks** pass,
including all-map rendering, five world-bridge reflections and four new checks
for joined lake texture, cache invalidation, deterministic shores and wave
pause/clipping. Surface river/lake and cave captures were visually inspected;
[before/after evidence](evidence/2026-10-04-world-water/README.md) is retained.
P2–P4 completion evidence remains historical and unchanged. Continue P5 from
the remaining feature dispositions and physical-input/display review.

### P5 world-path polish — 2026-10-04

Extended the approved village paving to all world roads, with the same stone
texture, irregular earth edges, narrow width and rounded corners. The original
road tile directions now preserve continuations at map borders. Building and
cave doorsteps use the village's existing paving treatment. The atlas compiler
moved unchanged into RetroArt and is shared by both renderers; the old flat
world-road rectangles were removed.

Checked Release build: zero warnings/errors. All **633 graphics checks** pass.
Inspected every road-bearing surface map; the village and its native terrain
captures remain byte-identical. Core/input source fingerprints are unchanged.
[Before/after evidence](evidence/2026-10-04-world-paths/README.md) is retained.
Continue remaining P5 presentation/input work from the existing dispositions.

### P5 area/world map addition — 2026-10-04

Implemented the user's accepted recommendation: a full-screen area map first,
showing the complete current region using its existing retro artwork. M or
controller View opens/closes; Actions also has Map. Enter/South switches to the
world grid; Left/Right cycles visited layers; Escape/East closes. The gold player
marker indicates facing, and discovered services, entrances, stairs and bridges
have a legend. Surface, upper/deep caves and alternate regions retain their
original grid positions. Known travel links include inter-layer destinations;
unvisited regions and unrevealed cells remain shaded and secrets unmarked.

Map viewing pauses movement, encounters, hazards and scene animation, clears
buffered movement, and does not draw RNG. Menus/text/combat keep their existing
interaction boundaries. Static discoveries persist across visits and saves;
generated caves clear old layouts on regeneration. A hash-bound `.map.json`
companion preserves version 1 `.y3m` gameplay bytes. Older saves load normally;
missing/corrupt/mismatched notes cannot block a valid adventure. Copy both files
to preserve history. New adventures reset the journal.

Verification: checked Release, zero warnings/errors; **529 core/input/storage
checks**, **648 graphics checks**, inspected village/cave/generated area maps and
surface/cave/alternate world views. The earned opening passes all 42 original
checkpoints and King return, with **14 visited regions and 17 links** restored
in a fresh process. Gameplay core and personal saves remain unchanged.
[Evidence and rerun commands](evidence/2026-10-04-map-feature/README.md) are retained.
The map feature is complete; continue the existing P5/P6 checklist, including
physical controller verification and remaining desktop conveniences.

### P5 desert corner rendering fix — 2026-10-04

Corrected the orientations of desert transition tiles `0x1b`–`0x1e` against
original bitmap 138 and the C++ tile mapping. The old mask turned three corners
the wrong way and offset their coverage, producing isolated sand/grass wedges.
All four corners now face the shared center of their authored patch and meet
without gaps. This changes only `WorldTerrain.DrawSandEdge`; map data, collision,
textures and gameplay remain unchanged. Both play and the area map use the fix.

Checked Release: zero warnings/errors; **648 graphics checks** pass. All 115
maps render; exactly the **16** maps containing desert corner tiles changed,
while **189 other captures** remain byte-identical to the map-feature run.
Reviewed examples include long boundaries, islands, concave turns and map edges.
[Before/after evidence](evidence/2026-10-04-desert-corners/README.md) and personal-save
preservation hashes are retained. Continue the remaining P5/P6 checklist.

### P5 desert biome polish — 2026-10-04

Implemented the requested natural desert treatment. A cached sand layer combines
all authored desert tiles before smoothing and perturbing their boundary, with
rounded turns, irregular contours and a dithered dry-grass fringe. This retains
the corrected original corner directions while removing geometric-looking edges.
The area map shares the new terrain. No source bitmap or gameplay tile changes.

Added quiet dust puffs and trailing motes, and up to two small rolling/bouncing
tumbleweeds following clear sand lanes with pauses between gusts. Effect masks
exclude water, vegetation, buildings and fixed props. Palms and dry bushes sway
by one pixel with anchored roots. All animation follows scene time and pauses
with menus; it draws no gameplay RNG. Wind is rendered beneath actors and omitted
from the map overlay for readability. There is no permanent particle simulation.

Verification: checked Release, zero warnings/errors; **657 graphics checks**,
including nine new checks for weather freeze/movement, masking, tumbleweed travel,
full-state/RNG isolation, static terrain stability, biome scope, same-map cache
invalidation and anchored vegetation. Inspected before/after desert renders and
sampled the retained eight-second wind animation. Gameplay core and personal
saves/profile remain unchanged. [Evidence](evidence/2026-10-04-desert-biome/README.md)
includes screenshots, animation, logs and source fingerprints. Continue the
remaining P5/P6 checklist, including hardware performance and physical input review.

### P5 mountain border artwork correction — 2026-10-04

Replaced the erroneous purple arrows on tiles `0x65`–`0x68` with walkable rocky
mountain feet blending into sand. Original bitmap 460 and its native tile mapping
establish the rock orientations as right, bottom, left, top. The port had
misidentified these tiles; earlier coverage checks verified drawing but not the
artwork's meaning. The shared ground renderer now uses the existing mountain
texture, irregular shallow borders, earth shading and loose stones in both play
and the area map. All 35 occurrences across maps 1, 6, 7, 12, 13 and 21 are fixed.

Checked Release: zero warnings/errors; **657 graphics checks** pass. Visually
inspected every affected map; exactly those six captures changed while **199**
other captures remained byte-identical. One production source file changed;
gameplay, assets and map data are unchanged. [Evidence and original artwork](evidence/2026-10-04-mountain-borders/README.md)
are retained. Continue the remaining P5/P6 checklist.

### P5 desert and Hell heat shimmer — 2026-10-04

Added continuously moving heat refraction using the existing scene clock. Desert
intensity follows the cached sand footprint and fades into nearby terrain;
all Hell regions (117–120) receive stronger shimmer. A smooth calm area preserves
the hero, screen-edge falloff avoids smearing, and the UI is drawn afterward.
Pause and overlays freeze the animation. Both miniature Off and the existing
Subtle/Strong modes compose correctly. No gameplay, RNG, save or art asset change.

Checked Release: zero warnings/errors; **685 graphics checks** pass, with 28 new
checks for actual GPU output, biome scope, smooth/frozen motion, hero/menu
preservation, complete viewport coverage and full gameplay snapshot isolation.
All 115 raw map renders are unchanged; 204 of 205 comparable existing captures
remain identical (the generated-Hell frontend capture now shows heat). Personal
saves/profile and all gameplay core sources are unchanged. [Retained evidence](evidence/2026-10-04-heat-shimmer/README.md)
includes desert/Hell animations, screenshots, logs and fingerprints. Continue the
remaining P5/P6 checklist, including physical input and platform performance.

### Completed goal: complete Hell biome overhaul — 2026-10-04

Implement the six accepted recommendations together for maps 117–120. Preserve
original traversal, fire hazards, doors, encounters, discoveries and saves.
Use the approved retro palette, miniature presentation and heat shimmer.

- [x] Charred basalt formations, broken pillars and scorched ground with readable paths.
- [x] Animated molten fissures/pockets within existing blocked terrain, with cooling crust and bubbles.
- [x] Restrained orange firelight on nearby floor/rocks, with deep plum/charcoal shadows.
- [x] Sparse rising embers, drifting ash and wisps of smoke.
- [x] Gatekeeper landmarks: ruined entrance, heavy ironwork, chains and scorched paving.
- [x] Varied flame sizes and independently phased animation.
- [x] Verify all four Hell maps, pause/map behavior, visibility, gameplay isolation,
      generated layouts and compatibility with heat/miniature; retain captures,
      animation and results here, then mark the goal complete.

Implemented in `WorldHell.cs` using the existing map, visibility, sprite batch
and scene clock, with the new `World/hell.png` production atlas. Final checked
Release build has zero warnings/errors; **743 graphics checks** pass. All four
Hell regions and nine generated layouts were inspected, including the Gatekeeper
entrance, paused animation, map presentation and heat-shimmer composition.
Every molten pixel remains within an authored fire-wall cell, and all such cells
retain visible heat/flames matching their original burn behavior. No gameplay
or personal save changes. [Durable evidence and animation](evidence/2026-10-04-hell-biome/README.md)
record the completed goal. Continue the existing remaining P5/P6 checklist.

### P5 optional vignette — 2026-10-05

Added **Vignette: On/Off** below Miniature effect in the pause menu. It defaults
to Off, including for existing profiles, and uses the normal preference save
and failure reporting. The nine menu rows, two-line status and controller hints
fit the existing panel without reducing the font size.

A cached elliptical alpha mask darkens the edges gradually to at most 42% at
the corners, with a clear center. Only the mask uses smooth filtering; world
pixels retain their existing sampling. It draws after world/heat presentation
and before the UI, including when miniature is Off. Menus, messages and maps
keep their original colors. No extra render target, shader, per-frame allocation
or gameplay change is needed. The previous Hell sharpness treatment remains.

Checked Release build: zero warnings/errors; **530 core/input/storage checks**
and **1,012 graphics checks** passed. Coverage includes old-profile compatibility,
persisted toggle, all miniature/scanline combinations, central pixel preservation,
edge darkening, UI/map isolation, and heat with miniature on/off. All 115 full-map
renders match the prior build. Only four existing pause-panel captures changed.
The live adventure/journal were saved during work; no task commands wrote them.
The personal profile was unchanged. The older game exited, and the latest build
was reopened for testing.

Evidence: [off](evidence/2026-10-05-vignette/off.png),
[on](evidence/2026-10-05-vignette/on.png),
[menu with diagnostic two-line storage status](evidence/2026-10-05-vignette/menu.png),
[Hell](evidence/2026-10-05-vignette/hell.png),
[desert](evidence/2026-10-05-vignette/desert.png),
[graphics log](evidence/2026-10-05-vignette/graphics.log),
[core log](evidence/2026-10-05-vignette/core.log),
and [source hashes](evidence/2026-10-05-vignette/source.sha256).
Full run: `build/graphics-checks/yipe-graphics-20261005-062603-c45641c4`.
Continue P5.6; no unfinished vignette implementation remains.

### P5 localized pixel lighting — 2026-10-05

Implemented the first of the six accepted visual features. **Pixel lighting**
defaults to On and persists through the existing preference controls. The pause
panel now fits ten rows at the same font size, with separate space for two-line
status text and keyboard/controller hints.

Forge and magic-shop windows, sparse wall-mounted cave torches and existing fire
sources light nearby scenery and actors. Hell uses the existing flame positions,
including braziers, so the lighting stays attached to its art. Four native-pixel
shade bands and restrained six-Hz flicker preserve the retro appearance. The
effect does not darken ambient terrain or add blurred bloom. Overlapping sources
use maximum intensity rather than accumulating brightness. Walls and closed doors
occlude light; opening/changing terrain rebuilds cached light patches. Hidden
sources and unexplored cells remain unlit, and secret walls use the same visual
rules as their ordinary counterparts. Decorative torches add no gameplay hazards.

`PixelLighting` shares one native light target and a small mask atlas between all
biomes. It builds source/occlusion geometry only when the map instance or tiles
change, then composites over existing world pixels before miniature/heat effects
and the UI. Pause freezes its scene clock. Lighting Off retains the prior world
appearance; the map remains unchanged at either setting.

Checked Release: zero warnings/errors, **530 core/input/storage checks** and
**1,060 graphics checks** pass. The new coverage includes seven real scenes,
combined effects, deterministic flicker, state/RNG preservation, closed/open
doors, changing sources, secret variants, fog, capped overlap, exact black
outlines, profile reload and untouched map/UI colors. All 115 raw map captures
and existing gameplay captures with the new effect disabled remain identical;
only five previous pause-panel captures changed. Personal save/journal/profile
hashes remained identical. Simplicity review kept source discovery, caching,
occlusion and native composition together in one renderer; no new dependencies,
gameplay state, save version, custom shader or per-frame pixel uploads.

Evidence: [town before](evidence/2026-10-05-pixel-lighting/town-off.png) /
[after](evidence/2026-10-05-pixel-lighting/town-on.png),
[cave before](evidence/2026-10-05-pixel-lighting/cave-off.png) /
[after](evidence/2026-10-05-pixel-lighting/cave-on.png),
[Hell before](evidence/2026-10-05-pixel-lighting/hell-off.png) /
[after](evidence/2026-10-05-pixel-lighting/hell-on.png),
[menu](evidence/2026-10-05-pixel-lighting/menu.png),
[graphics log](evidence/2026-10-05-pixel-lighting/graphics.log),
[core log](evidence/2026-10-05-pixel-lighting/core.log),
[source hashes](evidence/2026-10-05-pixel-lighting/source.sha256), and
[preserved saves](evidence/2026-10-05-pixel-lighting/preserved-saves.sha256).
Full run: `build/graphics-checks/yipe-graphics-20261005-064202-3a796283`.
The other five visual features remain queued; none is partially implemented.
