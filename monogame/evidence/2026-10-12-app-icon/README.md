# Yipe! III application icon — 2026-10-12

[Visual review](review.html) · [Artwork and regeneration instructions](../../packaging/icons/README.md)

Created original pixel-art knight/castle artwork with the built-in image_gen
tool. The transparent master and exact generation prompt are retained under
`monogame/packaging/icons`. Apple `sips` resized/converted the generated artwork;
`iconutil` assembled the ICNS. No game artwork or presentation settings changed.

## Integration

- MonoGame's existing `Icon.bmp` embedded-resource contract supplies the local
  build's running-window/Dock icon; no new runtime code or native bindings.
- The macOS packager includes `YipeIII.icns` in Resources and declares
  `CFBundleIconFile`. The standard ten representations cover 16–1024 pixels.
- The verifier extracts the ICNS and checks every representation's dimensions
  and alpha channel before accepting a package.

## Verification

- Locked restore and Release solution build passed: zero warnings/errors.
- Inspected 16/32/64/128/256 pixel exports, transparent BMP conversion, the actual
  local-build Dock screenshot (`local-dock.png`), and Finder's resolved bundle
  icon (`finder-icon.png`, exported via `NSWorkspace.icon(forFile:)`). macOS 26
  adds its own rounded backing to the bundle icon.
- Preview.8 extracted-package verification passed: 422 manifested files,
  208 content files, 15 native libraries, strict signatures, self-contained
  dependency loading, isolated import/save preservation, smoke launch and
  LaunchServices launch. Normal Game.Run startup, ident/title, resizing,
  fullscreen and return to a window also passed using the packaged runtime.
- [Package report](package-check/report.json) and startup logs/captures are
  retained here. The full gameplay/performance suite was not repeated for this
  asset/build-only change; [preview.7's release pass](../2026-10-12-release-readiness/README.md)
  remains the gameplay baseline. Physical-controller coverage remains unverified.
- Testing used an isolated adventure/profile in `build/app-icon`. The temporary
  local game was closed after the Dock capture.

## Deliverable

`build/monogame-packages/YipeIII-MonoGame-0.1.0-preview.8-macOS-arm64.tar.gz`

Extracted app: `build/monogame-packages/preview.8/Yipe III MonoGame.app`.

SHA-256: `ac1fbfdc4b9c7c24c4e3d568c1a84cfa377088e78d6ecdaa01f2c48af34c8b66`.

The package remains an ad-hoc signed Apple Silicon macOS preview. No commit,
push, public upload or new gameplay changes were made for this task.
