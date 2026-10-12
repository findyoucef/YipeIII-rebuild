# Yipe III MonoGame port plan and progress

Port the complete supplied Yipe! III game to C# and MonoGame, preserving its
gameplay and content while applying the visual style, controls, and presentation
already approved by the user. This document is the working checklist and handoff
record for the port. Update it during implementation so a later session can
resume from repository evidence without needing the conversation history.

Last updated: **2026-10-12**. **P1, P2, P3 and P4 are complete.** The continuous
P2–P4 goal delivered the connected opening, complete exploration and earned
campaign ending with the approved retro presentation. The [completion evidence](evidence/2026-10-04-p234-complete/README.md)
contains runnable inputs, full saves, independent comparisons, screenshots and
fresh-process frontend restart results. P5/P6 release work and P7 official console
integration remain separate; this is a playable desktop port, not a release claim.

## Resume here

| Field | Current state |
| --- | --- |
| Local gameplay launch | No game process remains after verification. The latest extracted app is `build/monogame-packages/preview.8/Yipe III MonoGame.app`; local builds remain the default for iteration. The previous Ice Cave preview is isolated from personal adventures. |
| Current milestone | Automating MonoGame macOS packaging and tagged prereleases in GitHub Actions, targeting the first hosted preview.9. P1–P4 and local release verification are complete. |
| Next task | Verify the hosted branch build, push `monogame-v0.1.0-preview.9`, then download and verify its release assets. Physical-controller and additional historical campaign-route coverage remain unverified. |
| Last completed work | Pixel-art knight/castle app icon: embedded local-build BMP plus macOS ICNS, fresh build/package/startup checks. [Icon review](evidence/2026-10-12-app-icon/review.html) and [verification](evidence/2026-10-12-app-icon/README.md). Prior [release report](evidence/2026-10-12-release-readiness/README.md) retains full gameplay/performance evidence. |
| Latest verification | Checked Release: zero warnings/errors. Seven native reference traces match. 43,080 campaign state/RNG observations and 66 complete saves match C++. Nine scenes at 720p/1080p average 60.01–60.16 FPS on M5 Max. Packaged normal startup, native dependencies, signatures and persistence pass. |
| Earned frontend evidence | Fresh opening matches all 42 C++ checkpoints plus King return and separate-process continuation. Fresh late frontend defeats the empowered Gatekeeper, saves/restarts, rescues 20 and displays the live Maynard finale. |
| Approved presentation | The user's existing pixel-art, miniature, lighting, clouds, weather and sound defaults remain unchanged. Current clouds, music, river junctions and precise Ice Cave entrance movement are user-approved. |
| Work left running or half implemented | Release workflow implementation and first hosted verification are in progress. Include the user's requested move of the cave-remaster skill into `.agents/skills`; the global copy has been removed. |
| Blocking dependencies | No controller available; user explicitly approved recording physical testing as unverified. Other-platform/other-Mac and official console checks need their environments. CI workflow is linted; check GitHub Actions for the hosted result of this checkpoint. |
| Save preservation | All 33 personal files stayed byte-identical throughout this pass. Every test used isolated saves/profiles. No save-format changes. |
| Repository checkpoint | This checkpoint includes approved NPC animation, title/pause menus, music/credits, terrain and combat/ice fixes, release verification and the app icon. All 156 source hashes match preview.8 before committing; only handoff documentation changed afterward. Existing baseline and remaster commits remain intact. |

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

## MonoGame GitHub release automation — 2026-10-12

Goal: produce verified downloadable MonoGame macOS artifacts in GitHub Actions
and publish the first prerelease through the same automation.

1. Extend the existing MonoGame regression workflow with the established
   self-contained packager, archive checksum, extracted-app verifier and uploads.
2. Run packaged smoke startup and persistence checks on the hosted Apple Silicon
   runner. Verify sustained normal startup and display changes on the downloaded
   app locally: the hosted VM runs at 6.8–6.9 FPS or crashes natively in that test.
   Keep full local visual/performance and physical hardware coverage separate.
3. Use `monogame-v*` release tags, deriving the package version from the tag.
   Publish only after verification; retain the existing legacy C++ tag workflow.
4. Document download/install/signing limits, release notes and tag commands.
   Validate workflow syntax and version selection, then push and observe a branch run.
5. Tag the passing revision, verify hosted publication, download the archive and
   checksum, and retain a local verification of the GitHub-built app.

Status: hosted validation in progress. CI exposed a real concurrent import race
in .NET's Unix non-overwriting move; the save store now publishes imports with
an atomic hard link. A strengthened test reproduced 12 successful competitors
before the fix. Save format and normal saves are unchanged. The repository-local
cave skill move is included in this checkpoint.


## Completed local release-readiness pass — 2026-10-12

Finish the retained UI/save lifecycle, run fresh regression and performance checks,
verify physical input when hardware is available, and produce an updated
self-contained macOS Apple Silicon preview. The user approved the Ice Cave fix.

1. Audit title, adventure switching, independent new saves, cancel/back, and
   quit/relaunch; add focused regressions only for missing risk coverage.
2. Verify atomic save failure/interruption, imports, generated/Hell/timed-room
   fresh-process restoration and deterministic continuation.
3. Run checked Release builds, all core/reference and graphical checks, earned
   opening/finale restarts, and the normal startup loop. Preserve personal saves.
4. Measure default-effect dense outdoor, cave and battle scenes; fix demonstrated
   performance problems and retain timings, hardware details and captures.
5. Physical controller: **unverified**. User confirmed no controller available
   and approved documenting the gap. Synthetic checks pass separately; the
   release report retains the hands-on checklist.
6. Refresh package instructions/notices, build preview.7, verify the extracted
   app and packaged persistence outside the checkout, and retain release evidence.
7. P5/P6 reconciled below. All automatic/local work passes; preview.7 and the
   retained report are ready. Physical hardware, other platforms and the three
   historical variant input streams remain explicitly unclaimed.

## Ice Cave entrance traction repair — 2026-10-12

The user reported sliding past doors and section entrances. An isolated
reproduction on **upper A1 / door 4,8** confirmed that releasing an eastward step
at **4,7** forced a second step to **5,7**, preventing alignment with the door.

- Ice within one tile of walls, doors, other floor materials, depth travel points
  and map edges now has ordinary traction. Open ice retains its existing 340 ms
  push, 220 ms glide and 420 ms one-tile braking animation.
- The terrain neighborhood also recognizes an opened doorway after its tile
  becomes ordinary ice. Visible/secret wall variants and floor aliases keep
  matching traction, and the original collision/key/transition logic is unchanged.
- Verified actual doors in upper A1 (4,8), upper A2 (2,5) and deep B2 (10,6),
  upper A3's one-tile passage (2,5), the A2/A1 room transition and upper/deep B1
  stairs. Release alignment, immediate held turns, opened-door returns and
  missing-key rejection all pass alongside the existing open-ice/encounter gates.
- Local Release build: zero warnings/errors. **274 Ice Cave checks** and **641
  core/input checks** passed. The doorway regression failed as expected against
  the previous code. All 33 personal files stayed byte-identical.
- [Review](evidence/2026-10-12-ice-entrances/review.html) retains the original
  overshoot, repaired alignment, entrance screenshots, and two silent 60 Hz clips
  showing door entry and preserved open-floor sliding. See `review.json` for
  source hashes and exact runs.
- Scoped simplify review kept the production edit in the existing movement
  selector plus Current controls text; no new state, setting, art or map data.
  `git diff --check` passed. No repackaging, commit, live input or relaunch.

## Surface river junction repair — 2026-10-12

The user identified square brown mounds beside **surface D2 / 7,3**. The pair
at **5,2 and 8,2** are tile **9**, whose original bitmap 143 lower-right cell
depicts a west/east/south river junction. The remaster had incorrectly treated
it as cave-wall terrain and omitted it from the connected water mask.

- Restored all **nine junctions across seven regions**: C2, D2, E2, C3, E5,
  C6 and D6. Reused the existing water texture, rounded shoreline treatment,
  animated ripples/foam and positional river ambience.
- Removed the wall/rock classification from terrain, sand blending, footsteps,
  overview map colors and lighting. Original tiles, collision and saves are
  unchanged; no new renderer subsystem or asset is needed.
- Local Release build passed with zero warnings/errors. All **369 focused
  overworld checks** passed: connected branches through straight/bent neighbors,
  dry northern banks, exposed ground at corners, animation, sound/cache updates,
  overview colors, light transmission and existing surface regression coverage.
- [Review captures](evidence/2026-10-12-river-junctions/review.html) include every
  affected region and D2 day/night gameplay from **7,3**, rendered with isolated
  fixtures. Original before captures are retained from the October 12, 07:43 UTC
  overworld run; provenance and final source hashes are in `review.json`.
- Scoped simplify review found no unnecessary abstraction. `git diff --check`
  passed. No repackaging, commit, live gameplay input or game relaunch.

## Buffered combat animation and defeat cues — 2026-10-12

After approving the rapid-attack sound fix, the user reported that early confirms
also removed the animation and skipped monster-death audio. They chose a brief
readable hit/defeat beat with confirmation buffered.

- An early Continue/Back now remembers the request and plays the remaining
  presentation at **3x** speed. It closes once, after the timeline finishes;
  additional presses do not restart animation or queue attacks. A final kill
  from the start completes in about **0.36 seconds**; a three-enemy response
  takes at most about **0.86 seconds**. Normal and Auto pacing are unchanged
  until the player asks to continue early.
- The existing timeline dispatches impact, individual monster deaths and rewards
  once each. Sound playback/pitch is unchanged. The prior four-voice overlap
  remains; death tails continue after the battle panel closes.
- Pause freezes the buffered animation and resumes it with the request intact.
  The result panel shows “Continuing...” while completing. This replaces the
  immediate visual dismissal described in the earlier audio-only entry below.
- Clean Release build; **890** monster/combat/footstep checks and **163**
  music/menu/control checks passed. New checks include 24/60 FPS rapid victories,
  three-enemy defeats, no repeated death cues or rewards, and preserved gameplay
  and RNG. The opening frontend matched all **42** C++ checkpoints plus King
  return, **116 victories**, save/reload and fresh-process continuation.
- All **33** personal files were unchanged. No repackaging or save-format changes.
  [Silent normal/quick video comparison and verification](evidence/2026-10-12-combat-animation/review.html).

## Rapid combat sound effects — 2026-10-12

The user confirmed battle/cave music works, but rapid Enter presses could dismiss
an impact before its 180 ms cue and each following sound cut off the previous one.

- Skipping a result before impact now plays that pending action sound once and
  returns immediately to commands. Normal animation timing and misses are retained.
- Combat impact, counterattack, death and reward effects can overlap across four
  voices. A saturated mix retires its oldest voice; it never queues delayed hits.
  Noncombat effects retain their original replacement behavior.
- Every active voice follows effects/master volume and is released on mute,
  focus loss, natural completion or disposal. Footstep suppression and ambient
  ducking recognize all active effects.
- Verification: clean Release build, **881** focused monster/combat/footstep checks
  and **163** music/menu/control checks passed. Rapid-confirm tests compare full
  saved state and RNG with independent core combat results. All **33** personal
  files were unchanged. No packaging or save-format changes.
- [Evidence and commands](evidence/2026-10-12-combat-audio/review.json). The initial
  test fixture intended to miss could still hit; reusing the existing deterministic
  miss seed corrected that test. The final run passed in full.

## Normal macOS startup — 2026-10-12

The archived failed launch was blocked in `Cocoa_GL_SwapWindow` /
`SDL_CondWait_REAL` throughout the main-thread sample, with no display-link
thread present. SDL 2.32.10 waits indefinitely for refresh notifications when
swap interval is nonzero. A current pre-fix launch succeeded, so this is an
intermittent failure rather than an always-reproducible one.

- [x] Disable OpenGL VSync in the macOS production graphics configuration;
  retain the fixed 60 Hz game timer, which also bounds frame presentation.
  Other platforms retain their existing VSync setting. No new dependency,
  profile preference, watchdog or launcher is added to production.
- [x] Add `Yipe.GraphicsChecks --startup-check DIRECTORY`: normal `Game.Run`,
  no smoke mode or graphics overrides, isolated save/profile, real ident/title
  progression, frame-rate/animation check, resize/fullscreen/return and clean
  exit. A test-only 45-second timeout catches a blocked native main thread.
- [x] Two separate processes passed all nine checks each at 60.8 and 60.9 FPS.
  Release build has zero warnings/errors. The direct regular executable also
  launched from an empty working directory with no arguments and reached title
  and gameplay. All 33 personal files were unchanged at verification.
- [x] Switch the local launch record back to the regular executable. No package
  rebuild, commit or push. The old scratch launcher remains historical output.

A follow-up title-menu input check overlapped with live gameplay. Down, Enter and
Escape test inputs reached the adventure; input automation was stopped and the
user was informed. Do not use those live captures as isolated menu-test evidence.
The normal user session remains open and should not be controlled or restarted
while completing unrelated work.

[Captures and results](evidence/2026-10-12-normal-startup/review.html),
[verification record](evidence/2026-10-12-normal-startup/review.json).
Only the local macOS backend was tested. This removes the game's dependency on
the stalled wait; it does not repair SDL's display-link implementation.

## Narrow snow margin - 2026-10-12

The user rejected the broad gravel band and requested snow closer to the mountains.

- [x] Reduce the setback and powder width while retaining the broad curved outline.
- [x] Draw mountain gravel below the snowfield and cliff faces above it, removing square cutoffs at narrow margins.
- [x] Preserve a solid visible rock core in blocked mountain cells.
- [x] Pass 45 snow and 338 overworld checks; only maps 13 and 21 change, with all 28 personal files unchanged.
- [x] Rebuild the local launcher and retain [before/after captures](evidence/2026-10-12-snow-narrow/review.html).
- [x] User approved the narrower margin: “ok this is better.”

## Smooth snowfield outline - 2026-10-12

The user liked the exposed margin better but found the snowfield shape unnatural.

- [x] Replace the detailed mountain-distance contour with a smoothly averaged open-ground field, reusing existing scratch buffers.
- [x] Round the square notches into broad curves and reduce short, wavy changes in margin width.
- [x] Keep native pixel edges, exposed gravel, cold weather and material-aware footsteps intact.
- [x] Pass the existing 45 snow and 338 overworld checks; only maps 13 and 21 change from the prior pass.
- [x] Rebuild locally and retain [day/night and map comparisons](evidence/2026-10-12-snow-smooth/review.html); all 28 personal files unchanged.
- [x] User rejected the wide margin; revised above.

## Snow setback from mountain feet - 2026-10-12

The user rejected snow continuing all the way to the mountains and requested
exposed ground between the snowy center and rock faces.

- [x] Keep the location at A3/map 13, Ice/Zephyr entrance at 5,4; inspect the actual scene.
- [x] Replace powder banks with a variable-width slate/gravel margin and scattered powder along its inner edge.
- [x] Preserve snowfall throughout the cold basin; keep snow prints/crunch on actual snow and stone footsteps on gravel.
- [x] Retain approved peaks, terrain/collision, cave access and secret-snow equivalence.
- [x] Pass 45 snow and 338 overworld checks; compare all 36 surface captures and rebuild the local launcher.
- [x] Retain [day/night and map comparisons](evidence/2026-10-12-snow-setback/review.html); all 28 personal files unchanged.
- [x] User reviewed: better, but requested a smoother shape. Revised above.

## Snow ground and mountain transitions - 2026-10-12

The user requested further revision after this pass. The snow-setback work above
supersedes its powder banks; coverage and snowy-pine fringe fixes remain.

- [x] Resolve the reported exterior to surface A3/map 13, Ice/Zephyr entrance at 5,4, and inspect matching gameplay.
- [x] Preserve snow coverage at rock-enclosed corners and remove the erroneous grass fringe beside snowy pines.
- [x] Add blue-gray contact shading from the actual rock outline, uneven powder banks and clustered pixel melting edges.
- [x] Keep approved mountain sprites, terrain/collision, cave approaches, snowfall and secret-snow behavior intact.
- [x] Pass 44 snow and 338 overworld checks; rebuild the local launcher with zero warnings/errors.
- [x] Retain [day/night and map comparisons](evidence/2026-10-12-snow-ground/review.html); all 28 personal files unchanged.
- [x] User reviewed and requested the snow stop short of the mountain feet.

## Maynard mountain backdrops - 2026-10-12

- [x] Resolve the castle (11,1) and cave (1,1) to Maynard, surface E3/map 17.
- [x] Capture both approaches before changing the art, using isolated gameplay fixtures.
- [x] Enlarge the existing northern peak sprites and extend their rock beyond the top of the view.
- [x] Preserve every original terrain/collision cell, river and entrance; other surface maps remain byte-identical.
- [x] Pass 338 overworld and 38 Maynard checks; rebuild the local launcher with zero warnings/errors.
- [x] Retain [day/night before-and-after captures](evidence/2026-10-12-maynard-mountains/review.html).
- [ ] User visual review.

## Desert grass fringe correction - 2026-10-12

- [x] Resolve the reported casino to surface F1, map 6, local 9,6, and inspect matching gameplay.
- [x] Exclude rock from the sand blend's ground samples so mountain borders no longer expose meadow pixels.
- [x] Preserve actual grass-sand transitions, authored terrain and approved mountain artwork.
- [x] Pass 337 focused overworld checks and rebuild the local launcher with zero warnings/errors.
- [x] Retain [before/after gameplay and maps](evidence/2026-10-12-desert-edges/review.html); all 28 personal files unchanged.
- [ ] User visual review.

## Dedicated winter mountain sprites - 2026-10-11

The user approved the bare mountain treatment but rejected the basin's applied
snow effect. The renderer now selects complete authored winter sprites.

- [x] Generate eight lightly dusted and eight deep-snow variants, with snow
  cornices, shaded folds and exposed stone built into their pixel art.
- [x] Reuse the native importer and connected-body cleanup; retain the bare
  atlas and its render behavior away from snow terrain.
- [x] Choose complete variants by snow-ground distance, including offscreen
  anchors; remove runtime snow recoloring and its unused height buffer.
- [x] Verify 335 overworld and 41 snow checks, matching A2/A3 spine and all
  40,960 mountain overlap pixels, native colors, silhouettes, paths and saves.
- [x] Compare all 36 surface captures: changes only in 7,13,18,19,20,21,26,27
  near the existing A3 and C4 snow terrain; 28 captures byte-identical.
- [x] Rebuild the local launcher and retain before/after gameplay and map captures.
- [x] User approved the winter sprites: “yep this looks much better”.

[Review gallery and generation prompt](evidence/2026-10-11-snow-mountain-sprites/review.html).

## Overworld mountain forms - 2026-10-11

- [x] Replace the repeated small-rock fill with larger connected ridge planes.
- [x] Vary front-face depth and use angled corners, sparse lit crests and a
  short local slate ramp; retain crisp native pixels and binary edges.
- [x] Use the original rock atlas only for occasional weathering. Keep cave
  rims, grass/sand foothills and existing travel openings intact.
- [x] Shape snow caps around the broader slopes, retaining the basin falloff
  and matching A2/A3 overlap.
- [x] Verify every surface mountain cell, 40,960 matching overlap pixels,
  fog, secret aliases, cache changes and unchanged gameplay/RNG.
- [x] 334 focused overworld checks and 641 core checks; local build only.
- [x] 4,119 full graphics checks and 360 tour frames; 25 mountain maps changed,
  all 90 other map captures byte-identical.
- [x] Revise the art direction after user feedback: the slab comparisons were
  rejected, then the user approved the overlapping-peaks mockup.
- [x] Create an art-direction mockup on surface F3 with the built-in image tool.
  [Current capture and proposed peaks](evidence/2026-10-11-mountain-direction/review.html).
  This is conceptual artwork, not a tested game render; no production change in
  this concept pass. The user approved the concept ("beautiful, I love it").
- [x] Integrate eight authored peak variants, verify region joins and snow
  transitions, and capture actual gameplay. 335 overworld and 39 snow checks pass.
  The local eleven-color ramp and binary alpha keep the new art on the native
  pixel grid. Connected-body cleanup removes stray atlas-row pixels; snowy
  cut-back corners expose snow rather than meadow. 28 personal files unchanged.
- [x] User approved the bare mountain implementation; the requested dedicated
  winter sprites were subsequently implemented and approved as recorded above.
  [Gameplay, before/after and concept comparison](evidence/2026-10-11-mountain-peaks/review.html).
  No repackaging or new commit/push in this pass.

[Five-region before/after gallery](evidence/2026-10-11-overworld-mountains/review.html).

## Music, Sound and contributor Credits - 2026-10-11

- [x] Woodland Fantasy by Matthew Pablo (CC BY 3.0) for title/towns;
  Celtic Impulse by Kevin MacLeod (CC BY 4.0) for outdoor exploration.
- [x] Streamed, repeating Ogg recordings; matched loudness and crossfaded loop
  joins; fade transitions, quieter menus and focus/mute pause/resume.
- [x] Options > Sound: master gate/volume, effects volume, music gate/volume.
  Left/right adjusts levels in place; Actions shares the same page. Back restores
  its selected entry, levels clamp at 0-100%, and Enter no longer cycles volume.
  Preferences are independent of adventure saves.
  Effects includes footsteps, action sounds and environmental ambience.
- [x] Options > Help & credits > Credits: Game & remaster, Music, Sound &
  ambience, Technology. Retain authors, source links, adaptations and licenses.
  Bundle upstream runtime license texts and third-party notices.
- [x] 641 core checks; 129 focused menu/music checks; 38 Maynard ambience
  checks. Zero build warnings/errors; 33 personal files preserved.
- [x] User listening review of the complete five-track selection is approved; subsequent rapid-combat sound fixes were also approved.
- [x] **Battle/cave music goal, completed 2026-10-12.** All six planned steps:
  1. Added Battle Theme A by cynicmusic (CC0), Cave Theme by Brandon Morris /
     HaelDB (CC0), and Crystal Caves v1.2 by FoxSynergy (CC BY 3.0).
     Source URLs, original/adapted hashes, exact filters, licenses and credits ship
     with the audio. All three measure approximately -20 LUFS without clipping.
  2. Cave Theme covers Earth, Stone, Fire, Breeding Grounds, Final Cave and Hell;
     Crystal Caves covers Ice/Zephyr and Water. Cave score intensity is 70%.
  3. Combat enters within 0.4 seconds at full mix; rewards lower the score.
     Exploration resumes from its stream bookmark; same-score regions stay
     continuous. New battles start their cue afresh.
  4. Existing Sound controls, focus/mute behavior and independent ambience/effects
     gain remain verified. All 28 personal files are unchanged.
  5. 163 focused menu/music/control checks pass, including all eight underground
     families, generated Hell, actual repeat boundaries, cancel/mute transitions,
     title/defeat behavior, credits and complete adventure/RNG preservation.
     Main and local-launcher Release builds have zero warnings/errors.
  6. [Listening previews](evidence/2026-10-12-battle-cave-music/review.html),
     [verification](evidence/2026-10-12-battle-cave-music/review.json), build/check
     logs and credits screenshots are retained. Local launcher content matches;
     no repackaging, commit or push in this task. No game is left running.

DesktopGL reports a buffered music position, so bookmark restoration is not
sample-accurate. The new clips are music-only previews, not recorded gameplay.
Musical preference and final mix remain open for the user's listening review;
other audio backends remain separate release-verification work. The macOS startup
issue was subsequently addressed as recorded above.

[Screenshots](evidence/2026-10-11-music/review.html),
[verification](evidence/2026-10-11-music/review.json),
[music provenance](Yipe.Desktop/Content/Audio/Music/sources.json).

## NPC animation and personality — 2026-10-11

The accumulated remaster work was committed as `0af5031` and pushed to
`origin/main` before the NPC goal began. The NPC pass is now implemented and
verified locally, with user visual review pending.

- [x] Five native pixel-art sheets: King, Skill Teacher and three Princess
  appearances, 60 poses with stable feet and palette/binary-alpha import.
- [x] Restrained world idles and greetings, grounded shadows and cave lighting.
- [x] Dialogue portrait headers, teaching approval, Princess rescue gratitude
  and King gold/item/equipment reactions. No delay before choices.
- [x] Original dialogue, event positions, rewards, training costs and RNG preserved.
- [x] All 20 rescue locations, four teachers, fog, pagination, pause/focus,
  immediate choices, cancellation and failed/repeated training verified.
- [x] 632 core/input, 183 focused and 4,036 full graphics checks,
  plus 360 tour frames. Local Release: zero warnings/errors; no repackaging.
- [x] Before/after captures and a 55-second animation review video retained.

[Plan](NPC_ANIMATION_PLAN.md), [review gallery](evidence/2026-10-11-npc-animation/review.html),
[art provenance](Yipe.Desktop/Content/Art/Retro/World/NPC_ANIMATIONS.md),
and [verification](evidence/2026-10-11-npc-animation/review.json).

## Final Cave remaster — 2026-10-11

Authorized scope: fifteen rooms, upper C1/B2/C2/B3/C3/C4
(43/48/49/54/55/61) and deep C1/D1/E1/F1/C2/E2/F2/E3/F3
(83/84/85/86/89/91/92/97/98). The cave skill's approved contour and
material-specific floor techniques guide an ancient endgame cavern, distinct
from the volcanic Fire Cave.

- [x] Connected dark rock with chipped contours, small facets, stratification
  and shaded front faces, anchored across matching room overlaps.
- [x] Broad worn bedrock slabs, selective shallow fissures, abrasion and sparse
  wall-foot chips. Keep original collapsing-floor warnings and open holes clear.
- [x] Visible crimson crystal clusters on suitable front/side walls supply
  contour-clipped light; secret rock aliases cast identical shadows.
- [x] Sparse native-pixel ash, falling grit and thin broken ground haze, clipped
  to discovered terrain and driven solely by presentation time.
- [x] Dedicated quiet recorded ambience: dungeon air/rumble and distant stone
  creaks, credited CC0 sources, existing volume/pause/focus/transition lifecycle.
- [x] Cohesive monster tint, contact shadows and landing dust; unchanged poses,
  scale, hover and battle artwork.
- [x] Verify 632 core/input, 265 focused and 3,862 full graphics checks,
  including 360 tour frames. Preserve before/after and motion evidence. No repackaging.

Implementation and verification are complete. The user approved the appearance
after reviewing the local build on 2026-10-11 ("ok looks good").
Original maps, mechanics, routes, secrets, platforms, rescues and encounters are
unchanged. Visual checks use isolated adventures. Matching rock/crystal/floor
overlaps are verified against their actual neighboring-room context, preserving
authored differences where rooms meet other caves. The scoped simplicity review
found no remaining issues. [Preview](evidence/2026-10-11-final-cave/gameplay-92.png),
[atmosphere clip](evidence/2026-10-11-final-cave/atmosphere.mp4), and
[verification](evidence/2026-10-11-final-cave/review.json).

## Water Cave raft presentation — 2026-10-11

User reported standing beside the logs and abrupt tile jumps. The generic prop
offset placed the deck 19 native pixels above the hero's boots, while raft and
hero used separate movement presentations.

- [x] Align the existing pixel-art deck beneath the boots; preserve the atlas.
- [x] Interpolate each accepted raft move over 0.9 seconds, sharing position and
  one-pixel buoyancy with the rider. Keep its original route/tick cadence.
- [x] Preserve walking onto/off the raft, including a raft tick during boarding;
  keep the carried hero planted and silent, with a wood contact on boarding.
- [x] Verify 632 core/input checks and 257 focused Water Cave checks, including
  all eight rafts, pause, map/actions, save/load aboard and other-cave isolation.
- [x] Finish 3,606 full graphics checks and 360 tour frames, including the existing
  raft rescue/save/reload/return route. All 33 personal files and 27 Core/map files
  remain byte-identical; the local Release has zero warnings/errors.

Only the eight raft rooms differ among 115 native map captures; 32 other-cave/town
references are identical. The scoped simplicity review found no remaining issues.
[Raft motion](evidence/2026-10-11-water-raft/raft-glide.mp4) and
[verification](evidence/2026-10-11-water-raft/review.json). Local builds only;
implementation is complete and the user approved the raft presentation after local verification on 2026-10-11.

## Water Cave remaster — 2026-10-10

User approved proceeding with the next cave remaster. Scope: thirteen authored
rooms, upper D1/E1/F1 and D2–D6 (44/45/46/50/56/62/68/74), deep D2–D6
(90/96/102/108/114). Final Cave is separate. The cave skill's approved connected
stone and material-specific floor techniques guide this treatment.

1. [x] Resolve all rooms, inspect native maps and approved breeding/Fire references;
   retain matching native, lit and unlit before views plus unaffected references.
2. [x] Replace tiled rock with connected eroded limestone, shaded wet faces,
   small facets, mineral trails and sparse teeth at the rock foot.
3. [x] Replace the water-like dry floor with quiet worn limestone and silt;
   distinguish deep turquoise pools and soften banks using world-anchored erosion.
4. [x] Motivate turquoise contour light with visible algae; add pixel drips,
   ripples, submerged caustic fragments and thin water-only mist. Keep native pixels.
5. [x] Integrate creature tint/contact shadows, actual-pool hero reflections below
   rafts, and a dedicated quiet CC0 cave/water audio mix using existing recordings.
6. [x] Finish focused and full local verification, inspect final gameplay views,
   retain evidence and confirm original maps/Core/personal-save preservation.

Core movement, raft rules, doors, secrets, stairs, rescues and encounters remain
unchanged. Water Cave has ordinary traction; Ice Cave's new sliding is separate.
Implementation and aesthetic approval are tracked separately. No packaging.

Verification passed: **627 core/input checks, 211 focused Water checks, 3,560 full
graphics checks and 360 tour frames**. Local Release: zero warnings/errors. Only
the thirteen Water rooms changed among 115 native maps; 32 other-room reference
captures are identical. All 33 personal files and 27 Core/original-map files are
unchanged. The scoped simplicity review found no remaining issues. Matching
authored overlap neighborhoods retain identical sediment; original mismatched
banks are preserved. [Gameplay](evidence/2026-10-10-water-cave/gameplay-62.png),
[motion](evidence/2026-10-10-water-cave/water-cave.mp4), and
[verification record](evidence/2026-10-10-water-cave/review.json).

## Ice Cave traction — 2026-10-10

User follow-up: make the character slide and struggle for traction on the frozen
floor. Applies by default to the eleven resolved Zephyr Cave rooms, on visible
ice floor 0x53 and its matching 0x49 alias.

The October 12 entrance repair above adds precise traction near obstacles and
travel points; the timings below now apply to the open floor.

- [x] Slower accelerating push-off (340 ms), steady glide (220 ms), and one
  decelerating tile (420 ms) after releasing or changing direction.
- [x] Reuse eight-way keyboard/controller intent and the existing pixel sprite;
  planted-foot glides stop the walking cycle and suppress repeated footfalls.
- [x] Route every accepted step through Adventure.Move, preserving collision,
  monsters, encounters and events. Automatic drift stops before doors, stairs,
  boundaries, secret walls and changes of floor material.
- [x] Clear momentum for pause, focus loss, menus, interactions, load and restore.
  Preserve the existing 250 ms walking and input buffering outside the Ice Cave.
- [x] Verify 627 core/input checks, 254 focused graphics/integration checks, and
  3,358 full graphics checks with 360 tour frames. Retain an authored-room
  movement clip from an isolated adventure. The Release build has zero warnings/errors.

No save-schema, original-map, Core gameplay or packaging changes. Momentum is
transient desktop state. Current controls explains the early-turn/braking behavior.
[Movement clip](evidence/2026-10-10-ice-movement/ice-slide.mp4) and
[validation evidence](evidence/2026-10-10-ice-movement/review.json).

## Reflective Ice Cave floors — 2026-10-10

User follow-up: make the remastered ice reflective. Keep the existing frozen
terrain, lights, sounds and gameplay; add quiet pixel-art reflections beneath
the hero and monsters throughout the same eleven Zephyr Cave rooms.

- [x] Reuse the actual hero/monster poses, native pixels, animated foot anchors,
  facing, creature size and hover height.
- [x] Cache reflectivity with the existing floor. Clear sheets reflect more;
  frost, fractures and scuffs weaken the image. Fade with distance; no water ripples.
- [x] Draw beneath scenery/actors; clip at walls, closed doors, stairs and fog.
  Occlusion stops a reflection from resuming beyond a blocking wall or door.
- [x] Inspect entrance, rescue and large-monster gameplay views and test frozen
  and moving poses, aliases, clipping, changed terrain and gameplay/RNG isolation.
- [x] Finish shared renderer regressions, retain evidence and open the updated preview.

Focused validation: **219 Ice checks passed**, including 17 reflection checks.
The local Release build has zero warnings/errors. The implementation adds one
cached byte mask to `WorldIce` and reuses the sprite atlases/animation selection;
no render targets, shaders, settings, new artwork or per-frame buffers.

The shared reflection/animation integration passed **3,322 full graphics checks**
and 360 tour frames. A final Ice-only mask refinement then enabled reflection in
the exposed floor at carved rock corners; the 219-check focused run verifies
that boundary and retains a walking sequence on a valid open route. Other cave
and town water reflections remain pixel-identical in 24 comparison captures.
Among 115 native full-map captures, only the eleven Ice rooms changed. All 33
personal files and 27 core/map files remain unchanged. The scoped simplicity
review found no remaining issues. User aesthetic approval is pending.
[Updated preview, motion, comparisons and logs](evidence/2026-10-10-ice-reflections/review.json).

## Ice / Zephyr Cave remaster — 2026-10-10

Scope resolved from the surface A3/map13 Zephyr sign, its cave entrance, the
original terrain/events and connected cave rooms. Upper: A1/B1/A2/A3, maps
41/42/47/53. Deep: A1/B1/A2/B2/A3/B3/A4, maps81/82/87/88/93/94/99. All eleven
rooms are authorized; surface snow and neighboring cave families are separate.

Apply the `remaster-yipe-caves` skill: connected frozen rock with small ice facets,
broad quiet ice sheets, sparse fractures/scuffs/frost, visible cold crystal light
with native contour occlusion, cohesive creatures and restrained frost motion.
Recorded wind/ice ambience uses the existing audio lifecycle. Preserve topology,
doors, secrets, stairs, encounters, rescues and personal saves. Local build only.

- [x] Resolve scope; inspect current native Ice Cave and accepted breeding/Fire references.
- [x] Retain baseline native, gameplay, unlit and isolated-light captures for all
  eleven Ice rooms and six unaffected references.
- [x] Implement cached terrain, contour lights, sparse glints/falling frost, world
  creature integration and a quiet recorded ambience loop.
- [x] Inspect all eleven native rooms and representative gameplay views; retain
  before/after captures and an eight-second motion clip. User aesthetic review is pending.
- [x] Verify all linked rooms, overlap/secret/cache/fog/door behavior, audio lifecycle,
  creature/battle isolation and full gameplay/RNG preservation; run shared regressions.
- [x] Retain evidence, review the scoped diff for simplicity and record the ready local build.

The floor reads as broad frozen sheets with shallow selected seams, scuffs and
frost deposits. Wall facets are small and connected, with dark front faces;
mineral inclusions retain the secondary stone's identity. Embedded cyan crystals
cast steady local light, while icicles, occasional glints and slow frost motes
provide restrained activity. Deep floors and ambience are slightly quieter.

Audio uses Thimras's CC0 recorded wind and ecfike's CC0 *Ice Crack 5.wav*, mixed
into one quiet 58-second loop. The public HQ MP3 preview was the crack input;
the original WAV requires a separate login. Source URLs, hashes, processing
recipe and credits are retained. No synthesized audio or new audio framework.
Existing sampled stone footfalls suit the hard frozen ground.

The scoped simplicity review follows the existing Stone/Earth partial renderer:
one cached terrain texture, tile signature, contour mask and local source lists;
shared lighting, monster shading and the existing single ambient voice. No new
settings, gameplay state, asset framework or package dependencies. Four older
cave tests now use untreated map54 instead of newly remastered map53 for their
neutral creature/audio reference. No unrelated production edits were needed.

Validation: **202 focused Ice checks**, **3,306 full graphics checks** and
**613 core checks** passed; the full suite also rendered 360 tour frames.
The local Release rebuild has zero warnings/errors. All nine matching floor
overlaps align; source/destination fog, closed/open doors, secret aliases and
changed-map caches pass. All 115 native map captures were compared with the
preceding overworld run: only the eleven Ice rooms differ. All 24 current-task
comparison captures for Maynard/Stone/Earth/Breeding/Fire/Hell are pixel-identical.
All 33 personal files and 27 core/original-map files remain unchanged.

The focused run also passed with overflow checking enabled. A full checked run
hit the existing outdoor hash's intentional wraparound arithmetic; the final
full run used a fresh normal Release rebuild. Temporary diagnostic edits were
restored. User aesthetic/audio review remains pending; no personal adventure
was launched or staged. [Captures, clip, source credits and verification](evidence/2026-10-10-ice-cave/review.json).

## Cloud silhouette rework — 2026-10-09

Goal: rework the unapproved cloud shadows into small, recognizable puffy pixel-art
silhouettes with restrained shading, feathered edges and natural drift. Preserve
terrain sharpness, coordinated sunlight/hero/dust response, weather, controls,
gameplay and personal saves. Test locally without repackaging and open the finished
build with the existing adventure for user review.

- [x] Inspect the existing mask and in-game captures: repeated flat-bottom shapes,
  regular spacing and broad blur weaken the cloud crowns.
- [x] Replace the common stamp with varied connected puff silhouettes, rounded
  undersides, irregular separated placement, smaller sizes and thinner edges.
- [x] Inspect native masks, normal gameplay captures and moving sequences; refine
  based on the rendered result.
- [x] Verify clouds, sunlight/actor sampling, weather/day-night, settings, native
  pixels and gameplay isolation; review the scoped diff for simplicity.
- [x] Open the verified local build with the user's latest existing adventure.

Validation: clean local Release build and **211 focused graphics checks** passed.
The final mask contains 20 separate clouds, 46–65 native pixels wide and 27–37 high
at alpha >= 32; occupied area is 9.9%. Connected crowns share one density envelope
so the lobes do not become separate dark bubbles. The feather radius is three
native pixels rather than six; scene artwork remains sharp. Horizontal drift is
2.4 rather than 3.2 pixels/second. Existing shade strengths remain unchanged.

The scoped simplicity review covered the mask/drift change, focused runner and
capture additions: no new production fields, settings, dependencies, per-frame
allocations or gameplay paths. No findings remain. Six outdoor scenes, neutral
masks and sampled frames across 24 seconds were inspected. All 33 personal files
remained unchanged through verification. The latest adventure is open and verified
by screenshot. Normal startup encountered a local macOS display-link wait; a
temporary launcher runs the identical Desktop assembly with refresh sync disabled
for this process only. Game timing and saved preferences are unchanged. Diagnose
the normal-launch stall separately. **Resolved 2026-10-12:** see the normal macOS startup section above. The user approved the current clouds on 2026-10-12: “im happy with clouds”.
[Captures, motion clip, logs and review](evidence/2026-10-09-cloud-rework/review.json).

## Overworld remaster — 2026-10-09

Goal: complete a cohesive pixel-art presentation pass across all 36 surface
regions, with Maynard as the accepted quality reference. Preserve routes,
encounters, secrets, map/save formats and the approved snow footprint. Use local
builds, without repackaging. The region audit identified Ervyn C5/map27, Quizilog
B2/map8, the D3/D4/E4 travel corridor and B4/map20 woodland as initial references.

- [x] Extend Ervyn with clustered garden planting and tended soil beds; extend
  Quizilog with dry scrub, pebbles, palm shade/litter and quieter sand variation.
  Existing paved shop approaches remain intact.
- [x] Replace repeated meadow fields with connected, restrained native-pixel
  variation, greener banks, shaded tree ground and broken worn road verges.
  Shared world coordinates account for overlapping rooms and wrapping edges.
- [x] Dress every remaining surface region with grouped low vegetation following
  rivers, cliffs, existing trees and flower clearings. Reuse the Maynard plant
  art and preserve open ground, door approaches, water and snow.
- [x] Add canopy leaves, shrub leaves in Ervyn, butterflies, bank insects and
  drifting meadow seeds. Surface crowns/reeds sway by one pixel with stationary
  roots. Daytime wildlife stays out of night/rain; dry dust stays out of showers.
- [x] Add smoothly mixed daytime birds, quiet night crickets and habitat wind.
  Proximity-based water fades/pans from actual water pixels; lakes are quieter
  than streams. Mute, master volume, focus, overlays and action ducking apply.
  Sources and processing: `Content/Audio/Ambience/outdoor-sources.json` and
  `OUTDOOR-CREDITS.txt`; reuse existing Maynard wind/river recordings.
- [x] Finish full graphics verification, retained comparisons and review record.

Validation: Release build has zero warnings/errors; 184 focused overworld checks,
38 focused Maynard transition checks, **3,104 full graphics checks**, **360 tour
frames** and **613 core checks** pass. Full verification used `--record
--capture-monsters 1`: all monster checks run, with extra videos limited to one
unchanged roster example. The old river-transition assertion was updated to
verify the intentional fade and eventual voice disposal instead of immediate
silence. All 27 core/map source files remain byte-identical to the task baseline.
Of 115 matching full-map captures, precisely the 35 targeted surface maps changed;
Maynard and all 79 underground/alternate captures remain byte-identical.
[Region checklist, previews, logs, audio provenance and review](evidence/2026-10-09-overworld/review.json).

Implementation uses a cached surface texture and bounded lists in the existing
partial WorldRenderer; it introduces no gameplay objects or persistent state.
The initial smooth ground patches were revised to lower contrast with broken
pixel edges and more grouped vegetation after visual review. Maynard's own
renderer and reduced river mix remain intact. User aesthetic review is pending.

## Maynard leaves and ambient sound — 2026-10-09

Resolved Maynard as surface E3/map17 from the original tables and inspected its
actual town renderer. The river bends west and runs beneath bridges at 3,3 and 2,6.
The request adds atmosphere; terrain, buildings, collision and gameplay stay intact.

- Replaced four faint leaf flecks with sparse native-pixel leaves that flutter,
  drift, settle and fade. Four turning poses use existing green/ochre colours;
  deterministic 12Hz presentation time consumes no gameplay RNG. Leaves draw
  behind buildings and the character, over open grass and paths.
- Added CC0 recordings: isaiah658’s birds, Thimras’s wind and small river. Daytime
  birds/wind give way to quieter wind at night; no synthesized sound. Three
  58-second loops have crossfaded seams, hashes, processing records and credits.
- River audio measures nearby pixels in the rendered water mask, including bends
  and both bridges. Its independent loop eases up/down with interpolated player
  distance, pans toward the bank, fades out inland and releases its voice.
- Reused master volume, sound toggle, pause/map/overlay/focus gates, action-sound
  ducking and exit disposal. Leaving Maynard restores existing biome ambience;
  other towns do not inherit these sounds or leaf drawing.

Checked Release build passes with zero warnings/errors; 37 focused checks,
2,941 full graphics checks with 360 tour frames, and 613 core checks pass.
All 120 map, 116 Earth, 68 Stone, 73 breeding, 129 Fire, 71 Hell and 64 other-town
captures match the previous local build. Isolated checks cover leaf timing and
occlusion, both bridges, walking attenuation/pan, sound gates, day/night changes,
voice cleanup and full gameplay/RNG preservation. Original map tables and Core
source are unchanged. The separately running personal game saved and exited
during final checks; its newer 55-region save, matching journal and preferences
were preserved. [Evidence](evidence/2026-10-09-maynard-atmosphere/review.json)
contains screenshots, an eight-second leaf/ambient preview and a separately
labelled river-distance audio demonstration. The user accepted the presentation
with “this is great,” then requested a quieter river. The follow-up lowers only
the river gain from 0.32 to 0.24 (25%); bird/wind levels, distance fade and panning
are unchanged. The local checked Release rebuild and all 37 focused checks pass.
The earlier river audio preview uses the original, louder level. Local build
only; no repackaging.

## Hell colour and ambient audio — 2026-10-09

The user likes Hell’s existing architecture but requested richer reds and missing
atmospheric sound. Resolved the alternate-layer A4–D4/maps117–120 independently
from map labels, original map117 events and current native/gameplay captures.
Maps118–120 regenerate; fixed-seed fixtures do not claim to reproduce the live
adventure’s current layout.

- Preserved the connected stone, floor structure, grey entrance paving, ironwork,
  creature art, heat shimmer, embers, smoke, ash and flame animation.
- Enriched the existing warm palette after quantization and slightly strengthened
  the red molten-seam glow; retained native pixel boundaries and neutral ash.
- Added a dedicated 58-second recorded ambient loop: lava flow, a lower-pitched
  rumble layer, restrained fire crackle and filtered cavern air. It reuses verified
  Blender Foundation (CC BY 3.0), PagDev (CC0) and JaggedStone (CC0) recordings,
  with full attribution and processing/hash records alongside the asset. No new
  downloads or synthesized noise. The adapted mix is CC BY 3.0.
- Reused the single ambient voice, entry fade, volume/mute and pause/map/overlay/
  focus gates, action-sound ducking and biome-exit disposal. All four Hell rooms
  continue the same loop; neighboring alternate regions do not inherit it.

Checked Release build passes without warnings/errors; 119 focused checks,
2,913 full graphics checks with 360 tour frames, and 613 core checks pass.
Before/after captures cover all four rooms and nine generated layouts. Exactly
four full-map captures changed; Earth, Stone, Fire and breeding references match
byte-for-byte. Gameplay, RNG, terrain, events and personal files are preserved.
[Evidence](evidence/2026-10-09-hell-atmosphere/review.json) includes local logs,
comparisons, source hashes and a six-second preview with default-volume audio.
User visual/audio review is pending. Local build only; no repackaging.

## Stone Cave remaster — 2026-10-09 (implemented and verified)

Scope independently resolved from map labels, original tables and current native
captures: upper E2 map51, F2 map52, F3 map58 (entrance at7,4), F4 map64,
F5 map70, F6 map76 (down stair10,9); deep F4 map104, F5 map110 and F6 map116
(up stair10,9 and rescue1,1). E2's flooded rescue chamber stays intact.

- Replace repeated wall tiles with connected cool limestone/granite, small facets,
  chipped ledges, mineral seams and shaded front faces.
- Make a quieter worn stone floor: broad shelves, sparse shallow fissures, mineral
  grit and damp water deposits. Keep doors, secrets, stairs and event cells clear.
- Share the stone silhouette with contour lighting; visible wall torches, no
  unmotivated glow or square contact shadows.
- Add restrained native-pixel drips, settling grit and localized lamp dust;
  reuse the existing credited cave ambience through the normal audio lifecycle.
- Ground creatures with a mild mineral tint, contact shadows and matching dust;
  retain their animations, silhouettes and battle art.
- Build and test locally, inspect all nine rooms, preserve full gameplay/RNG and
  personal saves, compare other caves, and retain before/after evidence.

All items above are implemented. Local checked Release builds pass with zero
warnings/errors; 183 focused Stone checks, 2,874 full graphics checks with 360
tour frames, and 613 core checks pass. Original water coverage, seven matching
room overlaps, secrets, doors, visibility, cache restoration, sound lifecycle,
creature silhouettes/battle art and full gameplay/RNG preservation are covered.
Only the nine Stone rooms differ in full-map comparisons. All Earth, Fire,
breeding and other lighting references match their current baselines exactly.
The personal save, map journal, profile, Core source and original map tables are
unchanged. Native before/after captures, default gameplay views, isolated lights,
a five-second atmosphere clip, source hashes and logs are retained in
[evidence](evidence/2026-10-09-stone-cave/review.json). Ambience reuses the existing
credited CC0 air loop and wetter Earth mix; no new audio assets were downloaded.
The user accepted the local Stone Cave build with “this looks good” and moved on to Hell. Local build only; no repackaging.

## Earth Cave remaster implementation — 2026-10-09

Completed goal: implement all six approved review recommendations across upper E3–E6
(57/63/69/75) and deep E4–E6 (103/109/115). Keep the native pixel-art treatment,
original topology, events, secrets, creatures, saves and gameplay RNG intact.

1. **Ambience:** separate upper/deep loops built from licensed cave air, dripping
   water and quiet granular samples; reuse the shared fade, volume, pause, focus,
   overlay and biome-exit lifecycle. Source credits ship alongside the WAV files.
2. **Atmosphere:** brighter drips and ripples, localized lamp dust, fungus spores,
   occasional falling grit, and more frequent drips below ground; deterministic
   scene time and discovered tiles only.
3. **Lighting:** the terrain's cached rock silhouette drives light occlusion;
   lamps and visible fungi supply warm/green local light, including door shadows.
4. **Walls:** smaller warm sedimentary facets, chipped contour edges and deeper
   front faces; preserve roots, fossils, old masonry and mineral veins.
5. **Floor:** compacted soil, sparse embedded stones and damp silt by actual
   shorelines; granular dry footsteps and existing water contacts/reflections.
   Avoid the dense slate fractures rejected in the breeding-ground iteration.
6. **Creatures:** restrained earthy world tint, stronger existing contact shadows,
   and soil-coloured landing dust. Preserve silhouettes, animation and battle art.

All six steps are implemented and locally verified. The full graphics run passed
2,700 checks and 360 tour frames; core checks passed 613. Final focused runs
passed Earth 240, breeding 131 and Fire 207 checks. All 73 breeding and 129 Fire
reference captures match exactly; the personal save/journal/profile, Core source
and original map tables are byte-identical to the pre-task checkpoint. Native
before/after, gameplay, lighting, a five-second atmosphere clip, source credits,
source hashes and verification logs are retained in
[evidence](evidence/2026-10-09-earth-remaster/review.json).
The new sound uses CC0 material from Independent.nu, JaggedStone and Fantozzi.
The user accepted the local Earth Cave build with “ok great” and moved on to Stone Cave. No repackaging.

## Earth Cave review — 2026-10-09

Reviewed seven rooms with `remaster-yipe-caves`: upper E3–E6 (57/63/69/75),
deep E4–E6 (103/109/115). Confirmed that ambient audio selects only Fire and
breeding tracks; Earth starts no ambient loop. Its visual drips, ripples, dust
and fungus glow already exist, but are sparse and faint. Earth also retains the
older tile-based light occlusion and torch cutoff, with no contour mask.

Recommended order: add suitable recorded cave ambience and clearer localized
atmosphere; adapt contour lighting to Earth's own rock silhouette and sources;
then refine warm sedimentary walls, compacted-earth floors, material footsteps
and creature integration. Preserve roots, fossils, puddles/reflections and room
identity. Avoid the dense fractured-slate treatment the user disliked elsewhere.

This was a review only: no rendering/audio/gameplay changes or new test run.
Compared all seven native maps, representative gameplay views and current source.
Retained captures come from the prior 2,629-check run; the relevant renderer source
matches the pre-breeding-floor snapshot. No live adventure was navigated.

[Lit entrance view](evidence/2026-10-09-earth-review/gameplay-57-lit.png) ·
[Deep damp chamber](evidence/2026-10-09-earth-review/gameplay-109-presentation.png) ·
[Findings and proposed work](evidence/2026-10-09-earth-review/review.json).

## Breeding-ground calmer slate — 2026-10-09

The user found the exposed slate too fractured. Reduced seed density by about
half, narrowed and lightened the joints, and interrupted them with earth. An even
slate tone removes the contrast that outlined every piece; fewer interior marks
leave larger intact areas. The original prop/dust shades remain in place, with
one added slate base colour to keep the floor below the wall tops in brightness.

Checked Release is clean and all 131 focused GPU checks pass across six rooms.
An intermediate brighter floor failed the existing contrast check in map 66;
the final tone passes the unchanged assertion. Before/after comparison confirms
60,288 unchanged wall-core pixels and six identical light masks. Other desktop,
core and map sources are unchanged. Full suite was not rerun for this localized
floor correction. Tests used isolated sessions; personal-file observations are
in the evidence. Local build only; no restart of the live adventure.

[Before](evidence/2026-10-09-breeding-floor-soft-seams/gameplay-59-before.png) ·
[After](evidence/2026-10-09-breeding-floor-soft-seams/gameplay-59.png) ·
[Evidence](evidence/2026-10-09-breeding-floor-soft-seams/review.json).
User visual review is pending.

## Superseded: breeding-ground exposed slate floor — 2026-10-09

The user found this floor too fractured. The calmer slate correction above
replaces its dense dark joints and individually contrasted faces.

The user found the previous floor barely different. Replaced the sparse, faint
fragments with exposed fractured slate beds covering much more of the open floor.
Broader grey faces and dark joints remain visible with the normal Strong miniature
effect and scanlines. Worn-earth deposits interrupt the stone; shallow cleavage
marks and fine grit provide smaller detail. The floor uses the existing mineral
palette and stays flatter and darker than the wall tops.

This is a localized replacement of the floor branch in `WorldBreeding.cs`. Checked
Release builds cleanly and all 131 focused GPU checks pass. Before/after captures
verify 60,288 unchanged wall-core pixels and six identical lighting masks. Other
desktop/core/map sources and personal save/journal/profile hashes are unchanged.
The prior full run passed 2,629 checks; it was not rerun for this floor correction.
No repackaging or live-game restart. User visual review is pending.

[Before](evidence/2026-10-09-breeding-floor-bedrock/gameplay-59-before.png) ·
[After](evidence/2026-10-09-breeding-floor-bedrock/gameplay-59.png) ·
[Entrance](evidence/2026-10-09-breeding-floor-bedrock/gameplay-60.png) ·
[Evidence](evidence/2026-10-09-breeding-floor-bedrock/review.json).

## Superseded: subtle breeding-ground floor refinement — 2026-10-09

The user reported that this looked too similar to the previous ground. The
stronger exposed-slate revision above replaces this treatment.

Applied the updated cave-remaster skill to the six breeding-ground rooms: upper
A4/B4/A5/B5 and deep A5/B5 (59/60/65/66/105/106). Compacted mineral-earth patches,
partly buried slate fragments, short scrape marks and sparse fine grit give the
floor a clearer material. Flattened the initial stones and broke their highlights
so they read as ground instead of raised obstacles. The approved four-colour ramp,
wall contours, lighting, nests, creature treatment and gameplay remain intact.

Only the non-stone texture branch in `WorldBreeding.cs` changed. The checked local
Release is clean and all 131 focused GPU checks pass, including linked-room
continuity, secrets, fog, cache changes and full session/RNG preservation. Across
all six rooms, 60,288 sampled wall-core pixels and every local-light mask match
the prior build. Core and original-map source hashes are unchanged. Tests used
isolated sessions; personal-file observations are recorded without restoring or
replacing the live adventure. The previous full suite passed 2,629 checks; it was
not rerun for this floor-only correction. No repackaging or relaunch.

[Before](evidence/2026-10-09-breeding-floor/gameplay-59-before.png) ·
[After](evidence/2026-10-09-breeding-floor/gameplay-59.png) ·
[Entrance](evidence/2026-10-09-breeding-floor/gameplay-60.png) ·
[Evidence](evidence/2026-10-09-breeding-floor/review.json).
User visual review is pending.

## Graphics submenu — 2026-10-09

All ten visual options now live in **Pause > Graphics**: Fullscreen, Scanlines,
Miniature effect, Vignette, Pixel lighting, Sun shafts, Cloud shadows, Rain,
Screen shake and Time of day. The main pause menu fits on one page and retains
Battle pacing. Escape/East or Start returns from Graphics to its highlighted
parent entry without resuming play. Existing defaults and saved preferences
are preserved, and the in-game help describes this navigation.

Checked local Release builds cleanly. All 66 focused menu checks and 2,629 full
GPU checks pass, including each moved setting and its persistence behavior.
The full run also renders 360 tour frames and verifies the latest Fire Cave floor.
Menu captures cover 640×360, 1024×768 and 1920×1080. Synthetic controller actions
pass; physical-controller testing remains open. Personal save/journal hashes
are unchanged; observed profile updates were left intact. No package was rebuilt.

[Pause](evidence/2026-10-09-graphics-submenu/pause-menu.png) ·
[Graphics](evidence/2026-10-09-graphics-submenu/graphics-menu.png) ·
[Evidence](evidence/2026-10-09-graphics-submenu/review.json).

## Fire Cave cooled-lava floor — 2026-10-09

The user preferred the red-hot walls/room (“this is much better”) and requested a
better floor. Replaced the broad smooth floor patches with irregular flat crust
plates, shallow folded flow ridges, fine ash/grit and sparse sealed ember seams.
The thicker unstable-floor cracks retain their last-drawn warning detail.

This changes only the non-stone terrain branch in `WorldFire.cs`. The preferred
wall joints, magma, red ambient light and creature treatment are retained. All
eleven rooms render successfully; checked Release is clean and 207 focused GPU
checks pass. Wall/magma core pixels and all eleven local-light masks are identical
to the prior version. The full suite was not rerun for this floor-only correction;
the previous red-glow revision passed 2,615 checks. Tests used isolated saves.
Personal files changed during the session; their updated contents were left intact.

[Before](evidence/2026-10-09-fire-floor/gameplay-67-before.png) ·
[Open room](evidence/2026-10-09-fire-floor/gameplay-67.png) ·
[Entrance](evidence/2026-10-09-fire-floor/gameplay-72.png) ·
[Evidence](evidence/2026-10-09-fire-floor/review.json).
Local build only. The user accepted the floor ("awesome") before requesting the Graphics submenu above.

## Preferred Fire Cave red heat correction — 2026-10-09

The user reviewed this revision and said “this is much better,” then requested
the floor improvement recorded above.

The user rejected the first remaster's cool appearance and specifically requested
red joints between wall rocks plus red light throughout the room. The same eleven
Fire Cave rooms now have ember-red/orange joints, hotter segments near magma,
warmer dark rock/floor colours and room-wide red ambient bounce, stronger below.
The outside wall foot stays dark so the silhouette keeps its depth.

The ambient pass reuses the existing blend states, scales existing world pixels
before heat/UI composition and keeps black/alpha intact. No new texture, shader,
blur or gameplay state was added. Other caves retain their lighting path. Local
Release builds cleanly and 207 focused checks pass. The new real-device check
covers black/transparent controls, unchanged alpha and stronger deep red bounce.
The full suite passes 2,615 checks and 360 tour frames. Only the eleven Fire Cave
map captures changed; 104 other maps, 22 lighting references and all 73 breeding
captures are identical. Personal save/journal/profile hashes stayed unchanged.

[Before](evidence/2026-10-09-fire-red-glow/gameplay-72-before.png) ·
[Entrance](evidence/2026-10-09-fire-red-glow/gameplay-72.png) ·
[Deep chamber](evidence/2026-10-09-fire-red-glow/gameplay-113.png) ·
[Evidence](evidence/2026-10-09-fire-red-glow/README.md).
The hotter wall/room treatment is retained in the floor revision above.

## Initial Fire Cave volcano remaster — 2026-10-09

The implementation below passed its checks, but the user found it too cool and
requested the red-heat correction above. Its captures retain the initial version.

User approved the six-part plan and requested a goal plus complete implementation.
Scope: upper caves C5/A6/B6/C6 (67,71,72,73), deep caves C3/B4/C4/C5/A6/B6/C6
(95,100,101,107,111,112,113), entered from surface B6. Current native entrance,
unstable-floor and deep rescue-room renders were inspected before editing.

- [x] Connected finely fractured basalt and quiet cooled-lava/ash floors.
- [x] Animated magma recessed into existing blocked rock, with cooling crust and bubbles.
- [x] Amber light using the visible contour, with stronger heat deeper down.
- [x] Crisp localized heat shimmer, rising embers, intermittent vent smoke and falling ash.
- [x] Warm creature integration, coherent volcanic details, original routes and events retained.
- [x] Licensed sample-based rumble/bubbling/crackle/hiss ambience and stone/grit footsteps.

All eleven native rooms and entrance/deep gameplay captures were inspected.
Fine stone, floor contrast and magma containment retain crisp pixel art. Magma
openings are authored by room, with sulfur flecks and cooled fragments around
them. Deep rooms have stronger heat, more particles and louder ambience.
Unstable-floor cracks, doors, secrets, stairs, encounters and rescues are intact.

The 58-second loop mixes Blender Foundation's CC-BY-3.0 Lava loop and PagDev's
CC0 Fireplace Sound loop; source links, attribution, processing and hashes ship
with the content. Existing audio controls/lifecycle and sampled footsteps are
reused. No synthetic oscillator or new gameplay system was added.

Checked Release has zero warnings/errors. 613 core, 206 focused GPU and 2,614
full GPU checks pass, including all 41 monsters and 360 tour frames. Of 115 map
captures, only the eleven Fire Cave rooms changed. The other 104 maps, 22 lighting
references and all 73 breeding-ground captures are byte-identical to the approved
breeding-contour build. Core source and original map data remain unchanged.

The runner uses isolated saves. The previously open personal adventure's files
changed during this work; their later contents were left intact, with no restore
or replacement performed. Hash observations are retained separately rather than
claiming the entire session's personal files were byte-identical.

Local build only, no package. [Captures, clip and verification](evidence/2026-10-09-fire-cave/README.md).
Implementation is complete; user visual/audio review remains pending.

## Approved: breeding-ground light contours — 2026-10-09

User reviewed the updated local build and approved the result: “this looks great!”

Reusable workflow: `$remaster-yipe-caves`, stored in the repository at
[.agents/skills/remaster-yipe-caves/SKILL.md](../.agents/skills/remaster-yipe-caves/SKILL.md). Its companion reference records
the approved pixel-art, floor/creature, contour-lighting and verification
techniques. Adapt the treatment to each cave's own palette, theme and resolved
map scope; the breeding grounds supply a quality reference, not a shared layout.

The user noticed square-looking cave lighting after the floor/monster update.
The breeding grounds' upper A4/map59 reference showed two separate causes:
ground darkness measured from square cells instead of the chipped rock, and
light visibility accepted or rejected whole 32-pixel tiles.

Ground contact now uses the actual stone silhouette with a short two-shade edge.
The light pass shares that cached silhouette, traces native-pixel visibility and
feathers occlusion by one pixel. Cave lamps retain circular reach above their
source; doors and discovery masks still block light. Terrain mutations rebuild
the mask, secret aliases remain identical, and the existing tile-lighting path
continues outside these six rooms. The wall artwork itself is unchanged.

Seven new focused regressions cover lamp reach, light grazing a carved corner,
a partial-light edge, secret equivalence, blocked/restored door paths and bare
earth at cut corners. Captures include the isolated light masks and lighting
on/off views at default gameplay scale. Rendering uses isolated sessions and
leaves the personal save, journal and profile unchanged. Checked Release builds
cleanly; 131 focused and 2,414 full GPU checks pass, including 360 tour frames.
Only the six breeding-room map captures changed; 109 other map captures and
22 other-biome lighting captures are byte-identical.
[Before](evidence/2026-10-09-breeding-light-contours/gameplay-59-before.png) ·
[After](evidence/2026-10-09-breeding-light-contours/gameplay-59.png) ·
[Verification](evidence/2026-10-09-breeding-light-contours/review.json).

## Review pending: breeding-ground floor and creature lighting — 2026-10-09

The user preferred the finer walls but found the floor out of place and clarified
that the monsters stood out as if they did not belong in the environment. The
live save resolved to upper A4/map59 at 8,3; current native and gameplay references
were inspected without moving the live player.

The floor uses four restrained mineral-dirt shades, fine grey gravel and low
contrast patches. Nest hollows and debris shadows share the new ground, while
straw stays warm. Drifting grit and creature foot dust use the same mineral shade.
The wall rendering and all authored layouts remain intact.

World monsters receive a subtle cool ambient colour and stronger contact shadows
only in these six rooms. The existing sprite poses, scales, hover heights and
shadow footprints remain; battle portraits and appearance elsewhere are unchanged.
This reuses the existing scene map and sprite tint/shadow inputs; no new texture,
lighting framework, animation asset or gameplay state was added.

Checked Release builds cleanly. All 124 focused checks and 2,407 full GPU checks
pass. Nine new assertions cover grounded, hovering and large creatures, combat
artwork isolation and exact appearance restoration on biome exit. Comparing all
115 map captures changes only the six breeding rooms; the other 109 are identical.
[Before](evidence/2026-10-09-breeding-cohesion/gameplay-59-before.png) ·
[After](evidence/2026-10-09-breeding-cohesion/gameplay-59.png) ·
[Verification](evidence/2026-10-09-breeding-cohesion/review.json).
The local build is ready; the live game was not relaunched during this task.

## Preferred wall direction: breeding-ground stone detail matched to Hell — 2026-10-09

The user found the angular walls better, but too low resolution beside Hell.
Both renderers use the same native resolution; the mismatch was detail scale.
The breeding stone spacing was 34×24 pixels, versus 13×11 in current Hell.
Compared the current verified Hell A4/map117 native and gameplay captures to
upper-cave B4/map60 before modifying only `WorldBreeding.cs`.

The revised stone pass uses Hell's smaller facets, narrow joints and upper-face
highlights, anchored in world coordinates for matching overlaps. It keeps the
slate palette, angular wall silhouette and shaded front faces, and removes the
redundant broad-slab crack/speckle rules. Hell rendering itself is unchanged.
All six revised native views and the default entrance gameplay view were inspected.
All 115 existing focused checks pass; pixel comparisons found zero changes outside
wall cells. No new tests, assets, state, helper types or packaging were needed.

[Before](evidence/2026-10-09-breeding-wall-detail/gameplay-60-before.png) ·
[Hell reference](evidence/2026-10-09-breeding-wall-detail/hell-reference-gameplay.png) ·
[After](evidence/2026-10-09-breeding-wall-detail/gameplay-60.png) ·
[Verification](evidence/2026-10-09-breeding-wall-detail/review.json).
User review remains pending. The existing game was left running with its previous
build; use the canonical full adventure on the next requested launch.

## Superseded: breeding-ground broad wall correction — 2026-10-09

The user rejected the first walls as visually wrong. The bright continuous rim,
rounded corners and disconnected grey/purple patches made them read as barriers.
The correction stays in `WorldBreeding.cs`: chipped angular silhouettes,
world-aligned slate slabs with short broken fractures and tiny weathering marks,
11–15-pixel shaded front faces, and narrow foot shadows. The shared rock palette
and native pixel grid remain intact; purple is removed from stone and claw marks.

The entrance (upper B4/map 60) was compared at native and default gameplay size,
then all six native rooms were inspected. The same correction covers their shared
wall pass and still joins matching original region overlaps. No pixels outside
wall cells changed in the six before/after map captures. Existing focused checks
cover secrets, fog, cache invalidation, region continuity and gameplay isolation;
all 115 pass. No new tests, frameworks, assets or gameplay state were introduced.

[Before](evidence/2026-10-09-breeding-walls/gameplay-60-before.png) ·
[After](evidence/2026-10-09-breeding-walls/gameplay-60.png) ·
[Verification](evidence/2026-10-09-breeding-walls/review.json).
User visual approval is still pending; do not describe these walls as approved.

## Complete: Monster Breeding Grounds remaster — 2026-10-09

Goal: bring maps 59, 60, 65, 66, 105 and 106 to the remastered terrain standard
with connected plum/charcoal rock, quiet packed earth, deliberate nesting details,
individual room landmarks, localized light and restrained animation/ambience.
The entry sign is upper B4, map 60 at 4,10. Earth Cave's completed treatment is
retained. This scope uses the six explicit map IDs, not every environment-7 map
(which would incorrectly include Hell).

- [x] Connected irregular walls, broad rock facets and shaded front faces.
- [x] Quiet floor patches, damp hollows, gravel and worn approaches; readable paths.
- [x] Authored nests, straw, eggshells, shed scales, bones and claw marks.
- [x] Entrance chamber (60), communal nests (59), holding cell (65), feeding area (66),
      deep brood chamber (105) and large-monster den (106), respecting their events.
- [x] Sparse fungi and warm entrance/holding-area lights using existing pixel lighting.
- [x] Scene-time drips/ripples, spores and grit; a quiet CC0 cave ambience loop that
      respects Sound/Volume, pause, overlays, focus loss and biome transitions.
- [x] Inspect all six native views and default presentation; verify fog, secrets,
      cache invalidation, gameplay/RNG preservation and audio lifecycle locally.

Implementation uses a cached terrain pass following `WorldEarth`, authored
native-pixel detail shapes and existing lighting/audio boundaries. No new movement,
encounter rules, save fields or gameplay randomness. Review the scoped diff for
simplicity and retain before/after captures and verification evidence. No package.

## Complete: mountain form and cave stone — 2026-10-05

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
- [x] Visually inspect actual F3, village, desert and narrow-passage GPU captures against the earlier artwork, and confirm the tested source still matches the working tree.

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
The final visual audit inspected the original F3 map against the current GPU
area capture, the village cave without an overlapping player, desert F1, E4's
narrow passages and the village gameplay camera. These show broader connected
contours, darker lower faces, intact routes, crisp original rock clusters and a
slate cave rim surrounding a readable dark opening. All recorded production and
test-source hashes still match; only later documentation has changed. The goal
is complete. User feedback can guide further refinements; no new shaders or art
assets were added.

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
| Moving cloud shadows | Reworked; awaiting user visual review | Four distinct small puff silhouettes, shallow curved bases, irregular separated placement, coherent density and three-pixel feathering replace the repeated flat-bottom stamps. Drift is 25% slower horizontally. Retains 7%/14% shade, coordinated shafts and hero/dust sampling, saved Off/Subtle/Strong control and native scene pixels. [Evidence](evidence/2026-10-09-cloud-rework/review.json). |
| Sun shafts | Implemented; ready for visual review | Restrained warm diagonal bands drift outdoors, with native pixel shading and separate saved Off/Subtle/Strong settings. Walking through them adds a warm character highlight, small equipment glint and sparse dry-ground dust. [Shaft evidence](evidence/2026-10-07-sun-shafts/README.md), [actor evidence](evidence/2026-10-07-sun-actor/README.md). |
| Rain and surface splashes | Appearance approved; automatic weather verified | Fine pixel streaks, ground splashes and shoreline-clipped rings. Off/Auto Subtle/Auto Strong control; 5–10 minute clear spells, 30–60 second showers and four-second transitions, timed only during active outdoor play. Existing saved intensity choices remain. [Rain evidence](evidence/2026-10-07-rain/README.md), [automatic weather evidence](evidence/2026-10-07-automatic-rain/README.md). |
| Combat feedback | Expanded presentation and simplified Enemy info accepted | Animated outgoing and individual incoming turns with timed sound, persistent HP/SP/AP and animated bars, direct targeting/area previews, distinct spell/ability effects, optional automatic return to commands, and victory/level-up celebrations. Enemy info now separates a focused portrait/stats card from optional Details after the user found the first panel crowded; inspection remains free and preserves the attack target. Pacing defaults Manual; Screen shake stays separately Off by default. Core combat/reward/RNG/save behavior is preserved. [Plan](BATTLE_UPGRADE_PLAN.md), [battle evidence](evidence/2026-10-07-battle-upgrades/README.md), [inspection layout evidence](evidence/2026-10-07-enemy-info-layout/README.md). |
| Monster animation and movement | All 41 types implemented: 656 native pixel-art poses | Creature-specific idle, travel, attack, hurt and defeat animation; independent 250 ms roaming/pursuing travel and encounter approach; alternating plants, hops, hovers and slithers; dry dust and shoreline-clipped water contacts. Hidden movement stays quiet and modal clocks freeze. Regional bosses retain doubled world scale; the waiting Gatekeeper and its empowered combat variant are covered. Core rules, rewards, RNG and save formats remain unchanged. [Plan](MONSTER_ANIMATION_PLAN.md), [evidence](evidence/2026-10-07-monster-roster/README.md). |
| Walking puddle ripples | Queued | Footsteps create small water rings and disturb the character's reflection. |
| Dusk/night palettes and automatic cycle | Implemented and verified; ready for visual review | Auto is enabled by default for new and legacy profiles. Twenty minutes of exploration: day 10, dusk 2, night 6, dawn 2. Fixed Day/Dusk/Night modes suspend the persisted phase. Moonlight and stronger warm windows preserve readability; sunlight, cloud shade and outdoor heat follow daylight. Modal/focus freezes and underground lighting are preserved. [Plan](DAY_NIGHT_PLAN.md), [evidence](evidence/2026-10-08-day-night/README.md). |

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
| Input | Keyboard/D-pad/stick, gameplay remapping/conflicts, fixed safe menus and persisted profiles; real frontend opening/late play | Physical-controller checks |
| Saving | Full-state format, explicit safe imports, generated/static reopen, platform saving and early/late fresh-process continuations | Wider lifecycle/OS matrix and menu save/load/exit behavior |
| Profiles | Presentation/audio settings, remapped controls, scores, score code, Silly Mode, Help/About and profile policy | Wider preference/lifecycle validation |

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
| P6 | Desktop release verification | Preview.7 verified; physical controller and extended historical routes open | Reproducible complete runs, restart tests and verified MonoGame packages pass |
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
- [x] **P0.3** Add C# build/test CI and a separate graphics-check procedure where
  a real display is required. Clearly distinguish C++ and MonoGame artifacts.
  Added separate `monogame.yml` workflow; actionlint passes. Hosted execution
  awaits a push and is not claimed as verified.
- [x] **P0.4** Reconcile every coverage-inventory feature with a port task or an
  explicit presentation/platform disposition. Record inherited adaptations from
  `docs/DECISIONS.md`, especially D7 and D8, and retain unresolved evidence limits.
  Completed in [FEATURE_DISPOSITIONS.md](FEATURE_DISPOSITIONS.md); assigned P5/P6
  work stays open until implemented and verified.
- [x] **P0.5** Establish a source-control checkpoint containing the approved
  preview, plan and evidence when committing is requested or otherwise authorized.
  Baseline checkpoint 0638981 contains the complete MonoGame port and evidence.
  Later UI/control/package working-tree changes are not part of that checkpoint.

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

- [x] **P5.1** Extend shared terrain families and architecture across all maps.
  Preserve palette, native pixel sizes, anchors, grounded entrances and readable
  paths; record any map-specific art exceptions.
- [x] **P5.2** Complete monster, boss, NPC and character-action artwork with clear
  targeting and combat feedback. Document new assets and generation/edit prompts.
  All 41 canonical monsters now have 656 native pixel-art poses, creature-specific
  travel, real combat/defeat presentation and ordinary/empowered Gatekeeper support.
  [Roster plan and rollout](MONSTER_ANIMATION_PLAN.md); final earned opening, restart and finale all pass. The separate [NPC pass](NPC_ANIMATION_PLAN.md) adds 60 verified poses for the King, Skill Teacher and three Princess appearances; the user approved the King grounding correction and NPC presentation.
- [x] **P5.3** Finish dialogue, inventory, equipment, spells, shops, combat,
  creation and results overlays. Test long text, focus, back/cancel and readable
  layouts at supported display sizes.
- [x] **P5.4** Integrate sound IDs, effect/monster aliases and playback behavior;
  persist volume/sound and display/input preferences with migration handling.
- [ ] **P5.5** Verify all original actions have reachable keyboard/controller
  mappings, including diagonals and Other. Test held input, reconnect, focus loss,
  pause, saving mid-step and resuming with a physical controller.
  On 2026-10-12 the user confirmed no controller is available and approved
  documenting physical verification as unverified for preview.7.
  Keyboard/controller gameplay remapping, conflicts, safe menu controls, persistence
  and failure feedback now pass automated checks. A physical controller remains required.
- [x] **P5.6** Close the P0.4 feature inventory, including explicit dispositions
  for original desktop menu conveniences, help, history and profile information.
  Help, 21 gameplay guide topics, About and stat/save information now pass.
  On 2026-10-12 the user explicitly excluded legacy Save As, native file browsers,
  recent-file menus, printing and desktop text/clipboard features. These are not
  release requirements. Existing save/load and CLI imports remain; ordinary
  menu/save-lifecycle verification continues under P5.3/P6.3.
- [x] **P5.7** Review new regions against V0 and check effect costs on large
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
- [x] **P6.3** Verify fresh-process restarts in early/late game, generated maps,
  Hell and timed-map locations; test imports and interrupted/failed writes.
- [ ] **P6.4** Run full map/asset coverage, overlay/input checks and physical play
  sessions. Separate automated results from manual observations and unresolved
  original-runtime comparisons. Automated coverage passes in preview.7; physical
  controller sessions remain unverified by agreement.
- [x] **P6.5** Build clearly named MonoGame desktop packages; test extracted
  assets, runtime/native dependencies and startup outside the checkout. Verify
  each claimed operating system separately and document signing/install behavior.
  Done for the sole packaged target: Apple Silicon macOS 26+ (tested 26.7.1),
  self-contained .NET 10.0.8, ad-hoc signed preview. See the 2026-10-07 evidence.
  Preview.7 freshly verifies 421 files, 15 native libraries, packaged persistence
  and normal startup; see the 2026-10-12 release report. Other operating systems,
  physical-controller play and notarization remain unclaimed.
- [x] **P6.6** Update user documentation and this plan with the actual supported
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
disables VSync for its manual `RunOneFrame` driver. Normal macOS startup now
uses the fixed 60 Hz game timer without VSync; other platforms are unchanged.
Use `--startup-check DIRECTORY` to exercise the normal game loop. See [core reproduction](Yipe.Tests/README.md) for the independent
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
| Source checkpoint | The baseline is committed at 0638981, followed by the mountain and deferred-effect checkpoints. UI/control/package work is currently uncommitted. | Preserve the working tree and its evidence; do not reset it to the baseline |
| Original diagonal movement | P1.3 restores eight-direction rules and preview input/animation. Diagonals use destination-only collision, horizontal-facing art and one 250 ms grid step. | P5.5: physical-controller validation and full-action/overlay coverage remain |
| Slower walking and timed gameplay | The approved pace can affect the practical difficulty of timed routes. | P3.5 passed timed platform/fire routes at 250 ms; retain this regression coverage |
| Save compatibility | `.y3m` v1 and explicit preview/C++ v20/original imports are complete. Original unknown/absent fields and selected Windows-1252 decoding are documented; earlier C++ versions require upgrading in the rebuild. | Keep formats/source files distinct; extend supported variants only with reference evidence. See `SAVE_FORMAT.md`. |
| Inherited adaptations | C++ D7 restores platform support on reopen; D8 sets new-character stat cap 50 and weapon upgrade ceiling 29. Other fidelity limits remain documented. | Dispositions are recorded in FEATURE_DISPOSITIONS.md; retain original-runtime limits |
| Menu coverage | Every original menu/system row is mapped in FEATURE_DISPOSITIONS.md; legacy desktop conveniences explicitly excluded by the user on 2026-10-12. | P5.6 closed; verify retained menus and save lifecycle under P5.3/P6.3 |
| Current CI and release labels | Root hosted CI/releases still target C++/SDL. A distinct local MonoGame macOS package now passes extraction/signature/startup/save checks. | P0.3: add hosted MonoGame coverage; keep artifacts separate |

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

### UI/control completion and first macOS package — 2026-10-07

Completed the existing draft and restored the public GameInput API so the solution
builds again. Controls now share persisted, validated gameplay bindings, with
fixed menu/text navigation, conflict rejection, cancellation, reset and visible
write-failure handling. Help reflects those bindings; all 28 original manual
topics, current profile policy and recovered About attribution are reachable.
The bitmap font now supports the manual/contact punctuation.

The first standalone MonoGame preview contains the official self-contained .NET
10.0.8 runtime, SDL/OpenAL, retro assets/audio and dependency notices. Packaging
uses official NuGet runtime packs, rather than shipping Homebrew's runtime with
its external Brotli dependency. The signed app keeps managed data in Resources.
Extraction verifies every byte, native dependencies/signatures, empty-directory
startup, macOS LaunchServices and save/RNG continuation against packaged binaries.

[Evidence and reproduction](evidence/2026-10-07-ui-package/README.md). P6.5 is
complete for the sole macOS arm64 preview target. P5.5/P5.6 remain open for
physical-controller verification and the separately listed desktop conveniences;
the initial draft's checked boxes were premature. No hosted release or other
platform is claimed, and no personal save/profile was modified.

### Remove obsolete commercial language — 2026-10-07

The user clarified that this port is not shareware and has no registration system.
Removed the seven original commercial/hintbook/distribution topics from the shipped
guide, retained its 21 gameplay topics, and replaced the registration explanation
with Stats and saves. Original reverse-engineering evidence remains historical;
it does not establish the port's distribution policy. Updated package preview.4
and its verification to require the gameplay guide without old commercial text.

Verification: zero-warning/error Release build, all 1,239 GPU checks, and
preview.4 extraction/signature/dependency/selected-save/LaunchServices checks pass.
[Guide-update evidence](evidence/2026-10-07-gameplay-guide/README.md).

### icosium labs pixel-art studio ident — 2026-10-07

Added a Casbah-inspired pixel-art icosium labs logo before the normal title menu,
with white terraced houses, an arched passage, ochre edges and Mediterranean blue.
The transparent logo is generated artwork; exact prompts and provenance are in
`Yipe.Desktop/Content/Branding/`. Its lettering is ivory for dark-background
readability. It renders on the native 320×180 canvas with point sampling and
shared whole-number enlargement, without world effects. The 3.2-second ident
fades in/out, pauses its timer on focus loss and accepts standard Confirm/Back/
Pause skip inputs without triggering title actions. Direct save and smoke startup
continue to bypass the title sequence. About credits the port/remaster to
icosium labs while preserving original credits.

Checked Release: zero warnings/errors, 558 gameplay/input checks, 1,246 GPU
checks and a visually inspected splash capture. Updated macOS preview.5 includes
the brand PNG and company attribution. No adventure/profile format or gameplay
change; the running personal game is not restarted.

### Simplify studio splash caption — 2026-10-07

Removed the PORT / REMASTER BY heading from the opening ident at the user's
request. The logo and skip hint remain. About and bundle attribution name
icosium labs without that label. Updated macOS preview.6.
