# World water polish — 2026-10-04

The user found water outside the village noticeably plainer. World rivers,
lakes and cave pools now use the exact compiled village water texture, irregular
river widths, shoreline shallows, sparse six-Hz currents and small foam flecks.
Surface banks use the existing earthy/grass palette; cave banks stay subdued.
Full pools receive a slight irregular inset along their exposed edges.

`WorldWater.cs` caches one native-resolution map layer and pixel mask. It updates
only on map/water-layout changes, joins neighboring water tiles without an
internal shoreline, and keeps waves/reflections inside the same mask. The bank
texture spans neighboring tiles, but rendering still respects per-cell visibility.
Existing masks/colors are reused; no new raster artwork was generated. Village
rendering, collision, progression, gameplay RNG, timers and saves are unchanged.

- Checked Release build: zero warnings/errors.
- **633 real graphics checks pass**, including all 115 maps, tile/secret variants,
  town effects, world bridge reflections and timed routes.
- Four focused additions cover uninterrupted lake texture across a tile join,
  water removal invalidating the cache, deterministic shorelines, and animation
  changing only water pixels while freezing at the same scene time.
- `river-*.png`, `lake-*.png` and `cave-pool-*.png` retain before/after native-map
  captures. These are diagnostic revealed-map views; each was visually inspected.
- Final simplicity review checked the four rendering files and graphics checks.
  One shared map texture/mask replaces the flat atlas; no new service or generic
  abstraction. The reusable distance buffer handles connected banks once per
  layout change, and the existing reflection path consumes its mask.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

Gameplay code did not change, so the prior 502 core checks and full campaign
comparison were not repeated for this rendering-only edit. Physical controller,
other-OS and broader performance/release verification remain P5/P6.
