#!/usr/bin/env python3
"""Verify a locally built CPack archive after extraction outside the checkout."""

import argparse
import os
from pathlib import Path
import plistlib
import subprocess
import tempfile


def run(*command, **kwargs):
    return subprocess.run(command, check=True, text=True, capture_output=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    args = parser.parse_args()
    archive = args.archive.resolve(strict=True)
    with tempfile.TemporaryDirectory(prefix="yipe-package-") as temporary:
        directory = Path(temporary).resolve()
        run("tar", "-xzf", str(archive), "-C", temporary)
        bundles = list(directory.rglob("*.app"))
        if len(bundles) != 1:
            raise RuntimeError("expected exactly one application bundle")
        bundle = bundles[0]
        with (bundle / "Contents/Info.plist").open("rb") as stream:
            metadata = plistlib.load(stream)
        executable = bundle / "Contents/MacOS" / metadata["CFBundleExecutable"]
        resources = bundle / "Contents/Resources"
        for required in [resources / "SDL2-LICENSE.txt",
                         resources / "assets/379-1033.bmp",
                         bundle.parent / "README.md",
                         bundle.parent / "docs/FEATURE_COVERAGE.md"]:
            if not required.is_file():
                raise RuntimeError(f"missing package content: {required}")
        for item in bundle.rglob("*"):
            if item.is_symlink() and not item.resolve().is_relative_to(bundle):
                raise RuntimeError(f"bundle symlink points outside application: {item}")

        libraries = list(bundle.rglob("*.dylib"))
        if not any("SDL2" in item.name for item in libraries):
            raise RuntimeError("SDL2 is missing from the application")
        for binary in [executable, *libraries]:
            dependencies = run("otool", "-L", str(binary)).stdout.splitlines()[1:]
            for line in dependencies:
                dependency = line.strip().split(" (compatibility version", 1)[0]
                if dependency.startswith(("/usr/lib/", "/System/Library/")):
                    continue
                if dependency.startswith("@executable_path/"):
                    resolved = executable.parent / dependency.removeprefix("@executable_path/")
                elif dependency.startswith("@loader_path/"):
                    resolved = binary.parent / dependency.removeprefix("@loader_path/")
                else:
                    raise RuntimeError(f"nonportable library dependency: {dependency}")
                if not resolved.resolve().is_relative_to(bundle) or not resolved.is_file():
                    raise RuntimeError(f"dependency is not inside application: {dependency}")
        run("codesign", "--verify", "--deep", "--strict", str(bundle))

        # An empty working directory prevents the source-tree asset fallback.
        work = directory / "empty-working-directory"
        work.mkdir()
        environment = {key: value for key, value in os.environ.items()
                       if not key.startswith("DYLD_")}
        environment.update(SDL_VIDEODRIVER="dummy", SDL_AUDIODRIVER="dummy",
                           DYLD_PRINT_LIBRARIES="1")
        smoke = run(str(executable), "--smoke-test", cwd=work, env=environment, timeout=30)
        if str(bundle / "Contents/Frameworks/libSDL2") not in smoke.stderr:
            raise RuntimeError("loader trace did not confirm the bundled SDL2 library")
        if "/opt/homebrew/" in smoke.stderr or "/usr/local/" in smoke.stderr:
            raise RuntimeError("loader trace contains an external package-manager library")
        assets = sum(item.is_file() for item in (resources / "assets").rglob("*"))
        print(f"Verified {archive.name}: {assets} assets, bundled SDL2, valid ad-hoc signature,")
        print("and headless startup from an empty directory outside the checkout.")
        print("Interactive launch, other machines, and notarization are not verified here.")


if __name__ == "__main__":
    main()
