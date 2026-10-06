# Hell east door clipping — 2026-10-04

The door at map 117, `(11,3)`, was visible from the lower eastern room while
`(11,2)` remained unexplored. The final fog mask covered the upper part of the
48-pixel sprite where it extended above its owning 32-pixel tile. The source
artwork was complete; fully revealed map captures did not reproduce the bug.

The world renderer and area map now restore just the known door's upper sprite
slice over fog. Transparent pixels preserve the hidden corridor; the door's
base retains its existing depth order. All three Hell door types retain their
original size and lighting. Opening a door removes the entire sprite immediately.
No changes to exploration rules, terrain, other art, gameplay or save formats.

- [Before](door-before.png) / [after](door-after.png): same partially explored room.
- [Area map after](area-map-after.png).
- [Gameplay with scanlines and Strong miniature](default-settings-after.png).

The new regression check failed on the prior renderer, then passed after the
correction. Checked Release build: zero warnings/errors. **752 graphics checks
pass**, including nine new checks for partial visibility, a complete crown,
area-map rendering, all three door tints, hidden-terrain preservation, gameplay/
RNG/visibility isolation and removal on opening. Reviewed the complete task diff
for simplicity: two renderer files, one shared drawing helper, no new state.

All 115 fully revealed maps and five reflection captures are byte-identical to
the previous flagstone/rounded-wall build. All 26 gameplay core files and four
personal save/profile files retain their pre-work hashes. Tests use isolated saves.

Failing reproduction: `build/graphics-checks/yipe-graphics-20261005-013542-b750c158`.
Passing run: `build/graphics-checks/yipe-graphics-20261005-013907-f6a1aaad`.
Logs, source fingerprints and comparisons are retained here.

```sh
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.GraphicsChecks -c Release --no-build -- --output build/graphics-checks
```

The user's existing game process was left running to preserve its session.
Save and restart it to load the rebuilt renderer. Resume other port work from
`PORTING_PLAN.md`.
