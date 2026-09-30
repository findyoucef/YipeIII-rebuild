# Feature and menu coverage

Inventory checked against `assets/embedded/ui-catalog.json`, resource menu 128,
the current source, and the evidence ledger. “Implemented” means source and
automated/static evidence exist; it does **not** mean original-runtime parity.
Original-runtime R01–R10 remain open. E084 proves Combat/Medium/1,
Quickness/Easy/2, Magic/Hard/3 and Random/Medium/4 earned automated rebuild
completions on the E081 corrected combat build. Four separate fresh replays
exit 0 with 108,879 exact state/RNG observations and identical command, trace
and final-save bytes. The E074/E076/E078/E079 routes remain historical E077
fixtures. No unresolved feature below is excluded from the rebuild scope.
E082 separately verifies eighteen native restart cases and five continuations.
See [current completion evidence and limits](../evidence/corrected-completion/README.md)
and [bounded persistence evidence](../evidence/persistence-parity/README.md).

**Implementation priority, 2026-09-24:** finish missing features and resolve
documented differences from the original. E067 restores mouse exploration and
corrects mouse hit testing in scaled views. E068 adds the portrait statistics
panel with complete numeric fields and keyboard/mouse navigation. Existing
progression routes remain regression coverage; extend them for specific blockers
or final completion work.

E069 restores fixed/configured key precedence, adds punctuation/keypad bindings,
and explains conflicts before saving them. Original editor recovery establishes
single-key storage, resolving the earlier incorrect modifier-combination gap.

E070 restores High Score Code display and clipboard copying during play,
including the original conditional suffix and raw-field persistence. E071
implements macOS printing, integrated print preview, and page setup using the
current visible map and sprites. Native paper layout is a D2 adaptation.
E072 adds macOS Recent Games and the informational Profile / Registration view.
E073 adds text Edit commands, a toggleable status bar with the four recovered
areas, and F6/Shift+F6 navigation between the map and Player Status. All original
menu commands now have implemented behavior or an explicit D2/D8 adaptation.
E074 proves connected default Combat/Medium seed-1 completion with all rescues,
stamps, movement prerequisites, the empowered Gatekeeper, and the finale.
Original-runtime comparison remains separate open work.

The fresh-game [progression audit](PROGRESSION_AUDIT.md) (E061) found missing
prerequisite acquisition behind implemented gates. E062 restores the Underworld
Key, all 145 chests including waterproof spray, and Quizilog's climbing shoes.
E063 restores all four miscellaneous catalogs, their price milestone, and the
Outback Rainbow Key purchase. E064 adopts a 50-point stat limit for new characters
and legacy imports, preserves native saved limits, and demonstrates potion-earned
strength 40. E065-E066 add a continuous default-character route earning its resources
through 116 victories and 14 chests, rescuing slots 2 and 1, and earning Wormoid's
stamp at level 10 with no deaths. The second Mystery reward grants 300 gold;
E074 connects the Outback Rainbow Key purchase to full completion. Save/restart/import
verification sessions and user play are deferred.

## Original menu actions

All 28 nonseparator entries are accounted for. The SDL interface adapts menu
access to dialogs and shortcuts; it does not yet reproduce a native menu bar.

| Original action | Command ID | Current behavior / remaining work |
| --- | --- | --- |
| File / New | 57600 | Startup N and in-game Ctrl+N offer four stat choices, random reroll, name, difficulty, portrait selection, and the optional original King introduction. Switching from play asks whether to save; the new game's first save opens Save As. A macOS desktop run confirmed in-game cancel and first-save panel/file creation; original-runtime comparison remains open. |
| File / Open | 57601 | Startup L and in-game Ctrl+O use the macOS file chooser for native `.y3r` saves or import-only original `.yp3` files. In-game Open validates the selected world before leaving current play, prevents saving over the target, and offers a save choice for different files. A macOS desktop run confirmed cancel and same-file reopen confirmation; the real R08 archive now passes the headless import/save path (E060); visible import and other dialog paths remain open. |
| File / Save | 57603 | F5 and Ctrl+S use atomic native persistence; either Control key is covered by event-loop regression tests (E056). The first save of a new game asks for a `.y3r` path. |
| File / Save As | 57604 | Ctrl+Shift+S is wired to the macOS native save chooser. Subsequent saves use the selected `.y3r` path; original `.yp3` files cannot be overwritten. Either Control plus either Shift is now covered by event-loop tests, including cancellation and selected-path writes (E056). A focused desktop check, native-menu and original-runtime comparison remain open. |
| File / Print | 57607 | Ctrl+P captures the current logical game view and opens macOS Print / Preview, including printer choice and PDF saving. The 416-by-384 map, fog, and sprites are retained independently of window size; paper settings center/fit it on one page. Printing errors are surfaced, and panels pause exploration (E071/U027). |
| File / Print Preview | 57609 | Implemented through the macOS print panel's integrated page preview, opened by Ctrl+P. This deliberately replaces the original separate MFC preview toolbar. Native PDF generation, orientation/scale/fit, and panel cancellation have automated evidence; original presentation and physical printers remain unverified (E071/U027). |
| File / Print Setup | 57606 | Ctrl+Shift+P opens native Page Setup from startup or play. Paper/orientation/scale preferences are shared for the application session, accepted changes persist between print operations, and cancel preserves prior settings. Non-macOS support remains planned (E071/U027). |
| File / Recent File | 57616 | R at startup or Ctrl+L opens a paged full-path list backed by macOS recent documents. Accepted opens and successful saves are remembered; cancel/failure leaves history unchanged. Clear List deletes no saves. Native persistence/order/deduplication and SDL navigation, missing-file errors, same-file reopen, import selection, modifiers/repeats, and paused state are covered (E072/U028). Non-macOS history remains unsupported. |
| File / Exit | 57665 | Adapted to Q/Escape with save choice; window close saves and exits. |
| Edit / Undo | 57643 | Ctrl/Command+Z, Alt+Backspace, or text-entry F2/right-click → Undo / Restore toggles the last text edit. Original menu/accelerator entries have no recovered gameplay handler. There is no gameplay undo (E073/U029). |
| Edit / Cut | 57635 | Ctrl/Command+X, Shift+Delete, or the text-entry Edit popup copies the selection and removes it only after clipboard success. Empty selection and clipboard failure preserve text. Original Edit accelerators are consumed outside text entry, ahead of rebound movement (E073). |
| Edit / Copy | 57634 | Ctrl/Command+C, Ctrl+Insert, or the Edit popup copies selected name/code text. In exploration Ctrl+C retains its original Cast action. High Score Code has its separate Copy Code button (E070/E073). |
| Edit / Paste | 57637 | Ctrl/Command+V, Shift+Insert, or the Edit popup replaces the selection. Single-line/128-byte limits are enforced without destroying the selection; Unicode code points are preserved by cursor movement and deletion. The title displays selection and cursor; this presentation is adapted (E073/U029). |
| View / Stats | 32778 | Ctrl+T shows the selected portrait, all numeric fields, combined AC/WC, stat limit, and carried count. Keyboard/mouse buttons reach Kills, Equipment, Special, High Scores, and full native Details. All 12 portraits, three scales, navigation, redraws, quit forwarding, score submission, and exploration pause are covered. Layout and bitmap text are D2 adaptations; visible original comparison remains open (E068/U025). |
| View / Items | 32779 | Ctrl+I or the configured Use Item key opens carried consumables. |
| View / Cast Spell | 32780 | Ctrl+C or the configured Cast key opens the spell picker. |
| View / Equipment | 32783 | Ctrl+E or Ctrl+T → Equipment; all seven slots and per-item ratings. |
| View / Read Sign | 32776 | Signs display on arrival. Ctrl+R rereads the current tile's extracted sign text; the original handler and visible presentation still need runtime comparison. |
| View / High Scores | 32771 | Startup H, Ctrl+H, and the status dialog; all 45 records and native persistence. |
| View / High Score Code | 32785 | High Scores during play offers the original code and local clipboard copying, through Ctrl+H or statistics. All 50 counters, signed fields, quotient/remainder boundaries, and conditional suffix are retained. Native version 20 preserves raw `+0x6a4`; older incomplete history is explicitly labeled. Historical email service and original-runtime presentation remain unverified (E070/U026). |
| View / Serial Number | 32781 | F1 → Profile / Registration explicitly identifies that the rebuild has no original serial number and displays D8 plus the active character's stored limits. Original Windows profile values are not invented or imported (E072/U013). |
| View / Status Bar | 59393 | A default-visible SDL strip shows the current view plus CAP/NUM/SCRL. Ctrl+B at startup/play or F10 → Toggle Status Bar controls session visibility. Logical layout reserves space below the full map, ignores bar clicks for movement, and excludes it from printing. Lock indicators, geometry, input, unchanged state, and print pixels are tested (E073/U029). |
| Options / Register | 32774 | F1 → Profile / Registration → Original Registration opens the three historical 2003 manual pages. D8 replaces the original registration-dependent stat/loot policy; no code-entry machinery or profile mutation is reproduced. The adapted informational UI is covered at startup and during play (E072/U013). |
| Options / Edit Keyboard Commands | 32772 | F10 → Movement/Combat Keys edits all 13 single-key bindings, including punctuation/keypad operators. Conflicts with configured/fixed actions are displayed and require explicit acceptance; existing bindings are preserved. Original `EDIT` controls store raw key codes, not chords. Capture now explains unsupported modifiers and retries; fixed movement keys and combat duplicate precedence are restored (E069/U012). Original UI/layout comparisons remain open. |
| Options / Toggle Silly/Normal Mode | 32784 | F10 during play toggles the original 38 weapon/30 armor names and confirmations in shops and Equipment. Creation sets the mode and restricts silly portraits to four. Native version 17 retains mode separately from portrait/settings; older native and imported saves default to Normal. Persistence is an adaptation because the original omits the flag; its sound loader ignores the argument. Original-runtime/UI comparison remains open (E058/U023). |
| Options / Sound | 32773 | F10 → Toggle Sound; supplied audio/aliases and persistent preference implemented. Timing comparisons remain open. |
| Help / About Quest of Yipe! | 57664 | F1 → About preserves original attribution/version/contact text and identifies the reconstruction. |
| Help / Help and Info | 32775 | F1 → Original Manual contains all 28 original pages, with Previous/Next/Start/End navigation. Character creation Help starts on page 4. |

The original accelerator table also contains framework next/previous-pane and
print-preview-toolbar commands. macOS uses its integrated print preview. Under
D2, F6/Shift+F6 now switch between the map and the modal Player Status view;
this replaces focus changes between original persistent panes. The native MFC
menu bar is adapted to the documented shortcuts, dialogs, and text Edit popup.
Original layout/focus/timing comparisons remain open; no menu command is silently
excluded as a framework default.
F6 has priority over an exploration binding to F6; Settings reports the conflict.
F1 is an adapted help shortcut at startup and
during exploration; it takes priority over an exploration binding to F1.
Combat retains configured F1 behavior. Ctrl+I/C/E/S/P take priority over movement
bindings, and repeated shortcut keydowns are ignored.
E069 also ignores Alt/Command-modified gameplay events and repeated item/cast
dialog keys, while retaining movement repeat and the original single-key lookup.

## Major gameplay and integration systems

| Feature | Current evidence and gaps |
| --- | --- |
| Startup and character creation | Four recovered stat presets, seeded random reroll, name entry, three difficulties, eight normal/four silly portraits, saved specialty/portrait, and an optional original-text King introduction are implemented. In-game New routes to setup after a save choice. The startup N shortcut no longer leaks into character-name input. Original-runtime/dialog comparisons remain open. |
| Player defaults, stats, level growth | New characters and legacy imports use mode 2 (cap 50; random weapon upgrades through rank 29) under D8. Native saves retain modes 0/1/2 and their 30/40/50 limits. Creation/status display the stat limit. Core cases cover all stat/loot boundaries and earn strength 40 through funded purchases before the linked route (E064/U013/U024). E065-E066 grow a default Combat/Medium character from level 1 to 10 using earned resources through two rescues and the first stamp. E074 extends this same fresh default character to level 42, all rescues/stamps, and the finale with earned resources. Other seeds/classes and original-runtime comparisons remain open. |
| Inventory | Ten consumables, 13 carried slots, stat/healing effects, and relocation implemented. Other serialized slots and invalid-item cleanup remain U004. |
| Maps and movement | 112 static and three generated maps, transitions, visibility, movement hazards/gates, and entry patches implemented. A core route verifies the strength/linked/rainbow-key path from map `0x2d` to princess slot 13 on `0x2e`. Recovered timers provide the `0x4a` moving-platform and `0x85` fire-wall princess routes. Core/SDL event-loop cases cover entry, rescue, exit, and persistence (E059). Prerequisite acquisition, encounters, visible play, and original route comparisons remain open (U022). |
| Encounters | All 186 placements, 41 monster definitions, grouped combat, repeat/large-footprint behavior, roaming and pursuers implemented. Exact attack/random-order fidelity remains P6 work. |
| Equipment and shops | Weapon/armor catalogs and all four miscellaneous catalogs implemented, including all 44 stock rows, the stat-potion price increase, Outback's level-26 Rainbow Key offer, exact-gold purchases, inventory limits, and accessory upgrades (E063). Quizilog remains blocked until its four groups are cleared, then awards shoes before either shop (E062). E065-E066 connect ordinary purchases of food, accessories, weapon/armor, spells, and 150-gold Backstab training to earned income and two rescues. Remote-shop funding and original transaction/UI comparisons remain open. |
| Treasure chests and movement-item acquisition | All 145 placements on 75 map IDs, including two on generated `0x78`, are cataloged and rendered. One-time collection grants recovered gold/XP and shared equipment/consumable loot; map `0x63` slot 1 `(9,1)` grants waterproof spray. Opened flags survive native persistence and legacy import. Acquisition, rendering, RNG, inventory limits, and re-entry have automated coverage (E062/U024). E065-E066 collect 14 chests in the connected route and assert their exact flags; movement-item acquisition and the remaining chests still need connected coverage. Original-runtime comparisons remain open. |
| Healing, magic, abilities | Services/training, 12 spells and four abilities implemented. Legacy active-effect import and original-runtime comparisons remain open. |
| Casino | Slots, dot racing, and darts implemented with recovered rules. Original UI/timing comparison remains open. |
| King, princesses, stamps | King selector/rewards, all 20 princess placements, six stamp bosses, shared signed rescue count, and finale predicate implemented. The first type-41 victory now awards the Underworld Key before advancement; first-key and matching-stamp rewards replace equipment loot (E062). E065-E066 earn two rescues (slots 2 then 1) and Wormoid's stamp in a continuous seed-1 SDL run. Mystery grants healing then 300 gold; its counter reaches 2 without a Rainbow Key. E074 proves all 20 rescues, six stamps, earned prerequisites, Gatekeeper states 1→2→3, and the live town-entry finale in one connected run. |
| Special locations and recovery | Secret tree, Other tile actions, location messages/hints, sign rereading, overlay drawing/relocation, and death/Hell/Gatekeeper recovery implemented. E074 exercises the moving raft chain, random raft landing, timed fall/fire passage, strength switch, secret-tree healing ring, and ordinary death recovery in the connected completion run. Original-runtime timing comparison remains open. |
| Statistics and high scores | Portrait statistics, all numeric fields, Kills, Special, Equipment, AC/WC, and 45 high scores are implemented. The main view supports keyboard/mouse navigation and native Details for complete names/text (E068). High-score code is implemented (E070). Write failures now report an error and preserve the previous score table while play continues; failure and retry have automated coverage. Visible original comparisons remain open. |
| Help and attribution | All original manual content recovered; current controls and original text are separated. Visible native-dialog and original-navigation comparisons remain open. Historical manual claims are not treated as current implementation evidence. |
| Native persistence | Player/world versions 1–20, settings, and high scores supported. Versions 16/17 separate specialty and Silly Mode; version 18 appends chest bits; version 19 appends the miscellaneous-shop stage; version 20 appends the High Score Code suffix field. Earlier files retain their fields and default the newly added state. New/Open/Save As use selected paths on macOS. Automated write-failure recovery covers Save/Save As, first save, Save and Leave for Quit/New/Open, window close, unchanged files, and retry. Broader failure/restart coverage and manual checkpoint verification remain open. |
| Original-file import | Strict import-only `.yp3`/`Yipe3.dat` readers. Four original-produced R08 saves pass core import/native conversion; the SDL Open/save path also accepts a real capture and leaves it unchanged (E060). Visible file-dialog import, later progression, unmapped legacy state, and other original versions remain open. |
| Input/audio/presentation | Configured movement/combat keys, recovered audio, SDL combat picker, and native dialogs implemented. Ctrl+N/O restore the original New/Open accelerators; adapted Other is Ctrl+Shift+O. E067 restores one step per left-button release and correct scaled coordinates across mouse views. E069 restores fixed movement keys and original duplicate precedence, adds punctuation/operators, and reports conflicts and unsupported chords without changing settings format. SDL cases cover three scales, mouse/keyboard equivalence, all edited actions, conflict confirmation, system modifiers, and repeats. Original focus/target/timing comparisons, other keyboard layouts, audio timing, and long-session behavior remain open. |
| Reproducible release | A clean macOS Release checkout passes all three CTest suites. CPack creates an ad-hoc-signed `.app` with bundled SDL2, its license, and 137 runtime assets plus README/docs. Extracted-package validation confirms relative dependencies, signatures, and bundled-SDL startup from an empty working directory (E057). Interactive independent-directory launch, other machines, writable-path checks, and wider release verification remain P9. E074 proves one automated connected playthrough and refreshes the development package; extracted-package dependency, signature, asset, and empty-directory startup checks pass. E075 resolves the environment failure after sandbox removal: all seven Release tests, including native recent-file write/read/clear, pass. E077 restores the original generated-room clamp and passes original-grid comparisons and sanitizers; a separate clean Release build passes all seven tests in 44.80 seconds. E080 packages that correction and the completed four-fixture matrix, with independent extraction/dependency/signature/137-asset/empty-directory startup checks. The recorded build is arm64/macOS 26.0 minimum, without Developer ID signing or notarization. |

Use this inventory to select missing functionality, then update its row and the
evidence ledger when implemented. Broad implementation rows must stay open
until their remaining coverage and verification requirements are satisfied.
