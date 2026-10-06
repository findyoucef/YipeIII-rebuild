# Game-session foundation verification

P1.4's session foundation is complete on 2026-10-03, America/Denver. Logical
movement now runs through `GameSession`, which owns the character, location,
gameplay RNG, current static map and remembered tile changes. The preview keeps
its Maynard boundary, approved presentation and position-only save format.

| Check | Result | Evidence |
| --- | --- | --- |
| Locked restore | All four projects passed | [Restore](restore.log) |
| Checked Release rebuild | Zero warnings/errors | [Build](build-checked.log) |
| Core/input/save checks | 226 passed, retaining all preceding reference sections | [Checks](core-checks.log) |
| New C++ comparison | 21,511 records, 10,118,050 bytes, byte-for-byte match | [Comparison](trace-comparison.log), [section hashes](session-reference.json) |
| Real graphics | 153 checks, 360 tour frames, recording off | [Graphics](graphics-checks.log) |
| Existing user save | Existence and fingerprint unchanged; test storage isolated | [Save isolation](save-isolation.log) |

The C++ fixture compiled with C++20, `-Wall -Wextra -Werror` and undefined-behavior
sanitization and ran without diagnostics. It invokes the original core rules;
its bounded door/switch/restoration/fire/teleport sequence mirrors corresponding
application branches. It does **not** run the SDL application or establish full
event-pipeline or campaign parity. Commands, row format and coverage are in
[the session reference documentation](../../Yipe.Tests/README.md#session-commands-and-entry-reference).
The complete traces are reproducible working outputs under
`build/monogame-reference/`; their generators and digest expectations are retained
in the repository. [Toolchain and working-tree state](toolchain.txt).

Entry comparisons cover every cell of all 112 static maps and all chest masks,
with signed quest-field boundaries and full tile/chest snapshots. Other sections
cover successive real-map actions, restoration exhaustion, fire RNG order and
the one-HP floor. Focused checks cover edge and cave loading, item context and all
queued relocation kinds, unavailable destinations, entry/persistence ordering,
D7 platform support, repeated tile-change replacement and transient tile reset.
All earlier town/RNG/character/map/world reference digests still pass.

The desktop now saves the session's accepted position even while its sprite is
moving. Load/reset synchronizes logical and visual positions. Six additional
graphics checks exercise that contract, cave/edge boundaries, shop handoff and
RNG isolation; the tour now dispatches commands through the session. The fresh
[default view](default-town.png) was visually inspected, and a
[diagonal-step capture](diagonal-step.png) is also retained. The test window exited
successfully; no test jobs were left running.

[Source fingerprints](source-sha256.json) cover 56 inputs, including the inspected
C++ application dispatch. [Changed/newly fingerprinted inputs](changed-inputs.json)
compare against P1.3; a null previous hash means newly included in the manifest,
not necessarily a newly created source file. No C++ gameplay source was modified.
Renderer/scenery code, effects, input, animation timing, map data and art retain
their preceding hashes. The final simplicity review covered session ownership,
entry/action ordering, frontend integration and the new fixtures; no further
implementation changes were needed after validation.

Arrival hints/messages, princesses, encounters, chests/rewards, transient monster
state, visibility, timed events, generated maps and finale completion remain
their P2/P3 tasks. The session returns original interaction codes and exposes
authored events; it does not silently grant unported rewards or resolve combat.
Full-state storage is not implemented here. Physical controllers, other desktop
platforms, full-campaign execution and consoles remain unverified.

Next: P1.5, versioned full-state saves with safe replacement and validation before
changing the live session. HEAD is still
`349fffe71e052dd4e011f1fe09c0e3080f5bb723`; the MonoGame tree remains untracked,
so that commit is not a recoverable copy of this implementation. No commits were
made. Resume from [PORTING_PLAN.md](../../PORTING_PLAN.md) and preserve the working
tree.
