# Hell crispness and Gatekeeper arch — 2026-10-04

The October 5 follow-up below supersedes this first pass's remaining blur and
painted Gatekeeper arch.

The previous limited-palette update was still too soft. The user also identified
the Gatekeeper arch, alternate/Hell A4 / 6,1, as blurry. Inspection showed that
the final miniature and heat passes softened the native pixel artwork.

Hell maps 117–120 now composite one-quarter of the previous miniature blur,
retaining subtle depth while exposing more original detail. Heat refraction uses
point sampling at four-times-native presentation resolution, preserving pixel
colors while retaining quarter-native-pixel motion. Lava firelight reaches twenty
native pixels instead of thirty, with reduced peak opacity. The red palette,
rounded walls, rock artwork, arch geometry, readable path and heat motion remain.
Other regions keep their exact prior effects. Settings and save data are unchanged.

- Gate: [before](hell-a4-defaults-before.png) / [after](hell-a4-defaults-after.png).
- Path: [before](hell-path-defaults-before.png) / [after](hell-path-defaults-after.png).
- Generated region: [before](hell-118-defaults-before.png) / [after](hell-118-defaults-after.png).
- [Gate with miniature off](hell-a4-sharp.png).
- [Animated preview](hell-motion.mp4), Strong miniature, diagnostic scanlines off.

Default comparisons use scanlines on and Strong miniature. All four regions and
the gate/path were visually inspected. The gate remains slightly affected by
the intentionally retained depth effect; it is no longer heavily softened.

## Verification

Checked Release: **zero warnings/errors; 982 graphics integration checks pass**
across 115 maps and ten Hell layouts, with 360 tour frames. The additional check
verifies that Hell refraction only samples existing source colors. Existing
checks cover fog, door crowns, lava containment, monster/ruin overlap, animation,
pause, miniature transitions, heat, state/RNG preservation and terrain caching.

Of 763 paired PNGs, 182 changed, all Hell views (including the generically named
generated-map views, which depict map 118). The other 111 full maps, all Earth
and puddle views, and all nine prior reflection captures are byte-identical.
All 26 core files and four personal save/profile files retain pre-work hashes.
Tests use isolated storage. The live game was not closed or navigated.

Baseline: `build/graphics-checks/yipe-graphics-20261005-053357-bfbfa487`.
Final: `build/graphics-checks/yipe-graphics-20261005-054934-a046deb2`.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

[Build log](build.log), [graphics log](graphics.log), [capture comparison](capture-comparison.json),
[source hashes](source-inputs.json), [preservation hashes](preserved-core-and-saves.json)
and [task diff](changes.patch) are retained here.

Copilot Bridge provided bounded [effect analysis](copilot-analysis.txt), independently
checked against the draw path and verified by the primary agent's build, tests
and visual review. Simplicity review covered all six changed code files: four
production files adjust existing effects through per-call arguments, and two
check files update reflective calls and verify source-color preservation. No new
rendering passes, textures, assets, saved settings or state fields were added.
A second [diff review](copilot-review.txt) suggested repeating the source-color
assertion for all four IDs. The primary agent confirmed the same per-call map
range selects the sampler for each ID; all four already receive animation and
state checks plus visual captures. The shared sampler assertion stays on A4.
No remaining task-diff findings.

This first pass was built while the prior game remained open. It was subsequently
launched, but the user reported that the difference was insufficient.

## Follow-up: native-pixel gate and no Hell blur — 2026-10-05

Replaced the enlarged painted Gatekeeper arch with native-pixel stone blocks,
dark joints, restrained chips and clear highlights. The entrance remains at
alternate/Hell A4 / 6,1: its two supports sit inside the authored wall cells,
and its crown continues above the north map edge. The central route remains clear.
The area map uses the same new gate artwork.

Hell maps 117–120 now bypass miniature blur entirely, including when Strong is
selected. Contact shadows remain; the zero-blur path directly enlarges the world
with point sampling and skips both blur passes. Heat continues to move the image
with point sampling. Other regions retain their existing miniature effect.

- Gate with normal settings: [before](gate-rework-before.png) / [after](gate-rework-after.png).
- [Path with normal settings](no-blur-path.png).
- [989-check graphics log](gate-rework-checks.log) and [source fingerprints](gate-rework-source.sha256).

Checked Release compilation succeeded without warnings/errors. The existing
graphics runner passed 989 checks and rendered 360 tour frames. New assertions
cover exact native-pixel preservation before heat and source-color preservation
during heat in all four Hell regions. Existing tests retain door crowns, fog,
generated layouts, monsters, pause, map behavior and gameplay/RNG coverage.
Of 764 paired captures against the previous crispness build, 183 changed, all
Hell-related; all 111 non-Hell full maps and the other biome captures match.

Final run: `build/graphics-checks/yipe-graphics-20261005-061208-2b592ec5`.
The live process started at 23:58, before the gate rebuild at 00:04; it could not
show that artwork. It was closed from its saved pause screen and replaced with
the new build; the saved adventure was loaded and resumed. The
[updated live window](live-updated.png) is open. Personal adventure, map journal,
profile and preview bytes were [preserved](gate-rework-saves.sha256).
Visual acceptance is pending; the remaining port work resumes at P5.6.
