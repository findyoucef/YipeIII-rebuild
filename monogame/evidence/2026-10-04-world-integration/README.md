# World integration checkpoint — 2026-10-04

The graphics runner passed **238 checks** on Apple Silicon macOS, including the
approved Maynard rendering, native sprite/palette compilation, 250 ms movement,
reflections, smooth miniature effect, scanlines, complete saves, cave/surface
transitions and generated-map loading. Its town tour now dismisses actual
interactions. The shop exit check verifies the original encounter RNG draw;
render-only checks preserve RNG. The separate desktop smoke test passed.

The full core suite still passed **500 checks** after frontend integration,
including the earned opening replay and separate-process restart. See the prior
[core evidence](../2026-10-04-p234-core/README.md) for reference fixtures and scope.

The source fingerprint is a working checkpoint, not a commit. The latest run also
covers creation through controller-style overlay commands, inventory cancellation,
unavailable spell/SP preservation, all packaged WAV files loading on the audio
device, and continuous-walking/paused-overlay timer behavior. It includes the font,
layout, ability-feedback and creation-RNG lifetime follow-ups. The core run passed
again after adding non-blocking sound events to Adventure; they do not change the
complete opening state/RNG checkpoints.

Reproduce from the repository root:

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
dotnet run --project monogame/Yipe.Desktop -c Release --no-build -- --smoke-test /tmp/yipe-smoke.png
```

The standalone smoke path now disables VSync, as the existing graphics runner
already does: the first attempt stalled inside SDL's Cocoa_GL_SwapWindow condition
wait on this desktop. Production VSync is unchanged. The stalled test process was
terminated and the corrected smoke test completed.

## Scope still open

This is **not** earned frontend/campaign completion. The new overlays are wired,
but creation/combat/spells/casino/controller paths need their own integration
coverage. The new world renderer needs all-map/tile review, proper coastal and
terrain edge treatment, all portrait variants, and animation polish. The atlas checks now cover all 44 monster/NPC sprites and sixteen props; padded
imagegen revisions fixed a bat wing, Flame Beast and portal/lever overlap. All
native cells are nonempty, palette-correct and have a transparent border. Actors
and props render at native size (double size for 2x2 monsters). Audio IDs, aliases, interruptions and key gameplay events are wired; playback,
all remaining event cues and audible playback still need further verification.
High scores and score codes now match 730 independently compiled C++ records
(hash `b3b130d150f8b7f952f4b6afc162d7c589ba9a5f6b44575218dfab8a882495f9`).
`scores_reference.cpp` covers ties, first finishers, difficulty truncation, wrapped
defeat counts, score-code suffixes, all sound IDs and monster/spell aliases. The
`verify-gameplay-references.sh` script reproduces the scores trace alongside the
five earlier gameplay references. The frontend score menus are verified too.

DesktopProfile separately persists preferences and 45 score records. Tests cover
reopening, missing defaults, atomic cleanup, and preserving malformed/newer files
without overwriting them. A failed early test exposed a missing InvalidDataException
catch; the corrected profile checks includes the correction. Fullscreen/input preference
coverage, full portrait choices, full campaign inputs and completion/restart proof
remain.

The included cave, surface and generated captures are diagnostic views; they do
not establish earned progression. No personal save is used by the runner.

Equipment labels match another **204 C++ records**, including the packaged
Yipe3Items.txt, complete embedded fallback and Silly Mode. The digest is
`0e4fe4123d2eec64d54a106ffaec14cd8115c387a24ac525fb366168c6d57802`.
`items_reference.cpp` and `--items-trace` reproduce them; the shared gameplay
reference script includes this suite. Parser tests cover comments, incomplete
catalog fallback, the 22-character label limit and unusual imported ranks.
Equipment overlays now expose accessory names and original AC/WC display values.
