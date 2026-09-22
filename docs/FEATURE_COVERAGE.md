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
| File / New | 57600 | Startup N creates a character. In-game Ctrl+N and the full character-creation flow remain unresolved. |
| File / Open | 57601 | Startup L resumes the single native save. File chooser, in-game opening, and UI legacy import remain unresolved. Ctrl+O is currently adapted to Other. |
| File / Save | 57603 | Implemented through F5 and Ctrl+S, using atomic native persistence. |
| File / Save As | 57604 | Unresolved; currently one per-user native save slot. |
| File / Print | 57607 | Unresolved; determine whether this MFC menu entry has meaningful game behavior. |
| File / Print Preview | 57609 | Unresolved; determine whether the original supplies game-specific output. |
| File / Print Setup | 57606 | Unresolved Windows printing-framework behavior. |
| File / Recent File | 57616 | Unresolved; current Load resumes one native file. |
| File / Exit | 57665 | Adapted to Q/Escape with save choice; window close saves and exits. |
| Edit / Undo | 57643 | Unresolved; distinguish framework defaults from gameplay handlers. |
| Edit / Cut | 57635 | Unresolved; distinguish text-edit controls from gameplay handlers. |
| Edit / Copy | 57634 | Unresolved; distinguish text-edit controls from gameplay handlers. |
| Edit / Paste | 57637 | Unresolved; distinguish text-edit controls from gameplay handlers. |
| View / Stats | 32778 | Ctrl+T, with Kills, Equipment, Special, and High Scores. Main numeric fields/AC/WC implemented; character-image presentation remains open. |
| View / Items | 32779 | Ctrl+I or the configured Use Item key opens carried consumables. |
| View / Cast Spell | 32780 | Ctrl+C or the configured Cast key opens the spell picker. |
| View / Equipment | 32783 | Ctrl+E or Ctrl+T → Equipment; all seven slots and per-item ratings. |
| View / Read Sign | 32776 | Signs display on arrival. Explicit Ctrl+R rereading and exact original handler behavior remain unresolved. |
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
| Startup and character creation | Startup art and three class choices implemented. Original name entry, random/reroll creation, subsequent setup choices, class art, and in-game new-game flow need an explicit audit. |
| Player defaults, stats, level growth | Core implementation and deterministic cases; class/difficulty/runtime reference comparisons remain open. |
| Inventory | Ten consumables, 13 carried slots, stat/healing effects, and relocation implemented. Other serialized slots and invalid-item cleanup remain U004. |
| Maps and movement | 112 static and three generated maps, transitions, visibility, movement hazards/gates, and entry patches implemented. Exhaustive event/route and original map comparisons remain open. |
| Encounters | All 186 placements, 41 monster definitions, grouped combat, repeat/large-footprint behavior, roaming and pursuers implemented. Exact attack/random-order fidelity remains P6 work. |
| Equipment and shops | Recovered weapon/armor catalogs, miscellaneous stock, selling/buying, combat bonuses, names, and Equipment statistics implemented. Exhaustive stock/reward coverage and original transactions remain open. |
| Healing, magic, abilities | Services/training, 12 spells and four abilities implemented. Legacy active-effect import and original-runtime comparisons remain open. |
| Casino | Slots, dot racing, and darts implemented with recovered rules. Original UI/timing comparison remains open. |
| King, princesses, stamps | King selector/rewards, all 20 princesses, six stamp bosses, shared signed rescue count, and finale logic implemented. Normal-play reachability/completion remains unverified. |
| Special locations and recovery | Secret tree, Other tile actions, location messages/hints, death/Hell/Gatekeeper recovery implemented. Remaining map event types and complete progression need audit. |
| Statistics and high scores | Kills, Special, Equipment, AC/WC, and 45 high scores implemented. Main portrait/presentation and high-score code remain unresolved. |
| Help and attribution | All original manual content recovered; current controls and original text are separated. Visible native-dialog and original-navigation comparisons remain open. Historical manual claims are not treated as current implementation evidence. |
| Native persistence | Player/world versions 1–15, settings, and high scores supported. Failure/restart coverage and early/middle/late/completion checkpoint verification remain open. |
| Original-file import | Strict import-only `.yp3`/`Yipe3.dat` readers with synthetic/captured auxiliary fixtures. Real player-save samples and user-facing import remain open. |
| Input/audio/presentation | Configured movement/combat keys, recovered audio, SDL combat picker, and native dialogs implemented. Mouse exploration, modifier bindings, shortcut conflicts, focus/repeat/scaling/timing and long-session comparisons remain open. |
| Reproducible release | Development CMake/CTest build works. Clean release build, packaged macOS app, independent-directory launch, complete playthrough, and final artifact/limitations documentation remain P9. |

Use this inventory to select missing functionality, then update its row and the
evidence ledger when implemented. Broad implementation rows must stay open
until their remaining coverage and verification requirements are satisfied.
