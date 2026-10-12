# MonoGame feature dispositions

This maps every menu and system row in the C++ [coverage inventory](../docs/FEATURE_COVERAGE.md)
to the port. **P1–P4 Done** means the gameplay is implemented and verified within
the linked [completion evidence](evidence/2026-10-04-p234-complete/README.md).
**P5/P6 open** identifies remaining presentation, platform, save-lifecycle or
release work; it is not a claim that the corresponding original UI exists here.

On 2026-10-12 the user excluded legacy desktop conveniences from the remaster:
interactive Save As, native file browsers, separate recent-file menus, printing,
and desktop text/clipboard commands. These are intentional omissions, not release
blockers. Existing in-game save/load, text entry and CLI imports remain available.

## Original menu commands

| Original command | MonoGame behavior / remaining task |
| --- | --- |
| File / New | Creation and new-adventure confirmation overlays, twelve appearances, difficulty, specialty and Silly Mode; P2 Done. Cancelling restores the prior interaction. |
| File / Open | Startup/pause Load and explicit `--load-save`; validated `.y3m`, with separate import CLI. Native chooser intentionally omitted. Review unsaved-progress behavior under P5.3/P6.3. |
| File / Save | Atomic full-state pause-menu save with failure recovery; P1/P2 Done. |
| File / Save As | Interactive Save As intentionally omitted. Existing CLI path selection remains available. |
| File / Print, Print Preview, Print Setup | Printing and PDF export intentionally omitted. |
| File / Recent File | Separate recent-file menu intentionally omitted; use the in-game Load Game adventure list. |
| File / Exit | Pause-menu Quit and window close; in-flight saves finish. Save-and-quit/unsaved confirmation review remains under P5.3/P6.3. |
| Edit / Undo | No gameplay undo in either implementation. Desktop text undo intentionally omitted. |
| Edit / Cut, Copy, Paste | Keyboard/controller text entry and deletion work; desktop selection, clipboard commands and edit popup intentionally omitted. |
| View / Stats | Character/progress overlay includes numeric stats, inventory count, ability, special items and deaths; P4.7 Done. |
| View / Items | Inventory overlay, all consumables, use/discard/back; P4.7 Done. |
| View / Cast Spell | All learned spells, power, costs, restrictions and combat/exploration effects; P4.2 Done. |
| View / Equipment | All seven slots, names and original AC/WC ratings; P4.7 Done. |
| View / Read Sign | Arrival text and Actions → Read sign; P3 Done. |
| View / High Scores | Three difficulty tables, 45 records, persistent profile and failure handling; P4.7 Done. |
| View / High Score Code | Original code and suffix displayed; P4.7 Done. Clipboard copy intentionally omitted. Historical email submission is not an active game service. |
| View / Serial Number | Omitted from this port. Help → Stats and saves shows the current stat limit and save information. |
| View / Status Bar | Permanent sidebar/status panes replaced by requested 16:9 play and modal status/actions. Historical lock-key indicators are not part of the game HUD. P5.6 records this presentation adaptation. |
| Options / Register | Omitted from this port; no registration UI or commercial manual pages. New-character stat caps follow the existing D8 policy. |
| Options / Edit Keyboard Commands | Seven gameplay keyboard and four controller shortcuts can be rebound, with conflict rejection, reset, atomic persistence and safe fixed menu controls. Automated verification passes; physical-controller P5.5 work remains. |
| Options / Toggle Silly/Normal Mode | Creation and Character-menu toggle, original weapon/armor names and persisted mode; P4.7 Done. |
| Options / Sound | Original effects/monster aliases, volume/toggle, profile persistence and interruption; P2/P4 Done. Cross-platform/audio-timing checks remain P5.4/P6. |
| Help / About Quest of Yipe! | A Casbah-inspired pixel-art icosium labs ident precedes the title. About credits icosium labs for the port/remaster alongside recovered Kinell/Kelley/Johnson attribution. UI navigation and rendering verified. |
| Help / Help and Info | In-game current-binding help, 21 gameplay guide topics and stat/save information are implemented and verified. Old commercial pages are excluded; current controls are presented separately. |
| Framework next/previous pane, preview toolbar | Modal navigation/back replaces permanent panes; native printing controls intentionally omitted. Historical MFC accelerators are not claimed as mapped. |

## Gameplay and integration systems

| Inventory system row | Implementation / remaining verification |
| --- | --- |
| Startup and character creation | P2 Done; four specialties, seeded rerolls, name/difficulty, eight Classic/four Silly appearances, King introduction. |
| Player defaults, stats, level growth | P1/P4 Done; original RNG/formulas and D8 caps. One earned Combat/Medium/1 completion; three other completion fixtures remain P6.1. |
| Inventory | P1/P4 Done; all 30 stored slots preserved, 13 carried slots exposed, ten consumables and original cleanup/relocation semantics. |
| Maps and movement | P3 Done; 112 static/three generated maps, all transitions/gates/visibility/timers. 115-map render audit and earned platform/fire/fall/strength routes. |
| Area/world map (requested addition) | Full-screen M/View overlay; current-region artwork and player/landmark markers, visited-world overview and traveled links, exploration shading, saved companion journal. No gameplay-rule or runtime visibility changes. |
| Encounters | P3/P4 Done; 186 placements, 22 pursuers, roaming, grouped combat, respawns and all 41 monsters. Independent state/RNG comparisons. |
| Equipment and shops | P4.3 Done; complete catalogs, services, stock/price progression, Quizilog reward and purchased Rainbow Key. |
| Treasure chests and movement-item acquisition | P3.4 Done; all 145 placements, full-inventory/reward ordering and persistence compared. Spray and other prerequisites earned in the campaign. |
| Healing, magic, abilities | P4 Done; services, twelve spells, four abilities and imported state. |
| Casino | P4.4 Done; three games, original wager/aim/payout rules and live overlays. Visual timing is adapted. |
| King, princesses, stamps | P4.5 Done; all twenty rescues, six stamps, King branches and earned live finale. |
| Special locations and recovery | P3/P4.6 Done; Other, tree code, moving platforms, falls/fire, Hell, Gatekeeper fee/fight/key and recovery. Six deaths retained in the earned run. |
| Statistics and high scores | P4.7 Done; unknown monster names stay hidden until a recorded kill; incomplete imported history is labeled; separate persistent score tables and original codes. |
| Help and attribution | Current controls, gameplay guide, recovered About credits and stat/save information implemented/verified. P5.6 inventory closed with the user's explicit legacy-feature exclusions. |
| Native persistence | P1 Done; versioned `.y3m`, atomic writes, full state, static/generated reopen, early/late fresh processes. Wider OS lifecycle testing remains P5/P6; desktop chooser/history intentionally omitted. |
| Original-file import | P1 Done within SAVE_FORMAT.md policy: explicit, source-preserving import of C++ v20 and mapped original archives. Unsupported versions/unknown original fields remain explicit. |
| Input/audio/presentation | Gameplay controls, all required art/overlays/audio implemented; synthetic input and real graphics/audio tested. Remapping and Help/About rendering at 640×360, 1024×768 and 1920×1080 are verified. Physical controllers, wider display/accessibility and performance review remain P5/P6. |
| Reproducible release | Standalone self-contained Apple Silicon macOS preview built and extracted: hashes, native signatures/dependencies, LaunchServices startup and packaged save/RNG restarts verified. [Package procedure](packaging/README.md). Hosted MonoGame CI, other OS claims, physical-controller play and official signing/notarization remain open. C++ packages do not contain this port. |

## Inherited and port-specific adaptations

[Reference decisions D1–D8](../docs/DECISIONS.md) remain bounded evidence, especially:

- **D7:** loading a saved platform rider restores support, but resets transient
  platform direction/timer phase; no timed map snapshot is serialized.
- **D8:** new characters and original imports use mode 2 (stat cap 50, random
  weapon-upgrade ceiling 29). Imported native modes retain their own limits.
  This does not recover an original registration profile or change the strength gate.
- Original behavioral quirks and RNG order remain unless explicitly documented
  in the reference. C++ equality does not close its original-runtime unknowns.
- C#/MonoGame replaces the reference's C++/SDL build choice. The user requested
  full-screen 16:9 play, overlay menus, retro artwork, 250 ms movement, four-Hz
  walking, scanlines and the Strong miniature effect. Original UI layout and
  animation timing are therefore deliberately adapted while rule timing stays separate.
- Area/world map discovery is an approved quality-of-life addition, persisted in
  a matching `.map.json` companion without changing version 1 gameplay saves.
- `.y3m` and JSON preferences are separate port formats; import never writes
  into original or C++ saves. Display/audio settings are separate from adventure state.

P5.6 is closed by the implemented Help/About/control work and the user's explicit
legacy-feature exclusions. Presentation, input, save-lifecycle and release
verification remain under their P5/P6 tasks; console readiness remains separate.
