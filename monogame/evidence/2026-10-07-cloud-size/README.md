# Smaller cloud patches — 2026-10-07

The user preferred the revised cloud shapes/shading/motion but found the clouds
too large. Halved all four shape/detail scales from 128/64/32/16 to 64/32/16/8
native pixels, with matching offset changes. Raised the coverage cutoff from
0.57 to 0.60 to open gaps where neighboring patches formed large banks.

The existing 7%/14% shade, density variation, narrow fringes, fractional diagonal
travel, sun-shaft attenuation, hero/dust light sampling, controls and saved
preferences remain. The production diff is confined to the existing cloud
field's scale and coverage constants; no new helpers, state or tests were added.
The simplicity review found no further changes needed.

Local checked Release build: zero warnings/errors. The existing 562 core/input
checks and 1,354 graphics integration checks pass. The graphics checks cover
cloud and sun composition, smooth movement, pixel scaling, scope, pause, maps,
UI, profiles and gameplay/RNG isolation. Captures were inspected at both cloud
strengths on grass and with the combined effects on sand. No package was rebuilt.

Final graphics run:
`build/monogame-cloud-size-graphics/yipe-graphics-20261007-220115-eeafdd18`.

[Subtle](map-017-subtle.png), [Strong](map-017-strong.png),
[combined desert](map-001-combined.png), [build log](build.log),
[core/input log](core.log), [graphics log](graphics.log),
[source hash](source.sha256).
