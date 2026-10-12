# Feathered cloud shadows — 2026-10-07

The user reopened cloud refinement and requested feathered edges. The existing
small cloud silhouettes now fade through a wider soft edge instead of a single
half-opacity pixel. Defined centers, gentle interior density variation, spacing,
shade strengths and motion remain. Terrain and sprite pixels stay sharp.

The production change is confined to mask construction in SunShafts. Two
weighted passes with a six-native-pixel radius filter the alpha mask once at
startup, wrapping across texture seams. No additional per-frame filtering or
settings were introduced. Ground shade, shaft attenuation and hero/dust light
sampling all use the same resulting mask.

Checked local Release build: zero warnings/errors. **569 core checks** and
**1,538 graphics checks** pass, including 360 tour frames. Existing checks cover
shade bounds, clear sky, smooth movement, world anchoring, wrapping, sunlight
attenuation, matching hero/dust samples, pause, settings, UI isolation, native
pixels and full gameplay/RNG preservation. No new assertions were needed for
this cosmetic change; the graphics runner adds a neutral-background capture
to make the feathering easier to inspect.

Town and desert captures, Subtle/Strong settings and the neutral mask preview
were visually inspected. Scoped simplicity review covered production, capture
and documentation changes with no remaining findings. Tests used isolated
files; personal save, map journal, profile and preview hashes were unchanged
through verification. No package was rebuilt. Visual approval remains pending.

Graphics run:
`build/feathered-clouds-graphics/yipe-graphics-20261008-001643-3b6c9f55`.

[Subtle](map-017-subtle.png), [Strong](map-017-strong.png),
[clouds only](map-017-clouds-only.png), [combined desert](map-001-combined.png),
[neutral preview](cloud-feathering.png), [raw mask](cloud-silhouettes.png),
[eight-second animation](feathered-clouds.mp4), [build log](build.log),
[core log](core.log), [graphics log](graphics.log),
[personal file checks](personal-file-checks.json), [source hashes](source.sha256).
