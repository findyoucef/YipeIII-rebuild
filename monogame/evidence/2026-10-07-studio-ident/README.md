# icosium labs Casbah-inspired pixel-art ident — 2026-10-07

The port/remaster developer's company is now credited with a studio logo before
normal title startup and in About. The generated transparent pixel-art emblem
uses stepped white houses, a narrow stair alley, an arch and sea-blue accents,
as requested for a Casbah-inspired identity. Ivory lettering stays readable on
midnight navy. [Source asset and exact prompts](../../Yipe.Desktop/Content/Branding/README.md).
Built-in image_gen generation/edit tools were used; no Python image editing.

The ident runs for 3.2 seconds with 0.45-second fade-in and 0.55-second fade-out.
Standard Confirm, Back and Pause skip it without selecting a title choice. The
active-window guard freezes its timer while unfocused; startup does not move the
character, advance world timers or consume gameplay RNG. Normal title handoff
resets pause; direct save loading and graphics smoke continue to bypass title.
It renders on the 320×180 canvas with point sampling and shared integer scaling.
World miniature, vignette and scanline effects do not affect the studio mark.

Zero-warning/error checked Release build. All **558 gameplay/input checks** and
**1,246 GPU checks** pass. New checks cover clock advancement, automatic title
handoff, each skip action without activation, rendered logo and unchanged full
save/RNG state. The [actual splash capture](splash.png) was visually inspected.
The full graphics suite also retains existing map/effect/letterboxing checks.
Original game attribution is retained alongside the port credit in About.

Updated archive: `YipeIII-MonoGame-0.1.0-preview.5-macOS-arm64.tar.gz`

SHA-256: `5686f98741f4c6be1dc9409502054705783f19aa744f79250a4470fa6ef14c30`

Extraction passes hashes, native/bundle signatures, dependency checks, isolated
selected-save graphics startup and macOS LaunchServices. The package includes
`Content/Branding/icosium-labs.png`: **295 manifested files** and
**82 content files**. [Report](package-report.json).
The updated app is extracted under `build/monogame-packages/preview.5/`.
The running personal game has not been restarted or modified.

## Caption cleanup

At the user's request, preview.6 removes the PORT / REMASTER BY heading.
The opening screen displays the logo and skip hint. About and bundle attribution
use the company name without that label. This text-only change builds with zero
warnings/errors and passes extracted package/startup verification; see
[caption package report](caption-package-report.json). The earlier 558/1,246
checks remain the studio implementation baseline and were not repeated for copy.
