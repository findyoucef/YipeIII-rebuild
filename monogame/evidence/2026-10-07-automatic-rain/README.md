# Automatic rain — 2026-10-07

The user approved the rain's appearance and requested automatic weather.
Subtle and Strong now select the peak intensity of timed showers. Clear spells
last 5–10 minutes of active outdoor play; showers last 30–60 seconds, including
four-second transitions. Off disables rain and suspends its clock. Indoors,
menus, maps and focus loss also suspend it. The clock continues across outdoor
map changes and begins clear on app launch; it is not part of adventure saves.

Missing rain preferences default to Auto Subtle. Existing saved Off/Subtle/Strong
values remain intact; the latter two now mean automatic intensity. The user's
saved Strong choice is retained. No personal save or preference file was edited.

The existing Rain effect reconstructs a deterministic weather sequence from one
outdoor clock. No new random stream, scheduler class, event queue, save field or
particle state was introduced. Fractional density and the last drop's opacity
make transitions gradual while preserving full-strength rain exactly. The scoped
simplicity review found no further issues.

Validation: checked local Release build, zero warnings/errors; **569 gameplay/input
checks and 1,394 graphics integration checks pass**. New non-graphics checks sample
24 hours for timing ranges, variation and fade continuity, and preserve an explicit
Off preference. Graphics checks exercise the real timer, clear/shower/end frames,
fractional density, pause, indoor, menu, map and Off suspension, alongside the
existing rain pixel/shoreline/settings/gameplay isolation checks. The full suite
also renders 360 tour frames. All four personal save/journal/profile hashes are
unchanged. No package was rebuilt.

Final graphics run:
`build/automatic-rain-graphics/yipe-graphics-20261007-223033-e3325147`.
The Auto Subtle menu and arrival captures were inspected. The retained animation
shows two clear seconds, a four-second build-up and two seconds of full rain.

[Clear](automatic-clear.png), [arriving](automatic-arriving.png),
[shower](automatic-shower.png), [ending](automatic-ending.png), [menu](menu.png),
[arrival animation](automatic-rain.mp4), [build log](build.log),
[core/input log](core.log), [graphics log](graphics.log), [source hashes](source.sha256).
