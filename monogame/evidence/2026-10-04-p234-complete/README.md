# P2, P3 and P4 completion — 2026-10-04

The port completes an earned Combat/Medium/seed-1 adventure from fresh creation
through **20 rescues, six stamps and the live Maynard ending**. It acquires both
keys, waterproof spray, climbing shoes, strength 40+, the secret-tree healing
ring, all twelve spells and Gatekeeper progression 1 → 2 → 3 through ordinary
movement, victories, funded purchases and elapsed exploration ticks. Six ordinary
deaths and their Hell recoveries remain in the route. Final level is 45.

## Independent campaign comparison

`campaign/commands.txt` contains 43,775 literal continuation commands after the
unchanged 42-checkpoint opening in `tests/app_shortcut_tests.cpp`. The native
input driver runs fresh creation and that opening before consuming this stream.
`CampaignRouteChecks` does the same through C# `Adventure`. Neither loads an
observation or checkpoint save into the live campaign.

All **43,080 state/RNG observations** in `campaign/states.jsonl.gz` match C#.
All **66 complete native saves** indexed by `campaign/checkpoints.jsonl` match
the entire C# persistent state after native-format conversion, beyond the fields
in the observation trace. The manifest's zero-based observation and one-based
command count identify each checkpoint. Saves and the compressed original trace
are retained beside the commands. Checkpoint labels describe authoring intent;
the saved state and final assertions determine actual progress.

A separately rebuilt native executable replays the complete stream from fresh
creation, exits 0, verifies the live finale, reproduces all observations, consumes
identical command bytes and reproduces all 66 native save files byte for byte.
See `native-verification.json`, `campaign-native.log` and `campaign-csharp.log`.
The native game and test driver were not changed. This regenerates one default
completion fixture; it does not reproduce the missing historical four-fixture
matrix or validate the original Windows executable.

From the repository root (.NET 10, C++20 compiler, SDL2 and pkg-config):

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
python3 monogame/tools/verify_campaign.py monogame/evidence/2026-10-04-p234-complete/campaign build/campaign-native-check
dotnet run --project monogame/Yipe.Tests -c Release --no-build -- --campaign-route monogame/evidence/2026-10-04-p234-complete/campaign build/campaign-earned-saves
```

Choose a new native output directory for each run. The C# output directory holds
its own earned `.y3m` saves; these are produced at the compared checkpoints, not
imported snapshots. The input-only authoring client is not needed to replay the
retained route.

## Real frontend and fresh-process continuation

The opening frontend replay independently matches all 42 C++ checkpoints, earns
116 victories, 14 chests, two rescues and the King reward. It uses production
creation/interaction/save menus and automatically continues in a new process.

The late frontend starts with the C# campaign's earned
`hell-finale-boss-ready.y3m`. It defeats the empowered Gatekeeper through actual
combat/spell/item overlays, rescues princess 19, rests, reaches the final fire
landing and saves through the production pause menu. A separately launched
process reloads that save, waits for the fire opening, earns rescue 20 and exits
Hell. Leaving and re-entering Maynard through its normal edge displays the live
ending and advances Gatekeeper state to 3. Both production saves match complete
in-memory state. Cancelling New adventure also restores an active battle.

```sh
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --opening-route --output build/graphics-checks
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --late-route build/campaign-earned-saves --output build/graphics-checks
```

The late check uses 50 ms updates, real exploration timers and 250 ms movement;
no quest, map, character or resource fields are granted. It navigates existing
UI boundaries with synthetic inputs. Clock timing and encounter outcomes after
reopening can differ from the native input stream; this frontend continuation
proves earned play/save/restart, not a second identical RNG trace. It takes
49,434 update frames before restart and 128 afterward. Resting frames are
simulated without wall-clock waits. The retained `frontend/` contains the late
input save, actual restart and completed saves, logs and inspected screenshots.
The opening replay's controlled timer protocol is documented in the earlier
[earned frontend evidence](../2026-10-04-earned-frontend/README.md).

## Broader verification and limits

- Checked Release build: zero warnings/errors; **502 core/input/storage checks**.
- Seven independent C++/C# byte-identical service, exploration, combat, quest,
  encounter, high-score and item-name traces (`gameplay-references.log`).
- **629 graphics checks**, including 115 static/generated map instances, all 97
  initial/linked/timed tile mappings, twelve appearances, monster/quest artwork,
  world reflections, casinos, inventory, statistics, sound playback and timed
  platform/fire routes. Platform saving and imported history have separate
  [diagnostic evidence](../2026-10-04-timed-routes/README.md).
- Opening and late frontend checks run in addition to those 629 checks. The
  full-map rendering audit and isolated route fixtures do not establish earned
  progression by themselves; the complete input stream above supplies that proof.
- Approved 16:9 overlays, 250 ms steps, four-frame-per-second walking, default
  scanlines and Strong miniature effect remain. New artwork and exact generation
  prompts are in the [world manifest](../../Yipe.Desktop/Content/Art/Retro/World/README.md).
- All tests use isolated saves. The pre-existing personal preview save retains
  its pre-goal SHA-256; `user-save-preservation.json` records the check.

This evidence is for Apple Silicon macOS with .NET 10 and MonoGame DesktopGL.
Physical controllers, Windows/Linux, the remaining class/difficulty/seed matrix,
release packaging, desktop conveniences and official consoles remain P5/P6/P7.
Source and asset hashes are retained in `source-inputs.json`; evidence hashes
are in `artifacts.json`. The scoped implementation review is recorded in
`review.md`. Re-run affected checks after changing those inputs.
