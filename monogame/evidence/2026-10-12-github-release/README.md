# First automated MonoGame macOS release — 2026-10-12

Goal completed: GitHub Actions builds and verifies a self-contained Apple Silicon
app, retains downloadable archives/checksums, and publishes distinct MonoGame
prereleases from `monogame-v*` tags. The legacy C++ workflow is unchanged.

**[Download preview.9](https://github.com/findyoucef/YipeIII-rebuild/releases/tag/monogame-v0.1.0-preview.9)**
— Apple Silicon, macOS 26+, bundled .NET 10.0.8. Ad-hoc signed; not Developer ID
signed or notarized.

| Evidence | Result |
| --- | --- |
| [Branch workflow](https://github.com/findyoucef/YipeIII-rebuild/actions/runs/38305477834) | Passed; downloaded artifact then passed the full local package verifier before tagging |
| [Release workflow](https://github.com/findyoucef/YipeIII-rebuild/actions/runs/38305850728) | Both build/test/package and publish jobs passed |
| Tag / source revision | `monogame-v0.1.0-preview.9` / `fb9a885e43ef1486f94ca3298c106b21dc1ba84b` |
| [Published assets](release.json) | App archive and matching SHA-256 file; 183,860,696-byte archive |
| [Manifest](release-manifest.json) | Tag revision, preview version and all 156 source fingerprints match |
| [Hosted core checks](release-hosted-core.log) | 660 checks, including 16 rounds of simultaneous import contention |
| [Hosted campaign](release-hosted-campaign.log) | 43,080 C++ state/RNG observations and 66 full saves match |
| [Hosted package](release-hosted-package.json) | 422 files, 208 content files, 15 native libraries; signatures, arm64, icon, dependencies, rendered launches and isolated persistence pass |
| [Published download checked locally](release-local-package.json) | Same checksum as hosted verification; all extracted-package and save checks pass |
| [Normal startup/display log](release-local-startup.log) | Real Game.Run, ident/title animation, 60.7 FPS sample, resize, fullscreen, return to window and save preservation pass on M5 Max / macOS 26.7.1 |

Archive SHA-256:

```text
0cb43e59f02aee989108c01c3469af3bffc8a6f8f8fba361b411df78dbe40585
```

The published archive was downloaded with `gh release download`, checked against
its checksum, and verified with `verify_macos_package.py --tests ... --graphics ...`.
No local repackaging substituted for the GitHub-built app. All test saves and
profiles were isolated. Captures: [title](release-title.png),
[window after fullscreen](release-windowed.png).

## Problems found and resolved

- The verifier's architecture command worked with Apple's `lipo` but failed with
  LLVM's argument parsing. Supplying the input before `-verify_arch` works with
  both; positive arm64 and negative x86_64 checks were verified locally.
- CI exposed competing imports replacing one another. The strengthened test
  [failed before the fix](import-race-before.log), with 12 successful competitors.
  [.NET's Unix move implementation](https://github.com/dotnet/runtime/blob/v10.0.8/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs#L193-L214)
  checks the destination before a replacing rename. Imports now publish their
  flushed temporary file with [atomic POSIX link](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/link.2.html).
  All 16 rounds of 16 independent stores now produce one intact destination and
  leave no temporary files, including Unicode paths. Normal saves and save bytes
  are unchanged. [Fixed test run](import-race-fixed.log),
  [zero-warning checked build](import-race-fixed-build.log).
- The user's cave-remaster skill is committed under `.agents/skills`; all three
  files were preserved, validation passed, and the global copy was removed.

## Coverage limits

The hosted VM's sustained normal-loop test alternated between a
[native SIGSEGV](hosted-native-crash.log) and
[6.8–6.9 FPS](hosted-normal-startup.log), below the test's minimum. The native
crash's root cause is unresolved; the initial debugger capture stopped on a
normal .NET suspension signal. No game rendering workaround or lower acceptance
threshold was introduced. MonoGame's own
[workflow](https://github.com/MonoGame/MonoGame/blob/76086c97bb780dfdb371945c5abe862cd245f7a0/.github/workflows/main.yml#L603-L643)
also reserves sustained Game/graphics checks outside hosted macOS runners.

CI still gates on three actual rendered smoke launches, including LaunchServices,
and packaged-runtime persistence (24 imports/96 cycles and eight special-room
restarts/640 continuation ticks). Its report explicitly records
`normal_startup: false`. The unchanged full `--graphics` check is required on the
downloaded branch artifact on a physical Mac before tagging; both that artifact
and the published release passed it here. VM graphics reliability is not claimed.

This task did not repeat the earlier 4,226-check visual suite or nine-scene
performance pass; those remain in the
[local release report](../2026-10-12-release-readiness/README.md). Physical
controllers remain unverified by the user's explicit instruction. Other Macs,
other platforms, and the three missing historical campaign variants remain
outside verified coverage. Unix import destinations must support hard links.

[Validation summary](validation.json), [workflow execution](workflow-run.json),
and [release procedure](../../packaging/README.md) retain the remaining details.
