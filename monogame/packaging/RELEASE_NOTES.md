Yipe! III remastered by **icosium labs**, built with MonoGame. This preview
includes the complete campaign, remastered environments and characters, music
and ambience, title and pause menus, separate adventure saves, and the new
pixel-art app icon.

## Download and play

- Download `YipeIII-MonoGame-<version>-macOS-arm64.tar.gz` from the release assets.
- Requires an **Apple Silicon Mac running macOS 26 or newer**. The .NET runtime
  is included; no .NET or Homebrew installation is needed.
- Extract the archive and open **Yipe III MonoGame.app**, or move it to Applications.
- This preview is ad-hoc signed, **not Developer ID signed or notarized**. If
  macOS blocks it, use System Settings → Privacy & Security → Open Anyway.
- Optional integrity check: download the matching `.sha256` into the same folder
  and run `shasum -a 256 -c YipeIII-MonoGame-<version>-macOS-arm64.tar.gz.sha256`.

The title screen offers New adventure, Load game, Options and Quit. Arrows/WASD
move, Enter opens actions and Escape opens pause. Sound options have separate
music and effects sliders; adjust them with Left/Right.

## Saves and verification

Saves live in `~/Library/Application Support/YipeIII-MonoGame/`. New adventures
receive separate slots. Keep both `.y3m` and `.map.json` when moving an adventure.
The older C++/SDL preview uses separate saves; retain originals when importing.

GitHub Actions builds this package from its release tag after locked dependency
restore, core/persistence checks and an earned campaign comparison with the
original C++ observations. The extracted app is checked for file integrity,
signatures, bundled native dependencies, icon sizes, launch/startup, display
changes and save continuation using its bundled runtime. Verification logs and
captures are retained in the matching **MonoGame macOS** Actions run.

Physical controllers, Intel Macs, older macOS and other operating systems remain
unverified. Local visual/performance results are documented in
`monogame/evidence/2026-10-12-release-readiness/`; they are not performance
certification for other hardware. The additional historical campaign variants
listed there remain outside the verified full-route comparison.

Original Yipe! III by Kevin Kinell. In-game Help & credits and bundled notices
retain the original contributors and the artwork, music, sound and technology
attributions.
