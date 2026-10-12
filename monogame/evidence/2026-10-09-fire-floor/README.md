# Fire Cave floor — 2026-10-09 UTC

The user preferred the red-hot wall/room treatment and requested a better floor.
The eleven Fire Cave rooms now use irregular cooled-lava plates, shallow folded
flow ridges, fine ash/grit and sparse sealed ember seams. These replace the broad,
smooth patches while remaining quieter than the glowing walls. Existing long
unstable-floor cracks are drawn last and remain readable.

- Open upper C5/map67: [before](gameplay-67-before.png), [after](gameplay-67.png).
- Entrance, upper B6/map72: [before](gameplay-72-before.png), [after](gameplay-72.png).
- Deep C6/map113: [before](gameplay-113-before.png), [after](gameplay-113.png).
- [Unstable-floor room73](map-73.png); native and gameplay captures cover all eleven rooms.

Only the non-stone terrain generation in `WorldFire.cs` changed. It uses the
existing palette, cache and world-coordinate seeds. The red walls, magma,
room-wide glow, audio, actors and gameplay retain their prior code.

Checked Release builds with zero warnings/errors. All 207 existing focused GPU
checks pass, covering the eleven rooms, palette, stable rendering, hidden/secret
terrain, overlaps, cache invalidation, lights/doors, heat, ambient colour, actors,
audio and full save/RNG preservation. Wall/magma core pixels and all eleven
isolated local-light captures are unchanged. [Comparison](wall-light-comparison.json).
No new tests were added for this art-only correction. The previous shared-lighting
revision passed 2,615 full checks; that full suite was not rerun for this narrower
change. The personal save/journal/profile changed during the session; these
observations are retained without restoring or replacing the updated files.
The renderer checks used isolated saves.

[Review](review.json), [scoped diff](changes.patch), [hashes](source-hashes.json),
[personal file observations](personal-files.json) and build/focused logs are retained.
Local build only; user review of the floor is pending.
