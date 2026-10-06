# P2–P4 core checkpoint — 2026-10-04

The continuous P2–P4 goal remains active. This checkpoint verifies domain rules,
not playable milestone acceptance. The desktop still has the approved Maynard
preview. `Adventure.cs` also passes the retained earned opening route: 42 full persistent-state/RNG checkpoints, 116 victories, 14 chests, two rescues and the first stamp. An additional real C++ return crosses the town bridge and visits the King three times for 500 gold. A separate C# writer/reader process saves before Wormoid and earns the remaining rescue and King reward after reopening. This is headless gameplay acceptance; it does not establish frontend completion.

Checked Release build: zero warnings/errors. **488 checks pass**, including all
P1 persistence/import/restart checks. No desktop renderer/input changes have been
made at this checkpoint, so the P1 graphics evidence remains the presentation
baseline; no new graphics run is claimed.

The following independent drivers call the original C++ implementations with
undefined-behavior sanitization. Their matching C# traces include complete Player
state and RNG where the operation can change them. Catalog/visibility/movement
records include their complete respective outputs. Quest text includes embedded
line breaks, so its count is LF-delimited records rather than operation count.

| Driver | LF records | SHA-256 |
| --- | ---: | --- |
| services_reference.cpp | 17,883 | `6bc81a9e9cbdf9804af21d4f71fd768f0564db56e2b677d4edb2725b83eb5eb8` |
| exploration_reference.cpp | 37,096 | `382fbcd9e06d4a4b92b1dd3939f6068df6b54d7c41c0914c3f2db345cadc08b5` |
| combat_reference.cpp | 28,363 | `1ccfefc62b63a47f04b1f433039fc74553e094b6d6eb520f4734dfa9310ffffa` |
| quest_reference.cpp | 20,311 | `799f84ce66ba26e4b203c23e4e1f920a82575464815467725be743b2ca047b56` |
| encounters_reference.cpp | 88,298 | `84ce49c678d035b4a98274973df4406b975d5cb25e0cd24722c98749ef63a373` |

Coverage includes four shop stock tables and stage changes, healing/training,
sequential spell learning, all exploration spells and casino rules; visibility,
hints/Other/secret tree, all timed hazards, regeneration/repellent/platforms;
41 monster definitions, 42 weapon damage ranges, multi-opponent setup and turns,
all abilities/combat spells, fleeing, loot and stamp/key rewards; King dialogue,
rescue choices, one-time chests, defeat/Hell/recovery/finale; all 186 placements
across levels 1–50 and three difficulties, roaming terrain and authored pursuers.

Reproduce from the checkout (after locked restore):

```sh
monogame/Yipe.Tests/verify-gameplay-references.sh
```

The script compiles the retained original-source drivers, runs checked C# tests
and compares bytes. Full traces are reproducible at `build/monogame-reference/`;
they are deliberately not copied here. Do not replace expected C++ hashes with
C# output. `source-inputs.json` fingerprints relevant checkpoint source files.

Opening inputs and C++ saves are retained in `Yipe.Tests/Fixtures/Opening/`.
Reproduce their extraction and original application run with:

```sh
python3 monogame/tools/extract_opening_route.py
python3 monogame/tools/capture_opening_reference.py
dotnet run --project monogame/Yipe.Tests -c Release
```

The original app run passed 665 existing cases before exporting its earned saves.
The exporter modifies only a generated copy's test entry point to observe those
checkpoints. It does not change C++ gameplay, queues, combat strategy or rewards.
It then reopens the earned final save and returns over the Maynard bridge to the
King through normal inputs. All reference saves are comparisons, never injected
into the C# opening run. The C# restart test reopens its own newly earned save.

Next: world rendering, gameplay overlays, audio and complete campaign runs.
No full campaign, physical controller or console execution is claimed.
