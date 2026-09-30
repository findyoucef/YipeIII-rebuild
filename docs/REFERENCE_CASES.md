# Original-runtime reference cases

The original reaches its rendered main menu in a fresh Whisky bottle. The interrupted Win32 capture run also preserved one Combat character and four real saves with control dumps (E060); broader gameplay comparison remains pending. The exact runtime configuration is recorded in `ORIGINAL_RUNTIME.md`. E075 adds [13,732 original-machine-code comparisons](../evidence/verification-milestone/original-probe/README.md) with exact matches. These isolate rule inputs; the visible application cases below remain incomplete. E077 additionally executes the original generated-map routine: all 130 grids and 20,280 tile/RNG records match after restoring its omitted minimum-Y clamp. See [generator evidence](../evidence/verification-milestone/generated-map-clamp/README.md). Static implementation tests do not count as original behavior observations.

| Case | Status | Capture required |
| --- | --- | --- |
| R00 Startup | **Passed 2026-09-15** | `evidence/reference-captures/R00-start-menu.png`: title art and New Game, Load Game, and Quit entries |
| R01 Character defaults | Partial: one Combat/Medium capture | Before/after character creation for each class; visible stats and starting inventory |
| R02 Movement | Pending | Open tile, blocked tile, door, and map transition |
| R03 Healing | Pending | Below max, near cap, enhanced-healing condition, and full-health item attempt |
| R04 Economy/equipment | Pending | Buy, sell, equip, insufficient funds, and full inventory |
| R05 Combat | Pending | Hit, miss, multiple opponents, flee, death, and rewards |
| R06 Spells | Pending | Costs, effects, and restrictions |
| R07 Quests | Pending | One state change and revisit |
| R08 Save/load | Partial: four original archives imported and converted | [Capture record](../evidence/original-runtime/r08/README.md) preserves creation, south movement, reopen/reported restart, hashes, and byte differences. Add a fresh process-lifetime/visual restart record and populated later-game saves; current control dumps alone do not establish all restart behavior. |
| R09 Settings | Pending | First-run `Yipe3.dat`, keys, and sound changes |
| R10 Completion | Pending | Late progression and ending behavior |

E081 adds 259,200 exact counterattack records and 103,680 flee records; failed
flee arithmetic is composed from original leaves and disassembly, while the
successful flee routine executes directly with UI cleanup inactive. E082 adds
16,749 late-rule records, including original map-entry regeneration, chest
XP/gold, quest flag leaves and composed consumable purchases. It also verifies
eighteen rebuild-native restart cases and five continued quest/maze routes.
These bounded observations do not change the original GUI case statuses above.
See [scope and limits](../evidence/persistence-parity/README.md).

E084 completes four new fresh-game rebuild routes after the combat corrections.
Independent fresh replays exit 0 with 108,879 exact state/RNG observations and
identical command, trace and final-save bytes. The [completion record](../evidence/corrected-completion/README.md)
retains all earned prerequisites, ordinary recoveries and live finales. It adds
no original GUI observations and does not change the reference-case statuses.

For each run, record runner/Windows version, working-directory contents, exact actions, screenshots or video, generated files and hashes, and whether random outcomes were controlled.
