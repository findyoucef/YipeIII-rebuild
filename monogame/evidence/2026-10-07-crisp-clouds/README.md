# Crisp pixel cloud shadows — 2026-10-07

The user chose research option 1: crisp pixel silhouettes. This revision replaces
the initial broad, banded ovals with four differently shaped, scalloped cloud
clusters. Interiors have a single shade level, with a one-native-pixel
half-strength fringe. The shapes are authored in the existing effect code; no
third-party artwork is imported.

Cloud silhouettes remain intact across sun shafts. A cached blocked-sun mask
now removes sunlight under cloud interiors and attenuates it along their fringe.
The hero highlight, equipment glint trigger and illuminated dust sample that
same mask. Clouds Off restores the original clear-sky sunlight mask. Clouds
still work independently with shafts Off. The existing saved Off/Subtle/Strong
preferences, 10%/20% shade, two-pixel-per-second drift, scene-clock pause,
world anchoring and outdoor-only scope remain.

Local verification passed: checked Release build with zero warnings/errors,
**562 gameplay/input checks** and **1,353 graphics integration checks**. Updated
cloud checks cover flat shade/fringes and intact silhouettes blocking shafts
while retaining clear-sky light. Three additional checks compare covered and
clear hero/dust light samples at different camera positions/times, including
texture wrapping. Existing sunlight, actor, save/RNG, UI, map and effects checks
pass. Tests use isolated sessions and profiles. No distributable was rebuilt.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/monogame-cloud-silhouettes-graphics
```

Full graphics run:
`build/monogame-cloud-silhouettes-graphics/yipe-graphics-20261007-213435-27336be0`.

Reviewed captures: [Off](map-017-off.png), [Subtle](map-017-subtle.png),
[Strong](map-017-strong.png), [clouds without shafts](map-017-clouds-only.png),
[combined town](map-017-combined.png), [combined desert](map-001-combined.png),
[16-second animation](animation.mp4). Animation assembles 64 production-rendered
frames at four frames per second and is not a performance measurement.

[Build log](build.log), [gameplay/input log](core.log),
[graphics log](graphics.log), [verified source hashes](source.sha256).

The simplicity review covered the five changed source/test files against their
pre-revision copies. Production changes stay in the existing sunlight effect
and its two call sites; the added texture and CPU pixels cache matching blocked
light for rendering and actor sampling. No new rendering pass, effect framework,
gameplay data or per-frame mask generation was added. Visual acceptance of this
revised direction remains with the user.
