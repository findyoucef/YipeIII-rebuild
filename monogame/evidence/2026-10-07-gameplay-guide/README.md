# Gameplay guide without commercial language — 2026-10-07

At the user's direction, removed the original shareware, registration/payment,
hintbook and distribution topics from the shipped guide. It now contains the 21
original gameplay topics. Help calls it Gameplay guide, and Stats and saves
explains stat limits/save preferences without registration or serial-number text.
No new pricing or distribution policy is invented. Historical reference evidence
is retained separately from the port's user-facing content.

Release build: zero warnings/errors. All 1,239 graphics checks pass with updated
guide navigation expectations. A source/content scan confirms no shareware,
registration, payment or serial-number language in the help screens or guide.
The package verifier requires 21 topics and rejects obsolete commercial language.

Updated archive: `YipeIII-MonoGame-0.1.0-preview.4-macOS-arm64.tar.gz`

SHA-256: `d83201e760f3d9f4afaca9ccda5dbdef9f32879cd1fd6c4e29b247555384d702`

The extracted app passes hashes, native/bundle signatures, dependencies, isolated
selected-save graphics startup and LaunchServices startup. See the retained logs
and [package report](package-report.json). Production changes are text/content
only; the full persistence proof from [preview.3](../2026-10-07-ui-package/README.md)
remains historical evidence and was not repeated for this update.

The updated app is extracted under `build/monogame-packages/preview.4/`.
An already running preview.3 must be relaunched to load the new UI/content.
No running personal session was quit or overwritten.
