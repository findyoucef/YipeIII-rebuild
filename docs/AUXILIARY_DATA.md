# Original `Yipe3.dat` format

`Yipe3.dat` is a headerless MFC `CArchive` payload for the original
`CUserSettings` object. It is separate from player `.yp3` saves. The mapping is
based on serializer `0x00414030`, default initialization at `0x00414340` and
`0x004143c0`, and the captured 447-byte file in
`evidence/original-runtime/Yipe3-2026-09-15.dat`.

All integers are little-endian. Each of the first 45 records contains an MFC
`CString` followed by a signed 32-bit value. Short ANSI strings begin with one
length byte. Longer strings use `ff` plus a 16-bit length; `ffff` then selects a
32-bit length. The original helper also recognizes `ff feff` as a Unicode marker.

| File order | In-memory offset | Type | Meaning |
| --- | ---: | --- | --- |
| 0 | `+0x004` and `+0x0b8` | 45 × (`CString`, `i32`) | High score names and values, interleaved on disk |
| 1 | `+0x174`…`+0x182` | 8 × `u16` | Northwest, north, northeast, west, east, southwest, south, and southeast virtual-key codes |
| 2 | `+0x190` | `i32` | Registration-related value; exact name pending |
| 3 | `+0x170` | `i32` | Registration-related value; exact name pending |
| 4 | `+0x184`, `+0x18c`, `+0x186`, `+0x188`, `+0x18a` | 5 × `u16` | Run, attack, cast, use-item, and special virtual-key codes in serializer order |
| 5 | `+0x16c` | `i32` | Registration-related value; exact name pending |

The first eight defaults are Windows virtual-key values, not character codes:
Numpad 7, Up, Numpad 9, Left, Right, Numpad 1, Down, and Numpad 3. The five
combat defaults in memory order are Run `R`, Cast `C`, Use Item `I`, Special
`S`, and Attack `A`. The captured file contains those defaults, names `Yip`
and `Youcef` in the middle 15-entry score group, and values 1302, 0, and 4529
in the three registration-related fields.

`load_original_auxiliary_data` implements a bounded reader for this exact field
order. It rejects truncation, trailing bytes, and the Unicode string variant,
which has not been observed in a real Yipe! III file.

The rebuild does not overwrite `Yipe3.dat`. `settings_from_original` imports
its 13 bindings into the portable `GameSettings` action order: eight movement
directions followed by Attack, Run, Cast, Use Item, and Special. Native
`settings.y3r` stores those bindings and the sound toggle with a versioned
header and checksum; high scores and the three unresolved registration fields
remain outside that native settings file. The original sound toggle is an
application-profile `Y3Settings/Sounds` value rather than part of `Yipe3.dat`.
