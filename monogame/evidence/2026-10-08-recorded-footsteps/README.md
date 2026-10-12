# Recorded footstep replacement

The user rejected the initial procedural footsteps. The replacement uses fifteen
CC0 recorded contacts by Nox_Sound, Kenney and Fantozzi, with three different
recordings for each of grass/dirt, stone, sand, wood and water. The sound generator
was removed. [Exact sources, credits and license](../../Yipe.Desktop/Content/Audio/Footsteps/README.md).

[Listen to the louder audition sequence](audition.wav), or
[listen at the actual default in-game gain](walking-surfaces.wav).
Both run for 13 seconds, with eight contacts per material in this order:
**grass → stone → sand → wood → water**, separated by short pauses. The audition
is three times the in-game gain so the recordings are easier to compare.
Individual `grass`, `stone`, `sand`, `wood` and `water` `-0`, `-1` and `-2` WAVs
in this directory are the exact bundled recordings before runtime volume.

The files were converted to 22,050 Hz, mono, 16-bit PCM; leading silence and
DC offset were removed, levels balanced, and tails faded within 240 ms. The
source manifest includes download URLs, segment offsets, processing gains,
source hashes and final WAV hashes. The Nox_Sound input is the author's publicly
downloadable HQ MP3 preview. Credits and the CC0 text are copied with the assets
into local build/publish output.

The revision changes asset loading and removes synthesized PCM. The existing
surface masks, planted-contact timing, quiet voice, original-effect priority,
master sound controls and pause/focus/modal handling are retained. Gameplay,
movement, RNG and save code are unchanged.

Verification:

- Checked local Release build: zero warnings/errors. [Build log](build.log).
- All **608 core checks** pass, including WAV compatibility, boundaries,
  headroom, distinct recordings and source/license/hash checks. [Core log](core.log).
- All **2,240 graphics checks** pass, including real-device footstep playback,
  terrain/timing, mute/volume, priority and lifecycle checks. [Graphics log](graphics.log).
- Tests use isolated saves/profiles. Default, recovered-source and current full
  adventure `.y3m` files remain byte-identical across these checks.
  [Personal save audit](personal-files.json).
- The updated local build is open on the existing full adventure, paused after
  load. Startup/audio log is clean. The save and its map/profile sidecars were
  protected in an additional backup before launch. [Launch record](launch.json),
  [backup record](launch-backup.json).

The scoped production diff was reviewed against the task baseline: existing WAV
loading is reused; no audio framework, asset registry or procedural fallback was
added. Historical synthesized previews remain in `2026-10-08-footsteps` as evidence
of the superseded implementation. Development builds locally without repackaging.
