# Hell A4 entrance correction — 2026-10-04

The user reported a mismatched fire block beside A4 / 6,2. A capture of the
running game confirmed unsupported flames on the two cells beside the approach.
A4 is map 117; 6,2 itself is the walkable path. The affected fire-wall cells are
5,2 and 7,2.

The recessed arch ends at native y=62, but its special terrain exclusion still
covered row 2 (y=64–95). This suppressed lava and rocks a full row beyond the
arch. `WorldHell.cs` now restricts that exclusion to rows 0–1 and gives both
approach wall ends low crags with small flames. The path, original hazard cells,
damage and Gatekeeper logic are unchanged. Production changes are confined to
the existing ground/scenery methods; no new assets, state or helpers were added.

- [User's live view before](live-a4-6-2-before.png): captured without changing
  their game; unrevealed cells remain dark.
- [Corrected view from 6,2](a4-6-2-after.png): isolated diagnostic scene with the
  map revealed, rendered through the actual frontend.
- `entrance-before.png` / `entrance-after.png` and `map-117-before.png` /
  `map-117-after.png` compare matching camera/time/scene fixtures.

Checked Release build: zero warnings/errors. All **743 graphics checks** pass,
including ten Hell layouts, visibility, animation, doors and gameplay/RNG
isolation. The runner now retains the reported 6,2 viewpoint as an extra
diagnostic capture. Only map 117 changed among 115 map captures; the five
reflection captures also remain identical. All 26 core files, the personal
preview and profile retain their pre-work hashes. The personal adventure and map
journal changed at 18:33:32 while the user's existing session was open; their
before/after hashes are retained without claiming they are identical. The checks
use isolated saves. Logs and comparisons are saved here. Visual review confirmed
both wall ends, the open path and visible guard.

Final run: `build/graphics-checks/yipe-graphics-20261005-003412-819739e2` (UTC).
Baseline: `build/graphics-checks/yipe-graphics-20261005-002127-16737949`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The user's existing process was left untouched during work and had closed by
completion. The corrected build was then launched. Resume other port work from
`PORTING_PLAN.md`.
