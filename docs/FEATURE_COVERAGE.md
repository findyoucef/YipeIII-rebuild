# Feature and menu coverage

Inventory checked against `assets/embedded/ui-catalog.json`, resource menu 128,
the current source, and the evidence ledger. “Implemented” means source and
automated/static evidence exist; it does **not** mean original-runtime parity.
R01–R10 and an end-to-end playthrough remain open. No unresolved feature below
is implicitly excluded from the rebuild scope.

## Original menu actions

All 28 nonseparator entries are accounted for. The SDL interface adapts menu
access to dialogs and shortcuts; it does not yet reproduce a native menu bar.

| Original action | Command ID | Current behavior / remaining work |
| --- | --- | --- |
| File / New | 57600 | Startup N and in-game Ctrl+N offer four stat choices, random reroll, name, difficulty, and portrait selection. Switching from play asks whether to save; the new game's first save opens Save As. The original intro toggle and native-dialog comparison remain unresolved. |
| File / Open | 57601 | Startup L and in-game Ctrl+O use the macOS file chooser for native `.y3r` saves or import-only original `.yp3` files. In-game Open validates the selected world before leaving current play, prevents saving over the target, and offers a save choice for different files. Real original-save compatibility and dialog comparison remain open. |
| File / Save | 57603 | F5 and Ctrl+S use atomic native persistence. The first save of a new game asks for a `.y3r` path. |
| File / Save As | 57604 | Ctrl+Shift+S opens the macOS native save chooser. Subsequent saves use the selected `.y3r` path; original `.yp3` files cannot be overwritten. Native-menu and original-runtime comparison remain open. |
| File / Print | 57607 | Unresolved; determine whether this MFC menu entry has meaningful game behavior. |
| File / Print Preview | 57609 | Unresolved; determine whether the original supplies game-specific output. |
| File / Print Setup | 57606 | Unresolved Windows printing-framework behavior. |
| File / Recent File | 57616 | Unresolved; Open uses a file chooser, with no recent-file list. |
| File / Exit | 57665 | Adapted to Q/Escape with save choice; window close saves and exits. |
| Edit / Undo | 57643 | Unresolved; distinguish framework defaults from gameplay handlers. |
| Edit / Cut | 57635 | Unresolved; distinguish text-edit controls from gameplay handlers. |
| Edit / Copy | 57634 | Unresolved; distinguish text-edit controls from gameplay handlers. |
| Edit / Paste | 57637 | Unresolved; distinguish text-edit controls from gameplay handlers. |
| View / Stats | 32778 | Ctrl+T, with Kills, Equipment, Special, and High Scores. Main numeric fields/AC/WC implemented; character-image presentation remains open. |
| View / Items | 32779 | Ctrl+I or the configured Use Item key opens carried consumables. |
| View / Cast Spell | 32780 | Ctrl+C or the configured Cast key opens the spell picker. |
| View / Equipment | 32783 | Ctrl+E or Ctrl+T → Equipment; all seven slots and per-item ratings. |
| View / Read Sign | 32776 | Signs display on arrival. Ctrl+R rereads the current tile's extracted sign text; the original handler and visible presentation still need runtime comparison. |
| View / High Scores | 32771 | Startup H, Ctrl+H, and the status dialog; all 45 records and native persistence. |
| View / High Score Code | 32785 | Unresolved; retain separately from the implemented local score tables. |
| View / Serial Number | 32781 | Unresolved registration/profile policy (U013). |
| View / Status Bar | 59393 | Unresolved; native MFC status-bar presentation is not reproduced. |
| Options / Register | 32774 | Unresolved registration/profile policy (U013). No access policy changes. |
| Options / Edit Keyboard Commands | 32772 | F10 → Movement/Combat Keys; all 13 keys editable and persisted. Modifier combinations and conflicts with fixed shortcuts remain open. |
| Options / Toggle Silly/Normal Mode | 32784 | Unresolved; trace its data, messages, and persistence before implementing. |
| Options / Sound | 32773 | F10 → Toggle Sound; supplied audio/aliases and persistent preference implemented. Timing comparisons remain open. |
| Help / About Quest of Yipe! | 57664 | F1 → About preserves original attribution/version/contact text and identifies the reconstruction. |
| Help / Help and Info | 32775 | F1 → Original Manual contains all 28 original pages, with Previous/Next/Start/End navigation. Character creation Help starts on page 4. |

The original accelerator table also contains framework next/previous-pane and
print-preview commands. Their applicability is unresolved alongside the native
menu/status-bar/printing work. F1 is an adapted help shortcut at startup and
during exploration; it takes priority over an exploration binding to F1.
Combat retains configured F1 behavior. Ctrl+I/C/E/S take priority over movement
bindings, and repeated shortcut keydowns are ignored.

## Major gameplay and integration systems

| Feature | Current evidence and gaps |
| --- | --- |
| Startup and character creation | Four recovered stat presets, seeded random reroll, name entry, three difficulties, eight normal/four silly portraits, and saved specialty/portrait are implemented. In-game New routes to setup after a save choice. Original intro toggle and original-runtime/dialog comparisons remain open. |
| Player defaults, stats, level growth | Core implementation and deterministic cases; class/difficulty/runtime reference comparisons remain open. |
| Inventory | Ten consumables, 13 carried slots, stat/healing effects, and relocation implemented. Other serialized slots and invalid-item cleanup remain U004. |
| Maps and movement | 112 static and three generated maps, transitions, visibility, movement hazards/gates, and entry patches implemented. Exhaustive event/route and original map comparisons remain open. |
| Encounters | All 186 placements, 41 monster definitions, grouped combat, repeat/large-footprint behavior, roaming and pursuers implemented. Exact attack/random-order fidelity remains P6 work. |
| Equipment and shops | Recovered weapon/armor catalogs, miscellaneous stock, selling/buying, combat bonuses, names, and Equipment statistics implemented. Exhaustive stock/reward coverage and original transactions remain open. |
| Healing, magic, abilities | Services/training, 12 spells and four abilities implemented. Legacy active-effect import and original-runtime comparisons remain open. |
| Casino | Slots, dot racing, and darts implemented with recovered rules. Original UI/timing comparison remains open. |
| King, princesses, stamps | King selector/rewards, all 20 princesses, six stamp bosses, shared signed rescue count, and finale logic implemented. Normal-play reachability/completion remains unverified. |
| Special locations and recovery | Secret tree, Other tile actions, location messages/hints, sign rereading, overlay drawing/relocation, and death/Hell/Gatekeeper recovery implemented. Overlay runtime timing and complete progression need audit. |
| Statistics and high scores | Kills, Special, Equipment, AC/WC, and 45 high scores implemented. Main portrait/presentation and high-score code remain unresolved. |
| Help and attribution | All original manual content recovered; current controls and original text are separated. Visible native-dialog and original-navigation comparisons remain open. Historical manual claims are not treated as current implementation evidence. |
| Native persistence | Player/world versions 1–16, settings, and high scores supported. Version 16 separates specialty from stat bias; older native versions infer the former class. New/Open/Save As use selected paths on macOS. Failure/restart coverage and early/middle/late/completion checkpoint verification remain open. |
| Original-file import | Strict import-only `.yp3`/`Yipe3.dat` readers with synthetic/captured auxiliary fixtures. File/Open now exposes `.yp3` import and requires a separate native save path; real original player-save samples and runtime compatibility remain open. |
| Input/audio/presentation | Configured movement/combat keys, recovered audio, SDL combat picker, and native dialogs implemented. Ctrl+N/O restore the original New/Open accelerators; adapted Other is Ctrl+Shift+O. Mouse exploration, modifier bindings, shortcut conflicts, focus/repeat/scaling/timing and long-session comparisons remain open. |
| Reproducible release | Development CMake/CTest build works. Clean release build, packaged macOS app, independent-directory launch, complete playthrough, and final artifact/limitations documentation remain P9. |

Use this inventory to select missing functionality, then update its row and the
evidence ledger when implemented. Broad implementation rows must stay open
until their remaining coverage and verification requirements are satisfied.
