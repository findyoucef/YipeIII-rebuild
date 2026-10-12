# Antigravity animation-art capability test — 2026-10-07

The user asked whether Gemini 3.8 Flash through Antigravity could generate the
animation artwork. A bounded test used `gemini-3.8-flash-low` to invoke the
installed CLI's `image-generator` specialist with the existing monster atlas
as the Mouth Man identity reference. The task authorized one generated walking
preview and no repository edits by the worker.

The specialist produced an actual 1024 × 1024 image in **54.4 seconds**, including
orchestration and generation. The [preview](mouth-man-walk-preview.jpg) preserves
the round ochre body, huge mouth, teeth and small-foot design reasonably well.
This confirms that the current Antigravity route can produce raster animation
art candidates. It does not establish average throughput for complete sheets.

The file was inspected visually and with `file`/`ffprobe`. It is an opaque JPEG,
with a painted checkerboard, black ground marks and sixteen walking poses in a
4 × 4 grid. The request was for four poses in a 2 × 2 sheet with real transparent
alpha and no ground. This candidate is **not accepted for production**. It needs
layout/transparency cleanup and native movement review before integration.
No animation was registered and the seven-monster/34-remaining counts stay valid.

An initial direct `generate_image` request returned tool unavailable. The local
tool inventory then exposed the `image-generator` specialist; that route
succeeded. Google's [official CLI release notes](https://github.com/google-antigravity/antigravity-cli/releases)
describe the same change from a direct tool to a built-in specialist. Flash
orchestrates the image request; the generated file's exact image-model ID was
not independently exposed, so no particular image backend is claimed here.

[Exact generation prompt](generation-prompt.txt), [worker result](generation.log),
[timing](generation-timing.json), [tool inventory](tool-inventory.txt),
[validation](validation.json), [source hash](source.sha256).
