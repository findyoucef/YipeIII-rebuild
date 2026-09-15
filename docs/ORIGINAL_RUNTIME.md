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

## Current limitation

Startup is verified, but no gameplay reference case is complete. The Codex host still lacks working Computer Use/Accessibility control for the Wine window, so advancing through menus is pending. A prior bottle initialized under a beta library faulted after debugger attachment; that result is recorded only as an environment failure.

After the host app has working Accessibility control, repeat the cases in `REFERENCE_CASES.md` with this fresh bottle. If the game faults during ordinary interaction, retain the Wine log and use a Windows virtual machine for authoritative reference runs.
