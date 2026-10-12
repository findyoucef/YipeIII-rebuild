# Puffy cloud silhouettes — 2026-10-07

The user reopened cloud refinement and requested the familiar clip-art cloud
shape, describing the previous noise silhouettes as amorphous and slug-shaped.

Replaced the noise-generated outline with separate cloud icons: rounded crowns,
smaller side puffs and mostly flat bases. Each icon is roughly 54–70 native pixels
wide, with varied size, handedness and crown placement. Staggered, jittered
placement leaves gaps between clouds, including across the wrapping texture.

The existing 7%/14% shade, gentle interior density variation, one-pixel fringe,
smooth drift, camera anchoring, shaft attenuation and hero/dust light sampling
remain. Cloud construction happens once and uses an independent fixed cosmetic
seed. Removed the unused noise helper. The production change is confined to
SunShafts; the graphics runner additionally exports the raw alpha silhouette mask.
The scoped simplicity review found no further issues.

Checked local Release build: zero warnings/errors. **569 gameplay/input checks
and 1,394 graphics checks pass**, including existing shape-independent cloud,
sunlight, actor, weather, profile and state/RNG checks. The suite rendered 360 tour
frames. No new assertions were added for this cosmetic change.
Checks used isolated saves and profiles; personal files were not written by this task.
No package was rebuilt.

Final graphics run:
`build/puffy-clouds-graphics/yipe-graphics-20261007-223746-2584739f`.
The clouds-only in-game capture was inspected for recognizable silhouettes and
separation. The result is ready for user visual review, not recorded as approved.

[Subtle](map-017-subtle.png), [Strong](map-017-strong.png),
[clouds only](map-017-clouds-only.png), [combined desert](map-001-combined.png),
[animation](puffy-clouds.mp4), [raw alpha mask](cloud-silhouettes.png),
[build log](build.log), [core/input log](core.log), [graphics log](graphics.log),
[source hashes](source.sha256).
