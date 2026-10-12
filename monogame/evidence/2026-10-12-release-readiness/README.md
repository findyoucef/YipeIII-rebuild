# macOS preview.7 release-readiness pass — 2026-10-12

The user approved the Ice Cave traction repair and requested retained-menu/save
verification, full regression/performance checks, physical-controller testing and
an updated distributable. They subsequently confirmed no physical controller is
available and approved documenting that coverage as unverified.

## Result

- Checked Release builds: zero warnings/errors; locked restore passes.
- **645 core/input/persistence checks**, including independent C++ record digests.
- **4,226 real graphics integration checks**: 115 static/generated maps, all
  canonical monsters/NPCs, caves, towns, UI, effects, audio, remapping and saves.
- Fresh C++ native replay and C# campaign agree on **43,080 state/RNG observations
  and 66 complete saves**. Gameplay rules and earned completion remain unchanged.
- Seven fresh UBSan-enabled native reference traces (services, exploration,
  combat, quests, encounters, scores, items) match C# byte-for-byte.
- Real frontend opening matches all 42 reference checkpoints and the King return;
  a separate process continues its save. Late frontend defeats the empowered
  Gatekeeper, saves/restarts, earns rescue 20 and displays the live ending.
- Eight special-room writer/reader processes compare 80 restoration/continuation
  ticks each, including Hell, all three generated rooms, hazards and a platform.
  These are cross-process consistency tests; existing C++ import/generator tests
  independently verify the inherited regeneration and timer-reset behavior.
- A killed repeated-save writer leaves a valid committed adventure. A separately
  staged partial orphan file is ignored, and subsequent saving succeeds. Existing
  tests cover permissions/replacement failures, corrupt imports, stale notes,
  concurrent writers, new-adventure isolation and canceled/failed title loads.
- No gameplay/art redesign was needed. Added release persistence/performance
  runners, separate MonoGame CI, package version metadata, current menu/save
  instructions and normal-startup checks against the packaged runtime.
- All **33 personal save/profile/journal files remain byte-identical**. No live
  gameplay input, save migration, commit, push or public upload was performed.

[Visual review](review.html) contains current title/menus, scenes and earned finale.

## Performance

Apple **M5 Max, 64 GiB RAM**, macOS **26.7.1**, .NET SDK **10.0.300**. Normal
fixed-60-Hz `Game.Run`, fresh shipping visual defaults and audio enabled. Each
scene has 60 warmup and 300 measured frames at each resolution; captures are
excluded. Fully revealed diagnostic scenes retain visual animation while world
ticks are held; forest includes rain, and a real three-enemy attack loops.

All samples average **60.01–60.16 FPS**. Across **5,400 frames**, three intervals
exceed 25 ms (worst **42.31 ms**). The worst scene's 95th-percentile interval is
**17.00 ms**, and 99th-percentile is **17.76 ms**. CPU draw submission p95 is at
most **1.84 ms**, excluding GPU completion. No sustained slowdown was found.
These are warm-scene measurements on this Mac, not minimum-system certification
or uncapped GPU throughput. Individual measurements and allocations/working set
are in [performance.json](performance.json).

| Scene | Width | FPS | Frame p95 ms | Frame p99 ms | Frames >25 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| maynard | 1280 | 60.01 | 16.93 | 17.09 | 0 |
| snow | 1280 | 60.02 | 16.86 | 17.14 | 1 |
| forest | 1280 | 60.05 | 16.82 | 17.76 | 1 |
| ervyn | 1280 | 60.03 | 16.81 | 17.00 | 0 |
| breeding | 1280 | 60.10 | 17.00 | 17.10 | 0 |
| fire | 1280 | 60.01 | 16.86 | 16.93 | 0 |
| ice | 1280 | 60.01 | 16.97 | 17.20 | 1 |
| hell | 1280 | 60.06 | 16.83 | 16.93 | 0 |
| three-enemy-battle | 1280 | 60.08 | 16.85 | 17.24 | 0 |
| maynard | 1920 | 60.09 | 16.86 | 17.06 | 0 |
| snow | 1920 | 60.03 | 16.87 | 17.00 | 0 |
| forest | 1920 | 60.02 | 16.82 | 17.13 | 0 |
| ervyn | 1920 | 60.03 | 16.80 | 16.95 | 0 |
| breeding | 1920 | 60.07 | 16.94 | 17.09 | 0 |
| fire | 1920 | 60.09 | 16.86 | 17.17 | 0 |
| ice | 1920 | 60.16 | 16.95 | 17.16 | 0 |
| hell | 1920 | 60.01 | 16.88 | 17.27 | 0 |
| three-enemy-battle | 1920 | 60.11 | 16.91 | 17.16 | 0 |

## Physical controller — unverified

No game controller was detected in macOS HID inventory. The user explicitly
confirmed no controller is available. Synthetic D-pad/stick/button, dead-zone,
repeat, diagonal, remap, disconnect/reconnect and focus-suppression checks pass;
they do **not** establish physical hardware support.

When a controller is available, record model, wired/Bluetooth connection, macOS
version and build hash, then use an isolated adventure to check:

1. Title New/Load/Options/Back and the character-name controller keyboard.
2. All eight D-pad/stick directions, dead zone, held motion and ice-door alignment.
3. Actions, Other, map, all pause categories, shops, targeting, combat and rewards.
4. Remap all four gameplay buttons, save preferences, quit/relaunch and reload.
5. Disconnect during movement and combat: pause without phantom input. Reconnect
   with a held button: no accidental selection; deliberate resume works.
6. Focus loss/return while holding movement and Confirm, mid-step pause/save,
   reload, and keyboard fallback. Record observable results separately.

## Evidence limits

This is an **Apple Silicon macOS 26+ development preview**, tested on 26.7.1.
It is ad-hoc signed, not Developer ID signed or notarized. Other Mac models,
Intel/older macOS, Windows/Linux and consoles are unverified. Power-loss/fsync
hardware durability is not simulated by process termination.

The retained Combat/Medium/1 full campaign is freshly verified. The three
historical Quickness/Easy/2, Magic/Hard/3 and Random/Medium/4 completion input
streams are still absent; P0.2/P6.1 remain open. Existing parameterized core
checks cover all classes/difficulties, but are not substitutes for those routes.
The new CI workflow passes `actionlint`; a hosted run awaits a push. Its setup
uses the [official setup-dotnet action](https://github.com/actions/setup-dotnet).

## Reproduce

From the repository root, run these sequentially (active desktop for graphics):

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet monogame/Yipe.Tests/bin/Release/net10.0/Yipe.Tests.dll
bash monogame/Yipe.Tests/verify-gameplay-references.sh
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --capture-monsters 1 --output build/release-checks
python3 monogame/tools/verify_campaign.py monogame/evidence/2026-10-04-p234-complete/campaign build/fresh-native
dotnet monogame/Yipe.Tests/bin/Release/net10.0/Yipe.Tests.dll --campaign-route monogame/evidence/2026-10-04-p234-complete/campaign build/fresh-campaign-saves
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --opening-route --output build/release-opening
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --late-route build/fresh-campaign-saves --output build/release-late
dotnet monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks.dll --performance-check build/release-performance
python3 monogame/tools/package_macos.py --version 0.1.0-preview.7 --output build/release-reproduction
python3 monogame/tools/verify_macos_package.py build/release-reproduction/YipeIII-MonoGame-0.1.0-preview.7-macOS-arm64.tar.gz --tests monogame/Yipe.Tests/bin/Release/net10.0 --graphics monogame/Yipe.GraphicsChecks/bin/Release/net10.0 --output build/release-package-check
```

Use unused output directories for the native replay and package archive. Large
raw frame sequences remain under `build/release-readiness/full/`; selected images
and logs are retained here. The initial benchmark draft used incomplete scene
selection and was stopped; only `performance-final` results are accepted.

## Verified distributable

Archive: `build/monogame-packages/YipeIII-MonoGame-0.1.0-preview.7-macOS-arm64.tar.gz`

Extracted app: `build/monogame-packages/preview.7/Yipe III MonoGame.app`

SHA-256: `f426e69af77e45890afd38c6f3a544e60cf7dba416ba1bd7736899b9188c785e`

Verification passed for **421 files, 208 content files and 15 native libraries**.
The extracted app passes strict ad-hoc signature and arm64 checks and launches
from an empty directory through both its launcher and LaunchServices. Loader
tracing confirms bundled CLR, SDL and OpenAL without Homebrew dependencies.
Separate runners against the package's actual assemblies and official .NET
10.0.8 runtime pass full-state/RNG imports and the eight-room restart checks.
Normal Game.Run startup, ident/title, frame pacing, resize, fullscreen and return
to a window all pass. See [package report](package-check/report.json) and
[normal startup log](package-check/normal-startup.log). The retained extracted
copy was separately checked against every manifested file hash.

Scoped simplify review kept all changes in release tooling/checks/documentation;
no production gameplay changes or test hooks were needed. `git diff --check`,
Python compilation and `actionlint` pass. The raw interrupted benchmark draft
was excluded from the accepted report.
