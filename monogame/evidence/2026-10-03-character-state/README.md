# Character state port verification

Plan task P1.2 is complete as a core-library port on 2026-10-03, America/Denver.
The C# character implementation matches 11,322 independently generated C++
records. The desktop preview, approved artwork and walking pace are unchanged;
character creation and inventory screens still belong to the later UI tasks.

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Locked dependency restore | All four projects passed | [Restore log](restore.log) |
| Release rebuild with checked arithmetic | Zero warnings/errors | [Build log](build-checked.log) |
| Core, input, movement and saves | 84 checks passed, including 624 movement cases, 8,192 RNG records and 11,322 Player records | [Check log](core-checks.log) |
| Character comparison | C++ and C# traces match byte for byte: 11,322 records, 9,525,064 bytes | [Comparison](trace-comparison.log), [section hashes](player-reference.json) |
| C++ reference | Compiled with warnings as errors and undefined-behavior sanitization; generated the comparison trace without diagnostics | [Toolchain](toolchain.txt), reproduction below |
| Approved presentation | Existing renderer, town/input rules, art and graphics runner retain prior fingerprints | [Preservation check](presentation-preservation.log) |

The [source fingerprints](source-sha256.json) identify 45 code/build/content inputs,
including the C++ implementation and oracle. The repository HEAD remains
`349fffe71e052dd4e011f1fe09c0e3080f5bb723`; the MonoGame tree is untracked, so that
commit is not a checkpoint for this implementation. No commits were made.

## Coverage and reproduction

The oracle links directly to `src/player.cpp` and `src/random.cpp`. It covers
all three starting presets, repeated random rolls over eight seeds, health
changes, all XP thresholds, growth through level 100, difficulty/stat-bias/cap
boundaries, all ten consumables, invalid items/slots, surface restriction order,
repellent accumulation, signed rescue values, and wrapping defeat counters.
Complete snapshots include unrelated state to catch accidental mutations.
Seven additional direct C# checks explain the array ownership, signed enum,
creation RNG and default-context contracts.

Follow [Yipe.Tests reproduction](../../Yipe.Tests/README.md#character-rules-and-reference)
to compile the C++ oracle, rebuild C# with overflow checking, generate both
traces and compare them. The 13 section digests are stored in
[player-reference.json](player-reference.json) and asserted by the C# runner.
The full trace SHA-256 is
`60af5f35b604f8a99c8031abbb7bab2808de7b37e0a067268af02106a2541f60`.
Large, reproducible traces remain working outputs under `build/monogame-reference/`.
All fixtures required to regenerate them are in the repository tree.

## Scope and remaining work

This verifies the supplied C++ reconstruction, including its documented D8
stat-cap adaptation. It does not establish new original-executable parity.
Tests use valid arithmetic domains for consumables; malformed full-state saves
and their validation are not implemented yet. The character model is not wired
to the desktop session, and surface potions only queue a relocation value.
World movement, combat, equipment transactions, full saves, UI and audio remain
their planned tasks. The core has no MonoGame or platform-I/O dependency.

Graphics were not rerun for this core-only change. The preceding 139-check
[graphics baseline](../2026-10-03-port-baseline/README.md) remains the historical
verification of the unchanged presentation. No native game window was opened
by these checks, and the existing save tests use isolated temporary files.

Next: P1.3, beginning with world location, a shared map catalog and comparisons
for the original cardinal and diagonal movement rules.
