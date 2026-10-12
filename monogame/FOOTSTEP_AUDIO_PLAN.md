# Player footstep audio

Snow-biome extension: three recorded CC0 Kenney snow crunches are added to the
existing five material families. Snow/grass selection follows actual snow coverage
at the planted feet, including melted edges and secret aliases. Current local
verification passes 613 core and 2,278 graphics checks. [Snow plan/evidence](SNOW_BIOME_PLAN.md).

Date: 2026-10-08. Status: the initial footstep behavior is implemented and verified.
The user rejected the synthesized sound quality; the replacement uses recorded
CC0 assets. [Sources and credits](Yipe.Desktop/Content/Audio/Footsteps/README.md)
and [replacement checks/previews](evidence/2026-10-08-recorded-footsteps/README.md).
Replacement verification: checked local Release passes with zero warnings/errors;
608 core and 2,240 graphics checks pass. The latest local build is open on the
existing full adventure, paused after load. No package was rebuilt.

Add quiet, short footsteps for grass/dirt, stone, sand, wooden bridge decks and
visible water. Play one contact at the end of an accepted 250 ms walking step,
matching its planted destination. Use three different recorded contacts per surface
and the existing sound and volume settings. Keep gameplay, RNG and saves unchanged.

- Load fifteen CC0 16-bit mono WAV recordings once at audio initialization:
  Nox_Sound's dirt, tile and water steps; Kenney's wood contacts; Fantozzi's sand
  contacts. Trim silence, balance levels and fade short tails to fit within the
  250 ms walking cadence. Ship sources, credits and the license beside the assets.
- Give footsteps a separate quiet voice. Important original effects stop that
  voice immediately; walking never replaces an original effect or queues steps
  behind a spell, door or monster sound. Muting, volume zero and focus loss stop
  the voice. Pause, maps, storage and overlays also stop it.
- Mark walking at the accepted player-command boundary. Idle time, blocked
  movement, turns, teleports, loading, map snaps and passive platform travel do
  not produce contacts. A paused unfinished walk resumes to one eventual contact.
- Use the renderer's real water and sand masks at the planted feet. Bridge decks
  take precedence over the water beneath them. Roads and cave stone use taps;
  packed earth uses the softer contact. Secret variants keep the same audible
  material as their visible terrain. Refresh cached terrain masks before sampling.
- Verify recorded WAV compatibility/boundaries, distinct variations, source/license
  provenance, master-volume behavior,
  audio priority/lifecycle on the real device, frame-rate-independent contact
  timing, diagonal walking, freeze boundaries and terrain-mask classification.
  Run relevant existing core/graphics checks using isolated saves and profiles.
- Retain WAV previews, evidence and source hashes. Open the latest local build
  on the recovered personal level-18 adventure after verification. Preserve the
  running old build and personal files; development does not repackage the app.

The implementation extends `GameAudio`, the current walking update and the two
renderer surface samplers. The synthesized PCM factory was removed. Recorded
WAVs use the existing `SoundEffect.FromStream` loader and one pending-walk flag;
there is no new audio framework or change to `PlayerMotion` or core rules.
