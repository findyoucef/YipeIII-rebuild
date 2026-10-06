# World and movement port verification

P1.3 is complete on 2026-10-03, America/Denver. The shared C# map and world rules
match 311,728 C++ records, and the Maynard preview now supports diagonals using
its approved artwork and 250 ms steps. Session dispatch and playable areas
beyond Maynard remain later work.

| Check | Result | Evidence |
| --- | --- | --- |
| Locked restore | All four projects passed | [Restore](restore.log) |
| Checked Release rebuild | Zero warnings/errors | [Build](build-checked.log) |
| Core/input/save checks | 188 passed, including prior movement/RNG/character references and the new map/world reference | [Checks](core-checks.log) |
| C++ comparison | 311,728 records, 9,717,008 bytes, byte-for-byte match | [Comparison](trace-comparison.log), [section hashes](map-reference.json) |
| Real graphics | 147 checks passed, 360 tour frames rendered, recording disabled | [Graphics log](graphics-checks.log) |
| Existing user save | Fingerprint unchanged; tested storage remained isolated | [Save isolation](save-isolation.log) |

The C++ oracle compiled with `-Wall -Wextra -Werror` and undefined-behavior
sanitization and generated the trace without diagnostics. Reproduction commands
and section coverage are in [Yipe.Tests](../../Yipe.Tests/README.md#maps-and-world-movement-reference).
Large reproducible traces and the full capture set are working outputs under
`build/monogame-reference/` and `build/graphics-checks/`; the repository contains
all required source fixtures. The recorded toolchain is in [toolchain.txt](toolchain.txt).

[Source fingerprints](source-sha256.json) identify 52 code/build/content inputs.
[Changed inputs](changed-inputs.json) distinguish this work from the character
baseline. The renderer, scenery and image assets retain their previous hashes.
The tested graphics runner adds diagonal checks to the existing 139 assertions.
No C++ gameplay sources were changed. HEAD remains
`349fffe71e052dd4e011f1fe09c0e3080f5bb723`; the MonoGame tree remains untracked,
so that commit is not a recoverable checkpoint for these sources. No commits
were made.

The fresh [default town](default-town.png) and [diagonal step](diagonal-step.png)
captures preserve the current presentation. The diagonal capture was visually
inspected; all four diagonal paths were exercised through game dispatch.

Map comparisons include all 112 static maps and complete event/chest records,
movement equipment gates, overlay/depth precedence, instance mutations, all
valid world regions/layers/map sets, edge/depth changes and queued relocations.
The parser rejects malformed structure and preserves generated-map chest IDs
without pretending those maps have static tiles. Each instance owns its mutable
data. The small town adapter delegates to the shared rules while preserving the
existing preview boundary and position-save format.

Diagonals use the original destination-only collision rule and one 250 ms grid
step, with horizontal-facing poses from the existing artwork. Input checks use
synthetic keyboard/controller states; physical hardware remains unverified.
Generated maps, visibility, platform advancement, map-entry action dispatch,
combat and full saves are not implemented by this milestone. These comparisons
verify the supplied C++ reconstruction, including inherited D7 support restoration,
and do not establish new original-executable parity or full-game completion.

Next is P1.4: a concrete game session owning character, RNG, world location and
current map, with C++ action ordering and map-entry initialization.
