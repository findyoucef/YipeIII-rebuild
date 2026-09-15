# Open questions and behavior differences

| ID | State | Question or difference | Evidence needed / next action |
| --- | --- | --- | --- |
| U001 | Resolved for startup | A fresh Whisky 3.7.0 bottle with its `v3.1.1` library archive runs the executable and renders its main menu. | Obtain working Accessibility control and complete the gameplay reference cases. Use Windows virtualization if ordinary interaction is unreliable. See `ORIGINAL_RUNTIME.md`. |
| U002 | Open | What gameplay condition does player offset `+0x65` / saved field `+0x69c` represent? | Trace all reads/writes and correlate it with a paired original save and on-screen status. |
| U003 | Open | What are player offsets `+0x4c`, `+0x62`, `+0x654`, `+0x664`, `+0x66a`, `+0x67e`, `+0x680`, and `+0x692` called in game terms? | Follow map, registration/difficulty, encounter, and surface-potion callers before renaming them. |
| U004 | Open | Are all 30 serialized 16-bit values at `+0x94` consumable inventory slots, and why does invalid-item cleanup only clear indexes 0–12? | Trace inventory-dialog list construction and every write to the array. |
| U005 | Open | What is the complete `.yp3` archive framing and string encoding? | Produce paired saves and map `CArchive` helper encodings around `0x00443c90`. |
| U006 | Partially resolved | `Yipe3.dat` serializes `CUserSettings`: 45 high-score records, 13 key bindings, and three registration-related integers. What are the exact names and semantics of the three integers? | The file layout and captured values are documented in `AUXILIARY_DATA.md`; trace registration callers and compare another original file before naming the remaining values. |
| U007 | Partially resolved | The 112 static 13×12 map tables and the runtime-generated `0x76`–`0x78` path are located. Where are all monster, item-stat, spell, quest, and generated-map rules stored? | Extend `MAPS.md` with transitions and event objects, then trace the remaining data families. |
| U008 | Accepted pending runtime check | The fallback weapon resource spells `Manynard Power Sword`, while the external file says `Maynard Power Sword`. | Preserve each source's spelling. Confirm the fallback text by launching without `Yipe3Items.txt`. |
| U009 | Open | Does the original item-name parser treat `#` anywhere as a comment marker or only at the first character? | The string-search branch suggests anywhere; add an original runtime case before considering E008 fully behavior-verified. |
