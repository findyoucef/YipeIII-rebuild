# Legacy `.yp3` save analysis

This is the static field map for the player serializer at `0x00443c90`, called through the document serializer at `0x0044f760`. The import implementation follows the original executable's archive path exactly, but compatibility is not yet demonstrated against a real `.yp3` sample.

## Archive framing and string encoding

The open-document path at `0x00471d80` constructs a load-mode `CArchive`
directly over the selected file and invokes the document serialization slot.
`Yipe_SerializeDocument` immediately delegates to the embedded player. No
class name, schema number, magic value, or other document prefix is written by
this path: the file begins with the player's CString.

The ANSI CString writer at `0x00472233` uses the standard variable-length MFC
form recovered directly from its branches:

- lengths below `0xff` use one byte;
- longer lengths begin with `ff` and a 16-bit length;
- a 16-bit value of `ffff` is followed by a 32-bit length;
- a 16-bit value of `fffe` denotes a Unicode string and is followed by another
  encoded character count.

This executable writes ANSI strings. The import reader retains their bytes in
`std::string` and rejects the Unicode archive variant explicitly. After the
name bytes, the 1.1.6.0 player payload has exactly 5,064 bytes. That fixed size
lets the reader reject a framed object, an older/newer field layout, truncation,
and trailing data rather than silently shifting the field map.

## Serialization order

All offsets below are in-memory offsets in the original player object. They are not file offsets.

1. CString at `+0x004`.
2. Thirteen 32-bit integers at `+0x008`, `+0x00c`, `+0x010`, `+0x014`, `+0x018`, `+0x024`, `+0x028`, `+0x01c`, `+0x020`, `+0x02c`, `+0x05c`, `+0x030`, and `+0x034`.
3. A 32-bit validation value produced by `0x00445020`.
4. Five 16-bit values at `+0x038` through `+0x040`.
5. A 32-bit value at `+0x658`.
6. A 16-bit value at `+0x07e`, a 32-bit value at `+0x070`, 16-bit values at `+0x07c` and `+0x65c`, then 32-bit values at `+0x074` and `+0x078`.
7. Seven booleans serialized as 32-bit integers from `+0x06c`, `+0x060`, `+0x061`, `+0x062`, `+0x063`, `+0x064`, and `+0x64c`.
8. 512 pairs of booleans serialized as 32-bit integers from arrays at `+0x152` and `+0x352`.
9. Fifteen 16-bit values at `+0x10c`.
10. Ten pairs: a boolean-as-32-bit at `+0x148` and a 16-bit value at `+0x080`.
11. Thirty triples: a 16-bit item ID at `+0x094`, a 16-bit value at `+0x0d0`, and a boolean-as-32-bit at `+0x12a`.
12. Fifty triples: a 16-bit value at `+0x65e`, a 32-bit value at `+0x584`, and a boolean-as-32-bit at `+0x552`.
13. Five 16-bit values at `+0x042`, `+0x044`, `+0x046`, `+0x048`, and `+0x050`.
14. A 32-bit value from `+0x058`, stored after subtracting 203, followed by a 32-bit value at `+0x054`.
15. The `+0x065` healing-related boolean, mirrored through saved field `+0x69c`, is appended as a 32-bit integer by the write branch. The original read branch derives `+0x065` from the already loaded `+0x69c` array entry and does not consume the appended copy.

The load branch reverses the `-203` transform. It calls `0x00445060` to compare the stored validation value with the recomputed value, accepts a difference from -1 through +1, and stores the result at `+0x64e`.

## Validation helper

`0x00445020` and `0x00445060` use this integer expression:

```text
value_at_0x58 / 2
  + (maximum_health - current_health) * 2
  + u16_at_0x3e
  + u16_at_0x3c
  + u16_at_0x38
  + i32_at_0x34
  + i32_at_0x30
```

The write branch stores zero instead when the prior validation flag is false. This is a consistency check, not evidence of a cryptographic checksum.

## Import mapping

`load_legacy_save` exposes a `LegacySave` containing both a rebuild `GameState`
and every serialized field not yet understood. The known mapping includes the
player name, primary stats, HP/SP/AP, level, weapon and armor ranks, five minor
equipment ranks, XP, transformed gold, world coordinates, computed map ID,
difficulty, combat style, restoration count, tree/key movement state, inventory,
and the recovered extended item-status fields. Offset-based arrays remain in
the import object so future reconstruction does not require reparsing the file.

The original validation expression is evaluated and reported through
`validation_passed`. A mismatch is non-fatal, matching the original loader,
which stores the result as player state rather than rejecting the document.
The importer never writes `.yp3` files.

## Rebuild-native saves

The native rebuild format is independent of MFC and legacy `.yp3` files. Its
14-byte header contains magic `Y3RS`, a little-endian 16-bit version, a
little-endian 32-bit payload size, and a CRC-32 of the payload. Version 1 stores
the initially reconstructed player fields. Version 2 appends experience, gold,
equipment, combat settings, world coordinates, the first encounter's completion
state, and the Visual C++ random-generator state. Version 3 appends ability
points, the learned special ability, its active duration, and the three Enrage
stat backups. Version 4 appends the limited-restoration counter and the four
movement flags for waterproof shoe spray, tree-climbing shoes, and both keys. The
loader accepts all four versions, gives version-1 files the starting world
state, gives version-2 files a full starting AP pool, and defaults fields added
by newer formats. Version 5 appends changed map IDs, coordinates, and tile values
so opened doors and linked mechanisms survive map changes and restart. Version 6
appends the 15 learned-spell values recovered from legacy offset `+0x10c`. The
loader accepts all six versions. Writes use a temporary file and atomic replacement
so a failed write does not destroy the previous save.

## Verification status

- Static evidence fixes the headerless framing, CString forms, field order,
  widths, gold transform, and validation expression.
- Byte-level synthetic tests exercise every field family, both supported ANSI
  length forms practical for player names, validation, unknown-field retention,
  truncation, and the unsupported Unicode marker.
- No original `.yp3` exists in the preserved runtime evidence or analysis
  workspace. Capture paired R08 saves and load them in the rebuild before
  claiming original-save compatibility.
