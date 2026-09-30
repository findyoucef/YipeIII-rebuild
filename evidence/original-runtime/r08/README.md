# R08 original save captures

Captured 2026-09-22 with the supplied Yipe! III 1.1.6.0 under the Whisky
configuration in `docs/ORIGINAL_RUNTIME.md`. Executable SHA-256:
`6e309aeedc59d80470e0d5edb4be2fe165de5c36d5ad29aa7bdd4a39272f3286`.
The archives and Win32 control dumps were retained by the interrupted run;
this checkpoint verifies their byte differences and imports all four.

The capture sequence is Combat character creation as `R08Combat`, Medium,
save at `(5,5)`, move south once and Save As, reopen the south save and save
again, then restart the original, load the south save, and Save As again.
The control dumps preserve the creation dialog and each document title and
visible HP/SP/AP, level, XP, and gold. The `restart-*.txt` dumps support the
reported restart sequence but contain no OS process-lifetime log; their Wine
process ID is reused. No usable gameplay screenshot was retained. Treat R08
as a bounded save/reopen capture, not full restart or gameplay parity proof.

| File | Size | Position | SHA-256 |
| --- | --- | --- | --- |
| `R08Combat-start.yp3` | 5074 | `(5,5)` | `7cd65c821a51bcee40d9bd7040754a0affca3127f6d58a3e1ec36882309f5a5e` |
| `R08Combat-south.yp3` | 5074 | `(5,6)` | `320cec8d283fb57ebfa332fa4a2ace2578f6d07609835d2d956d3a44c14c6f6a` |
| `R08Combat-reopened.yp3` | 5074 | `(5,6)` | `331cd11fbbab9a5955287302cc60f0e1dae688f7e1e81130e929b2f05016f69e` |
| `R08Combat-restart.yp3` | 5074 | `(5,6)` | `09eb28316056f778decaa532cf0015722f39e12d49f1694a671f20d0f02840e5` |

All start with `09` and the nine ANSI bytes `R08Combat`, then the expected
5064-byte player payload. They contain STR 16, DEX 13, INT 10, HP 13/13,
SP 10/10, AP 5/5, level 1, gold/XP 0, weapon/armor 2, helmet 1, difficulty 2,
portrait 0, map `0x11`, layer/region 0. Validation is 5 and passes.

Only these bytes differ (offsets are zero-based file offsets):

| Field | Offset | start | south | reopened | restart |
| --- | --- | --- | --- | --- | --- |
| Player `+0x672`, extended entry 10 | `0x122c` | 3 | 9 | 34 | 11 |
| Player `+0x676`, extended entry 12 | `0x1240` | 31 | 7 | 40 | 31 |
| Player Y `+0x044` | `0x13be` | 5 | 6 | 6 | 6 |

The serializer itself rewrites the first two fields using `rand()%44` and
`rand()%46` before writing. Their changes do not mean gameplay state was
lost on reopen. The preserved export is
`evidence/corrected-analysis/functions/00443c90.c`.

`test_original_runtime_saves` imports every file, checks the reference state,
and converts it to a native save and reloads it. The SDL event-loop suite
opens the actual south capture, selects a separate `.y3r` destination, plays,
saves, and reloads it. Native dialogs and time are substituted in that suite.
The source archive stays unchanged. These samples establish compatibility
with this early-game archive family; they do not test populated late-game
flags, active abilities, other original versions, or a visible macOS import.

The capture helper `evidence/scripts/original_ui.c` uses Win32 messages and
enumerates only windows owned by `Yipe!III.exe`; it does not edit saves or
process memory. Build instructions are in `docs/TOOLS.md`. A follow-up launch
reached the original startup dialog, but the current computer-use interface
could not select the Wine process, and PrintWindow returned black pixels.
No new visual or restart-parity claim is based on that attempt.
The follow-up Wine session ended with `wineserver crashed` after interruption;
this is a runner failure, not evidence of an original gameplay defect.
