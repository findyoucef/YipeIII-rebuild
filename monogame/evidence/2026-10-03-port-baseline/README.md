# Port baseline and random generator verification

This record completes plan tasks P0.1 and P1.1 on 2026-10-03 local time
(America/Denver). Log timestamps use UTC and may show 2026-10-04. Verification
ran on Apple Silicon macOS with .NET 10.0.300 and the pinned MonoGame DesktopGL
dependencies. The approved town rendering and movement settings were unchanged.

The repository HEAD was `349fffe71e052dd4e011f1fe09c0e3080f5bb723`; the MonoGame
tree was still untracked. The commit alone does not identify these sources.
[Source fingerprints](source-sha256.json) identify the code, build inputs and
content used by this verification. No C++ gameplay implementation was modified.

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Locked dependency restore | Passed for all four C# projects | [Restore log](restore.log) |
| Release build with checked arithmetic | Zero warnings and errors | [Build log](build-checked.log) |
| Core, input, movement and saves | 64 checks passed, including 624 C++ town movement cases and 8,192 RNG output/state records | [Check output](core-checks.log) |
| Graphics without recording | 139 checks and 360 rendered tour frames passed | [Normal run](normal-graphics.log) |
| Graphics with recording | 139 checks passed when the built executable ran from `/tmp`, outside the checkout | [Recorded run](recorded-graphics.log) |
| Recording format | 360 frames, 1280 × 720, 60 fps, six seconds | [FFprobe metadata](video-probe.json) |
| Save isolation | Existing user save fingerprint unchanged across both graphics runs; each runner used its own save directory | [Isolation result](save-isolation.txt) |
| Runner arguments | Help returned 0; missing output argument and unknown option returned 2 | [Argument results](runner-arguments.json) |

The independently compiled C++ generator emitted 1,024 draws for each of eight
seeds. The C# digest matches every emitted value and post-draw state. Separate
checks cover the original default sequence, unchanged initial state, and resumed
continuation. [Reference metadata](random-reference.json) records the seeds,
final states, source hashes and trace digest. Reproduction instructions are in
[Yipe.Tests](../../Yipe.Tests/README.md).

## Reproduce

From the repository root:

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

For the outside-checkout recording check on this desktop platform:

```sh
port_root="$PWD"
(cd /tmp && "$port_root/monogame/Yipe.GraphicsChecks/bin/Release/net10.0/Yipe.GraphicsChecks" --record --output "$port_root/build/graphics-checks")
```

Keep the graphics window focused. Recording needs FFmpeg on `PATH`; normal
checks do not. [Runner documentation](../../Yipe.GraphicsChecks/README.md)
describes outputs, exit codes, display requirements and test-only VSync behavior.

The full video and per-run captures were working outputs under
`build/graphics-checks/`; they are not required repository inputs and can be
regenerated. This record preserves small logs, metadata and a
[fresh bridge view](bridge-reflection.png). The earlier
[approved preview evidence](../2026-10-03-preview/graphics-checks.log) remains intact.

## Scope limits

These results verify the current town preview, the recovered graphics runner
and the original random generator. Character stats, inventory, world transitions
and the rest of the campaign are not yet ported. Physical controllers and other
desktop/console platforms were not exercised. The existing C++ completion
artifacts are still missing from this checkout; no full-game replay or original
executable comparison is claimed by this run.
