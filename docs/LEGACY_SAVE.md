# Legacy `.yp3` save analysis

This is the static field map for the player serializer at `0x00443c90`, called through the document serializer at `0x0044f760`. It is not yet a complete file-format specification: MFC `CArchive` framing and CString encoding still require real save samples.

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
15. The `+0x065` healing-related boolean, mirrored through saved field `+0x69c`, serialized as a 32-bit integer.

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

## Rebuild-native saves

The native rebuild format is independent of MFC and legacy `.yp3` files. Its
14-byte header contains magic `Y3RS`, a little-endian 16-bit version, a
little-endian 32-bit payload size, and a CRC-32 of the payload. Version 1 stores
the initially reconstructed player fields. Version 2 appends experience, gold,
equipment, combat settings, world coordinates, the first encounter's completion
state, and the Visual C++ random-generator state. Version 3 appends ability
points, the learned special ability, its active duration, and the three Enrage
stat backups. Version 4 appends the limited-restoration counter and the four
movement flags used by tree, key-door, and unresolved `+0x674` checks. The
loader accepts all four versions, gives version-1 files the starting world
state, gives version-2 files a full starting AP pool, and defaults fields added
by newer formats. Version 5 appends changed map IDs, coordinates, and tile values
so opened doors and linked mechanisms survive map changes and restart. The
loader accepts all five versions. Writes use a temporary file and atomic
replacement so a failed write does not destroy the previous save.

## Remaining work

- Capture paired original `.yp3` files from R08.
- Identify the MFC archive prefix, CString length form, and any document/object framing before the player payload.
- Confirm the field sizes and order against byte differences in real saves.
- Preserve all unsupported fields in the import model, even when the rebuild cannot name them yet.
- Reject truncation, impossible framing, and unsupported archive variants with a specific error.
