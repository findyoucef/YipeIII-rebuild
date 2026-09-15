# Original-runtime reference cases

The original reaches its rendered main menu in a fresh Whisky bottle. Gameplay cases remain pending because the current Codex host cannot control the Wine window. The exact runtime configuration is recorded in `ORIGINAL_RUNTIME.md`. Static implementation tests do not count as original behavior observations.

| Case | Status | Capture required |
| --- | --- | --- |
| R00 Startup | **Passed 2026-09-15** | `evidence/reference-captures/R00-start-menu.png`: title art and New Game, Load Game, and Quit entries |
| R01 Character defaults | Pending | Before/after character creation for each class; visible stats and starting inventory |
| R02 Movement | Pending | Open tile, blocked tile, door, and map transition |
| R03 Healing | Pending | Below max, near cap, enhanced-healing condition, and full-health item attempt |
| R04 Economy/equipment | Pending | Buy, sell, equip, insufficient funds, and full inventory |
| R05 Combat | Pending | Hit, miss, multiple opponents, flee, death, and rewards |
| R06 Spells | Pending | Costs, effects, and restrictions |
| R07 Quests | Pending | One state change and revisit |
| R08 Save/load | Pending | State changes, save, application restart, and load; retain paired `.yp3` files |
| R09 Settings | Pending | First-run `Yipe3.dat`, keys, and sound changes |
| R10 Completion | Pending | Late progression and ending behavior |

For each run, record runner/Windows version, working-directory contents, exact actions, screenshots or video, generated files and hashes, and whether random outcomes were controlled.
