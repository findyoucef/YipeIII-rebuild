# MonoGame feature dispositions

This maps every menu and system row in the C++ [coverage inventory](../docs/FEATURE_COVERAGE.md)
to the port. **P1–P4 Done** means the gameplay is implemented and verified within
the linked [completion evidence](evidence/2026-10-04-p234-complete/README.md).
**P5/P6 open** identifies remaining desktop convenience, presentation, platform or
release work; it is not a claim that the corresponding original UI exists here.

## Original menu commands

| Original command | MonoGame behavior / remaining task |
| --- | --- |
| File / New | Creation and new-adventure confirmation overlays, twelve appearances, difficulty, specialty and Silly Mode; P2 Done. Cancelling restores the prior interaction. |
| File / Open | Startup/pause Load and explicit `--load-save`; validated `.y3m`, with separate import CLI. Native chooser and unsaved-change confirmation remain P5.6. |
| File / Save | Atomic full-state pause-menu save with failure recovery; P1/P2 Done. |
| File / Save As | Select a path using `--load-save`; interactive Save As remains P5.6. |
| File / Print, Print Preview, Print Setup | No printing UI. Desktop print/PDF disposition and implementation remain P5.6; no console dependency. |
| File / Recent File | No recent-file list; P5.6. |
| File / Exit | Pause-menu Quit and window close; in-flight saves finish. Save-and-quit/unsaved confirmation remains P5.6. |
| Edit / Undo | No gameplay undo in either implementation. Text undo remains P5.6. |
| Edit / Cut, Copy, Paste | Keyboard/controller text entry and deletion work; selection/clipboard/edit popup remain P5.6. |
| View / Stats | Character/progress overlay includes numeric stats, inventory count, ability, special items and deaths; P4.7 Done. |
| View / Items | Inventory overlay, all consumables, use/discard/back; P4.7 Done. |
| View / Cast Spell | All learned spells, power, costs, restrictions and combat/exploration effects; P4.2 Done. |
| View / Equipment | All seven slots, names and original AC/WC ratings; P4.7 Done. |
| View / Read Sign | Arrival text and Actions → Read sign; P3 Done. |
| View / High Scores | Three difficulty tables, 45 records, persistent profile and failure handling; P4.7 Done. |
| View / High Score Code | Original code and suffix displayed; P4.7 Done. Clipboard copy remains P5.6. Historical email submission is not an active game service. |
| View / Serial Number | No original serial is invented or imported. Profile/registration information screen remains P5.6. |
| View / Status Bar | Permanent sidebar/status panes replaced by requested 16:9 play and modal status/actions. Historical lock-key indicators are not part of the game HUD. P5.6 records this presentation adaptation. |
| Options / Register | Inherited D8 cap policy; no registration transaction. Original historical registration information belongs with the P5.6 manual/profile screen. |
| Options / Edit Keyboard Commands | Fixed keyboard, D-pad and stick controls work. Rebinding and conflict UI remain P5.4/P5.5. |
| Options / Toggle Silly/Normal Mode | Creation and Character-menu toggle, original weapon/armor names and persisted mode; P4.7 Done. |
| Options / Sound | Original effects/monster aliases, volume/toggle, profile persistence and interruption; P2/P4 Done. Cross-platform/audio-timing checks remain P5.4/P6. |
| Help / About Quest of Yipe! | Repository attribution retained; in-game About screen remains P5.6. |
| Help / Help and Info | Original recovered manual remains in reference assets/docs; in-game original manual and current-control help remain P5.6. |
| Framework next/previous pane, preview toolbar | Modal navigation/back replaces permanent panes; native printing controls remain with P5.6. Historical MFC accelerators are not claimed as mapped. |

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
| Help and attribution | P5.6 open; see menu dispositions above. |
| Native persistence | P1 Done; versioned `.y3m`, atomic writes, full state, static/generated reopen, early/late fresh processes. Desktop chooser/history and wider OS lifecycle testing remain P5/P6. |
| Original-file import | P1 Done within SAVE_FORMAT.md policy: explicit, source-preserving import of C++ v20 and mapped original archives. Unsupported versions/unknown original fields remain explicit. |
| Input/audio/presentation | Gameplay controls, all required art/overlays/audio implemented; synthetic input and real graphics/audio tested. Physical controllers, remapping, display-size polish, accessibility and performance review remain P5/P6. |
| Reproducible release | Development run/build works on this Mac. Distinct MonoGame CI/packages, extraction/signing/runtime checks and other OS claims remain P0.3/P6.5. C++ packages do not contain this port. |

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

P5.6 closes the remaining convenience work after reviewing this list. P2–P4
completion does not silently remove those tasks or assert console readiness.
