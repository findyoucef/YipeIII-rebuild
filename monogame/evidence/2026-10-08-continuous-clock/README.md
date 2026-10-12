# Continuous day/night time

The user requested that time continue while the game is paused or suspended.
Auto now advances from elapsed UTC timestamps at the start of each update,
before simulation pause/focus/storage gates. Paused scenery changes time of day;
after suspension, the first update applies the elapsed interval once. The cycle
remains twenty minutes. Fixed Day, Dusk and Night keep their selected look and
suspend Auto's phase, as before.

The production change adds one timestamp field to `TownGame`, initializes it
after loading preferences, moves phase advancement out of active exploration,
and updates Help. No clock service, background task, timer thread, gameplay
simulation catch-up or persistence-format change is added. Negative UTC intervals
are clamped to zero, and long intervals wrap within one cycle. Time while the
process is closed remains excluded.

Checks exercise:

- Active and underground progression independent of rain.
- Pause, action panels, maps, in-flight saving/loading, ident and live battle
  menus: daylight advances while the gameplay/RNG snapshot and actor position
  remain unchanged.
- Inactive-app behavior through MonoGame's actual platform activation state.
- Simulated **130-second** and **3,607.75-second** suspension gaps with only
  **0.1 second** of frame time: elapsed wall time advances the phase, night lighting
  appears immediately while paused, multiple cycles wrap and the next frame
  does not repeat the gap.
- Backward wall-clock adjustment, fixed-mode suspension and return to Auto.

The remaining presentation checks hold fixed Day after the Auto fixture, so
their color comparisons stay deterministic across different runner speeds.
Existing native palette, underground lighting, readable UI, recorded footsteps,
actor travel and combat checks remain in the full suite.

[Build log](build.log), [core log](core.log), [graphics log](graphics.log),
[personal save/map/profile audit](personal-files.json). Testing uses isolated
profiles and saves. Development builds locally without repackaging.
Checked Release passed with zero warnings/errors, **608 core checks** and
**2,247 graphics checks**. All nine audited personal save/map/profile files
remained byte-identical across verification.

The scoped diff was reviewed against the task baseline: existing phase evaluation
and profile persistence are reused; gameplay's elapsed-frame handling is unchanged.
No running game was closed by the agent. After the previous session had closed,
the updated local build was opened on the same full adventure, with an additional
backup of the save and map/profile sidecars. It is paused after load while Auto
time continues. Startup log is clean. [Launch record](launch.json).
