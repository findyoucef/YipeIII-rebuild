# Trailer capture

The trailer uses the current desktop renderer, authored maps, normal
movement, ice traction, raft support and combat presentation. The capture creates
independent adventures in `build/trailer-20261020-v3/capture-save`; it does not read or
write personal saves. Scanlines are disabled; other visual settings are retained.

From the repository root, with .NET 10 and FFmpeg on PATH:

```sh
dotnet run --project monogame/tools/TrailerCapture -c Release -- build/trailer-20261020-v3
python3 monogame/tools/edit_trailer.py
open monogame/evidence/2026-10-20-trailer/review.html
```

Use `--shot=02-maynard` to replace one clip, or `--stills` for first-frame checks.
Run capture with the game window unobstructed and avoid keyboard input during it.
This is an offline, fixed-step capture, not a performance benchmark. Reflection
follows the existing GraphicsChecks approach so capture needs no production hooks.

The edit script checks clip lengths/formats, mixes shipped music and recordings,
uses combat cue timings read from the captured presentation and footfalls recorded
at completed steps with the actual ground material, and exports a 1080p60
H.264/AAC MP4 with nearest-neighbor scaling. The game view is captured at 1280x720
(4x the 320x180 scene); the final 1920x1080 scene has six output pixels per source
pixel. Interface text and the custom endcard retain their own pixel font sizing.
Nine cross-dissolves of 20 frames each turn 45 seconds of source footage into a
42-second edit. Audio cues follow the overlapping shot starts. The Fire Cave approach
and fight are one uninterrupted eight-second take: movement into the authored Flame
Flier at (7,1) starts the actual encounter. No battle is injected into the session.

Raw clips, isolated saves and intermediate audio remain in ignored `build/`.
Walking routes start their next step before drawing the previous step's completion
frame, removing the old capture-only standing gaps. Per-shot motion traces and
footstep cues are saved alongside the clips. The capture fails if a scripted route
is blocked or interrupted by an unintended menu or encounter.
The review MP4, poster, timeline and credits are under
`monogame/evidence/2026-10-20-trailer/`. The latest export is `yipe-iii-trailer-v3.mp4`;
the `v1` and `v2` MP4s are retained for comparison. This is a cut for review, not a release
announcement. Android is labeled as in development; no release date is asserted.
