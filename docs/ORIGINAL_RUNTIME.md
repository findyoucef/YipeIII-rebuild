# Original runtime on Apple Silicon

Recorded on 2026-09-15. This setup is for observing the supplied Windows executable. It is not a rebuild dependency.

## Configuration

| Component | Recorded value |
| --- | --- |
| Host | arm64 macOS 26.6.2, Apple M5 Max |
| Translation | Rosetta 2 installed |
| Runner | Whisky 3.7.0 (build 55), maintained `frankea/Whisky` fork |
| Whisky library archive | release `v3.1.1`, SHA-256 `01f3a1b43b98065fe20c529c1023b61dd79a6d2ad93bba6040865f646481ccf3` |
| Bundled Wine report | `wine-11.0` |
| Bottle | `YipeIII-Stock-Test` |
| Prefix | `~/Library/Containers/com.franke.Whisky/Bottles/B40A1FF7-A751-444D-83D3-4B50BC3D23CA` |
| Bottle Windows version | Windows 10 |
| Graphics | Whisky recommended backend; DXVK disabled in a default bottle |
| Working directory | `/Users/baouchiy/Downloads/Yipe! III` |

The bottle was created after installing the stock `v3.1.1` library archive. This matters because a bottle first bootstrapped with a different Whisky runtime did not behave consistently after the runtime was replaced.

## Launch command

```bash
env \
  WINEPREFIX="$HOME/Library/Containers/com.franke.Whisky/Bottles/B40A1FF7-A751-444D-83D3-4B50BC3D23CA" \
  WINEFSYNC=0 WINEMSYNC=1 WINEESYNC=1 \
  WINE_ENABLE_POSIX_SIGNALS=1 WINE_DISABLE_NTDLL_THREAD_REGS=1 \
  "$HOME/Library/Application Support/com.franke.Whisky/Libraries/Wine/bin/wine64" \
  '/Users/baouchiy/Downloads/Yipe! III/Yipe!III.exe'
```

The fresh bottle bootstrapped successfully. A direct Wine launch retained `Yipe!III.exe`, and the visible window reached the rendered main menu. The executable was started from the directory containing `Yipe3Items.txt` and `MonsterSounds`.

`WhiskyCmd run` invokes Wine through Windows `start`; its `--follow` result described the launcher process rather than the lifetime of the separately started game. That caused the earlier false conclusion that the game had exited.

## Verified startup observation

The captured window shows the Yipe! III title image and the selectable `New Game`, `Load Game`, and `Quit` entries, followed by `Kevin Kinell © 2003`. The capture is stored as `evidence/reference-captures/R00-start-menu.png` with SHA-256 `204311ebf11444c427e42d9265bd0ecafe927773ee7fd632b192fd0d2adf885b`.

## Save capture checkpoint, 2026-09-22

The interrupted run used the Wine-native `evidence/scripts/original_ui.c` helper to capture one Combat/Medium character, a southward move, and four actual `.yp3` files with Win32 control dumps. The existing importer accepts every archive, and native conversion and the headless SDL import path pass. See the [R08 record](../evidence/original-runtime/r08/README.md) for hashes, byte differences, and evidence limits. R08 remains partial: the reported restart has control dumps but no independent process-lifetime or visual record, and no later-game checkpoint is captured.

## Current limitation

A fresh follow-up launch reached the startup dialog in an isolated build directory. The current computer-use inventory did not expose Wine, selecting `wine64-preloader` failed, and the helper's PrintWindow capture was black. No new visual route or restart result is claimed from that attempt. A prior bottle initialized under a beta library faulted after debugger attachment; that result is recorded only as an environment failure.

The follow-up Wine session also reported `wineserver crashed` after interruption. This remains runner evidence, not a game-behavior result.

After the host app has working Accessibility control, repeat the cases in `REFERENCE_CASES.md` with this fresh bottle. If the game faults during ordinary interaction, retain the Wine log and use a Windows virtual machine for authoritative reference runs.

E081/E082 subsequently executes isolated original damage, successful flee,
generated map-entry and other data leaves under Wine. It also composes failed
flee and consumable-purchase arithmetic from unchanged leaves plus disassembly.
The [bounded record](../evidence/persistence-parity/README.md) identifies exactly
which original routines ran and which UI-dependent paths remain excluded.
E082's eighteen restart cases and five continuations run the rebuild's native
Open path; they are not new original GUI observations or `.yp3` captures.

E084 records four new production-created rebuild completions on the E081
corrected combat build. The [commands and replay evidence](../evidence/corrected-completion/README.md)
are automated native rebuild observations; they add no original GUI capture
and do not change the R01–R10 reference-case statuses.
