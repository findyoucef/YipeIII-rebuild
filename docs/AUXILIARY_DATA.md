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
| 1 | `+0x174`…`+0x182` | 8 × `u16` | Primary movement/action virtual-key codes |
| 2 | `+0x190` | `i32` | Registration-related value; exact name pending |
| 3 | `+0x170` | `i32` | Registration-related value; exact name pending |
| 4 | `+0x184`, `+0x18c`, `+0x186`, `+0x188`, `+0x18a` | 5 × `u16` | Combat command virtual-key codes in serializer order |
| 5 | `+0x16c` | `i32` | Registration-related value; exact name pending |

The default key codes are `g`, Up, `i`, Left, Right, `a`, Down, `c`, followed
in memory by `R`, `C`, `I`, `S`, `A`. The captured file contains default key
codes, names `Yip` and `Youcef` in the middle 15-entry score group, and values
1302, 0, and 4529 in the three registration-related fields.

`load_original_auxiliary_data` implements a bounded reader for this exact field
order. It rejects truncation, trailing bytes, and the Unicode string variant,
which has not been observed in a real Yipe! III file.
