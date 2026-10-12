# Player footstep audio

The hero now makes quiet contact sounds on completed 250 ms walking steps.
Grass/dirt, stone, sand, wooden bridge decks and visible water each have three
short original variations. The sounds follow the existing Sound and Volume
settings. A dedicated voice plays at 28% of the master volume; important effects
stop that voice, and footsteps never replace or queue behind an original effect.

Terrain sampling uses the actual planted foot pixel and current renderer masks.
Wooden decks take precedence over water beneath them. Dry river banks stay dry;
waterproof walking on visible water splashes. Sand follows the compiled footprint,
and secret terrain retains its visible material. Packed earth uses soft contacts.

Contacts occur once at the accepted player-walk boundary. Idle time, passive
movement and position restores remain silent. Pause, maps, overlays, storage,
focus loss, muting and zero master volume stop the voice. An unfinished paused
walk resumes to its eventual single contact.

## Sound previews

[Walking across five surfaces](walking-surfaces.wav) plays eight contacts per
material at the default in-game gain: grass/dirt, stone, sand, wood, then water.
The sequence includes short gaps between materials. Individual previews below
use the raw waveform gain so their texture is easier to inspect.

| Surface | Variations |
| --- | --- |
| Grass and dirt | [1](grass-0.wav), [2](grass-1.wav), [3](grass-2.wav) |
| Stone | [1](stone-0.wav), [2](stone-1.wav), [3](stone-2.wav) |
| Sand | [1](sand-0.wav), [2](sand-1.wav), [3](sand-2.wav) |
| Wood | [1](wood-0.wav), [2](wood-1.wav), [3](wood-2.wav) |
| Water | [1](water-0.wav), [2](water-1.wav), [3](water-2.wav) |

The PCM factory runs at audio initialization: 22,050 Hz, 16-bit mono, approximately
125 ms per contact. Deterministic noise and small frequency differences create
the variations without consuming gameplay RNG. Existing original audio assets
are preserved; no downloaded or third-party sound assets were introduced.

## Verification

The checked local Release build has zero warnings/errors. **608 core checks**
and **2,240 graphics checks** pass. New checks cover PCM boundaries, energy,
headroom and distinct variations; real-device playback, master gain and priority;
town/material masks, wet/dry banks and secret aliases; 24/60 Hz timing, diagonal
walking, passive travel, pause/resume, modal stops and cancelled contacts.
Complete saved-state and RNG comparisons pass.

The earned opening matches all 42 C++ checkpoints plus King return and a separate
process reload. The earned late route also passes empowered Gatekeeper, production save, a
separate-process restart, the final rescue and live Maynard ending. Tests use isolated saves
and profiles; the final local launch uses the recovered full adventure rather
than the earlier level-1 town demo.

- [Build](build.log), [core](core.log), [graphics](graphics.log),
  [opening/reload](opening.log), [finale/restart](late.log).
- [Source review](source-review.json), [personal file audit](personal-files.json).
- [Implementation plan](../../FOOTSTEP_AUDIO_PLAN.md).

Core source and `PlayerMotion` remain unchanged. The implementation adds one
pending-walk flag, two renderer material samplers and a small PCM factory in the
existing audio system. Development builds and runs locally without repackaging.

The latest local build is open on a protected full-adventure copy: level 18,
44 explored regions and the latest personal progress. [Launch verification](launch.json).
The source adventure and its matching map journal were backed up before launch.
A normal-input grass walkthrough was performed without saving over that source.
The recovered save was updated by its already-running game before the first
graphics run; those newer personal updates were retained. The audit uses the
latest saved checkpoint rather than restoring earlier hashes.
