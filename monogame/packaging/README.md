# MonoGame builds and packaging

Download [preview.9](https://github.com/findyoucef/YipeIII-rebuild/releases/tag/monogame-v0.1.0-preview.9).
Its hosted build, published checksum and local download verification are retained
in the [release evidence](../evidence/2026-10-12-github-release/README.md).

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
python3 monogame/tools/package_macos.py --version 0.1.0-preview.9
python3 monogame/tools/verify_macos_package.py build/monogame-packages/YipeIII-MonoGame-0.1.0-preview.9-macOS-arm64.tar.gz --tests monogame/Yipe.Tests/bin/Release/net10.0 --graphics monogame/Yipe.GraphicsChecks/bin/Release/net10.0
```

The builder refuses to overwrite an existing archive. Use a new version or a
separate `--output` directory when repeating it. Package after the normal checks;
avoid simultaneous restore/build/publish operations against shared project files.
The existing root GitHub workflow still builds/releases the C++ game. MonoGame
has its own build, verification and prerelease workflow, described below.
Developer ID signing and notarization remain separate work.

## GitHub Actions builds and releases

The [MonoGame macOS workflow](../../.github/workflows/monogame.yml) runs on
relevant pushes to `main`, pull requests, manual dispatch and `monogame-v*` tag
pushes. Every successful run provides a **YipeIII-MonoGame-macOS-arm64** artifact
containing the app archive and its `.sha256`. **MonoGame-regression** contains
the regression logs, extracted-package report and startup captures, including
failure diagnostics. Branch/PR/manual-branch packages use unique
`0.0.0-ci.<run>.<attempt>` versions and do not create releases.

After the desired commit's workflow passes, download its application artifact
and run the verifier above with `--tests` and `--graphics` on a physical Mac.
Then create and push a new release tag:

```sh
git tag -a monogame-v0.1.0-preview.9 -m 'Yipe! III MonoGame 0.1.0-preview.9'
git push origin monogame-v0.1.0-preview.9
```

The tag run rebuilds from that exact commit, deriving `0.1.0-preview.9` from the
tag. It must pass core/campaign tests, signature/dependency/icon checks, graphics
smoke startup, LaunchServices and fresh-process save checks before
uploading the verified artifact. A separate job downloads that run's artifact,
verifies its checksum again and attaches both files to a GitHub prerelease.
Only this publish job has `contents: write`; it uses the repository-provided
`GITHUB_TOKEN`. No personal access token or signing certificate is required for
the existing ad-hoc signed preview.

The `monogame-v` prefix keeps these releases separate from the legacy C++
workflow's `v*` tags. Manual dispatch builds artifacts only, even when a tag is
selected. Use a new version for later releases; published assets are never
overwritten by this workflow. Update [release notes](RELEASE_NOTES.md) before
tagging when the feature set or requirements change. Full visual/audio review,
performance measurement and physical controllers remain local/manual checks.

The hosted macOS VM is unsuitable for the sustained normal-loop graphics check:
observed runs either crashed natively or rendered the title at 6.8–6.9 FPS. The
native crash's root cause is unresolved; no gameplay workaround was added. The
workflow still requires three actual rendered smoke launches, including
LaunchServices. Normal Game.Run, ident/title animation, resize/fullscreen and
frame pacing remain required local checks of the downloaded artifact before
tagging. Reports explicitly set `normal_startup: false` when that check was not
run. MonoGame's own [workflow](https://github.com/MonoGame/MonoGame/blob/76086c97bb780dfdb371945c5abe862cd245f7a0/.github/workflows/main.yml#L603-L643)
also excludes sustained Game/graphics tests from hosted macOS runners.

## Bundle and verification

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

## Android ARM64 prototype

The separate `Yipe.Android` project compiles the existing game against MonoGame
Android 3.8.5.1. `GameContent.props` supplies the same 208 assets to both platforms.
The desktop solution and macOS workflow remain independent of Android tooling.
The APK declares Android 8 / API 26 or newer and ARM64 only; the tested environment
is an API 36 ARM64 emulator on an Apple M5 Max, not a physical phone.

Install the .NET 10 SDK, JDK 21 and Android SDK command-line tools, then install
the workload and SDK packages. On this Mac the SDK is under
`$HOME/Library/Android/sdk`; set these paths for your installation:

```sh
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
export ANDROID_HOME="$HOME/Library/Android/sdk"
dotnet workload install android --skip-manifest-update
"$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" 'platforms;android-36' 'build-tools;36.0.0' 'platform-tools'
dotnet restore monogame/Yipe.Android --locked-mode
dotnet build monogame/Yipe.Android -c Release --no-restore -t:SignAndroidPackage \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME"
python3 monogame/tools/verify_android_apk.py \
  monogame/Yipe.Android/bin/Release/net10.0-android/android-arm64/com.icosiumlabs.yipeiii-Signed.apk
"$ANDROID_HOME/platform-tools/adb" install --no-incremental -r \
  monogame/Yipe.Android/bin/Release/net10.0-android/android-arm64/com.icosiumlabs.yipeiii-Signed.apk
"$ANDROID_HOME/platform-tools/adb" shell am start -n com.icosiumlabs.yipeiii/.MainActivity
```

This is a self-contained, development-signed sideload APK: no desktop CLI,
fast-deployment server, AOT or trimming is required. The package is about 181 MB;
first launch also extracts the assets into app-private storage. The app requests
no external-storage permission. Untrimmed builds disable LLVM marshal methods
because the tested .NET Android combination otherwise failed to register the
activity's native startup callback. This setting is confined to Android.

Touch controls are enabled by default: the lower-left pad supports eight movement
directions, with ACT/BACK at lower right and MAP/MENU above. Tap menu rows, swipe
to scroll, drag sound sliders and use the touch keyboard to name an adventure.
Options → Touch controls adjusts size (80–130%) and opacity (20–100%). World
controls avoid display cutouts/system gesture edges and hide while a controller
is connected. Version `0.2.0-touch` uses version code 2.

Controller controls remain D-pad/left stick to move, South to select, East to go
back, Start to pause and View/Back to open the map. Android system Back navigates
the game menus. Presentation is landscape, fullscreen and integer-scaled with
centered black margins. macOS keeps its existing layout and controls.

Assets and saves occupy separate private directories. Updating with `adb install
-r` and the **same signing key** preserves saves, journals and preferences; this
was verified byte-for-byte before reopening the saved adventure. Uninstalling or
clearing app data deletes them. Keep the local development keystore for compatible
updates. Suspension pauses audio and play, writes preferences and saves at a safe
interaction boundary. During an unfinished interaction, the process can resume
in place; if Android kills it, loading returns to the last completed save.

The [Android workflow](../../.github/workflows/android.yml) builds on relevant
pushes/PRs and manual dispatch, verifies every packaged content byte and ARM64
native payload, then uploads `YipeIII-Android-arm64-prototype` with its checksum.
It has been linted locally; its first hosted run is pending a push. Each runner's
temporary development key can differ, so CI artifacts are **not guaranteed to
update an existing installation**. Stable signing, backup/export, AAB/store
publication and physical-device qualification remain later work. The job does
not publish releases or run device tests.

The [review](../evidence/2026-10-12-android-prototype/review.html) records emulator
screenshots/video, native audio state, save/reload and controller disconnect
checks. To repeat synthetic controller testing on a disposable emulator:

```sh
"$ANDROID_HOME/platform-tools/adb" -s emulator-5554 root
python3 monogame/tools/android_controller.py --adb "$ANDROID_HOME/platform-tools/adb"
```

Enter `a`, `b`, `start`, `map`, `up`, `down`, `left`, `right`, or
`stick_right 0.5` (any stick direction); `quit` disconnects. This uses Android's
virtual input device interface and the real MonoGame gamepad path. It is not
physical-controller validation. Test creation, movement, saving, Home/resume,
system Back, disconnect/reconnect and cold launch/load before claiming a mobile
build works. The [touch review](../evidence/2026-10-12-android-touch/review.html)
records the subsequent physical Pixel installation and touch checks. Full
cave/battle campaign coverage, hands-on multi-touch/controller usability and
battery/thermal performance remain unverified.

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
