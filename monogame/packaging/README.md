# MonoGame macOS preview packaging

The package is a separate Apple Silicon MonoGame desktop preview. It contains
the complete campaign, retro assets, audio, gameplay guide and self-contained
.NET 10.0.8 runtime. It does not require a .NET or Homebrew installation to run.
The declared minimum is macOS 26.0; local verification uses macOS 26.7.1.
Intel Macs, older macOS, Windows, Linux and consoles are not verified targets.

Build on an Apple Silicon Mac with .NET 10, Python 3.12+ and Apple command-line
tools. The first publish downloads official runtime/reference packs from NuGet.
The ordinary lock files and separate `packages.osx-arm64.lock.json` files pin
their respective dependency graphs.

```sh
dotnet restore monogame/Yipe.slnx --locked-mode
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
dotnet run --project monogame/Yipe.Tests -c Release --no-build
python3 monogame/tools/package_macos.py --version 0.1.0-preview.8
python3 monogame/tools/verify_macos_package.py build/monogame-packages/YipeIII-MonoGame-0.1.0-preview.8-macOS-arm64.tar.gz --tests monogame/Yipe.Tests/bin/Release/net10.0 --graphics monogame/Yipe.GraphicsChecks/bin/Release/net10.0
```

The builder refuses to overwrite an existing archive. Use a new version or a
separate `--output` directory when repeating it. Package after the normal checks;
avoid simultaneous restore/build/publish operations against shared project files.
The existing root GitHub workflow still builds/releases the C++ game. MonoGame
regression CI now has its own workflow; public upload and official
signing/notarization remain separate work. The new workflow has been linted
locally; check GitHub Actions for hosted results after pushing this checkpoint.

The `.tar.gz` contains `Yipe III MonoGame.app`, `README.txt` and a SHA-256 file
manifest. A separate archive `.sha256` accompanies it. The launcher in
`Contents/MacOS` executes the self-contained game in `Contents/Resources/Game`.
Managed assemblies belong with resources rather than Apple's nested-code
directory. Native libraries, game apphost and bundle receive ad-hoc signatures.
This is a development preview without Developer ID signing or notarization.

The [pixel-art app icon](icons/README.md) appears in Finder and the Dock. The
packager includes the multi-resolution ICNS, while an embedded BMP also gives
ordinary local builds the same running-game icon.

Publishing uses an empty runtime/targeting pack root so NuGet supplies Microsoft's
official packs. Installed targeting-pack data remains available only for package
pruning. This avoids silently shipping the Homebrew SDK's runtime build, whose
compression library can depend on external Homebrew Brotli libraries.

Verification extracts to a temporary location, checks every manifested byte,
arm64 slices, bundle/native signatures and native dependency paths. It imports
an isolated preview fixture using the packaged CLI, starts the extracted launcher
from an empty working directory, reopens that save and captures actual graphics.
The loader trace must show the bundled CLR, SDL and OpenAL and no Homebrew paths.
The verifier also starts the app through macOS LaunchServices (`open -n -W`) and
requires a fresh rendered capture before accepting that launch.
An active graphical desktop is required for this check.

With `--tests`, the verifier copies the existing persistence runner into a
separate disposable directory beside copies of the package's actual assemblies
and runtime. Its runtime configuration/dependency list uses the packaged runtime;
the signed extracted app is untouched. Separate processes verify full-state/RNG
save continuation, 24 imports and 96 reload/item/save cycles. This reuses the
established C++ expectations; it does not replace them with package output.
Reports, subprocess logs and the startup capture remain under `--output`.

With `--graphics`, an isolated runner also uses the packaged assemblies/runtime
for normal Game.Run startup, ident/title animation, resizing, fullscreen and
returning to a window. The extracted signed app remains untouched.

Physical-controller play, real reconnect/focus testing and another user's Mac
still need manual verification. The user confirmed no controller is available
on 2026-10-12 and requested that this remain documented as unverified. Synthetic input and real graphics checks are
reported separately. The ordinary GPU runner is documented in
[graphics checks](../Yipe.GraphicsChecks/README.md).

## Dependency notices

The app retains notices under `Contents/Resources/Licenses`:

| File | Source |
| --- | --- |
| MonoGame.txt | [MonoGame license at the pinned 3.8.5.1 source revision](https://github.com/MonoGame/MonoGame/blob/5e9fe9b100ffa67026dff97cc8adffae1414534e/LICENSE.txt) |
| DotNet.txt, DotNet-ThirdParty.txt | [dotnet/runtime v10.0.8](https://github.com/dotnet/runtime/tree/v10.0.8) |
| SDL2.txt | `MonoGame.Library.SDL` 2.32.10.2 NuGet `LICENSE.txt` |
| OpenAL.txt | `MonoGame.Library.OpenAL` 1.24.3.4 NuGet `LICENSE` |
| NVorbis.txt | `NVorbis` 0.10.4 NuGet `LICENSE` |

Original game attribution is recovered from `assets/embedded/ui-catalog.json`,
dialog 100: Kevin Kinell, Pat Kelley and Eric Johnson. The 21-topic gameplay
guide JSON retains the gameplay titles/body text from the checked-in
`app/generated/original_help.hpp` (the generator's `\?` escapes represent literal
question marks). The UI expands tabs for display. The seven obsolete commercial,
hintbook and distribution topics are excluded; current controls appear separately. Updating the runtime version
also requires refreshing its retained third-party notices.

## Current release evidence

[Preview.8 app icon report](../evidence/2026-10-12-app-icon/README.md) records the
icon addition and fresh extracted-package/startup checks. No gameplay changed.

[Preview.7 regression and performance report](../evidence/2026-10-12-release-readiness/README.md)
records the gameplay tests, frame timings, captures, source hashes and package
verification. See its physical-controller checklist before claiming hardware
coverage. The package is an Apple Silicon development preview, not a notarized
public release or a claim about other Macs.
