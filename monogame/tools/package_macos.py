#!/usr/bin/env python3
"""Publish a self-contained, ad-hoc signed MonoGame macOS arm64 preview."""
import argparse
import hashlib
import json
import platform
import plistlib
import re
import shutil
import subprocess
import tarfile
import tempfile
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("build/monogame-packages"))
    parser.add_argument("--version", default="0.1.0-preview.9")
    args = parser.parse_args()
    if platform.system() != "Darwin" or platform.machine() != "arm64":
        parser.error("Build this preview on an Apple Silicon Mac.")
    if not re.fullmatch(r"[0-9][0-9A-Za-z.-]*", args.version):
        parser.error("Use a version containing only letters, numbers, periods and hyphens.")
    repo = Path(__file__).resolve().parents[2]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"YipeIII-MonoGame-{args.version}-macOS-arm64.tar.gz"
    if archive.exists():
        parser.error(f"Archive already exists: {archive}")
    with tempfile.TemporaryDirectory(prefix="yipe-monogame-package-") as temporary:
        root = Path(temporary)
        contents = root / "Yipe III MonoGame.app" / "Contents"
        resources = contents / "Resources"
        binary = resources / "Game"
        installed_packs = subprocess.check_output(["dotnet", "msbuild", str(repo / "monogame/Yipe.Desktop"),
                                                   "-getProperty:NetCoreTargetingPackRoot"], text=True, cwd=repo).strip()
        subprocess.run(["dotnet", "publish", str(repo / "monogame/Yipe.Desktop"), "-c", "Release",
                        "-r", "osx-arm64", "--self-contained", "true", "-p:RestoreLockedMode=true",
                        "-p:NuGetLockFilePath=packages.osx-arm64.lock.json",
                        "-p:NetCoreTargetingPackRoot=" + str(root / "official-packs"),
                        "-p:PrunePackageTargetingPackRoots=" + installed_packs,
                        "-o", str(binary)], check=True, cwd=repo)
        runtime = json.loads((binary / "Yipe.Desktop.runtimeconfig.json").read_text())["runtimeOptions"]
        if runtime.get("includedFrameworks") != [{"name": "Microsoft.NETCore.App", "version": "10.0.8"}]:
            raise RuntimeError("Update runtime dependency notices before changing the packaged .NET version.")
        shutil.copytree(repo / "monogame/packaging/licenses", resources / "Licenses")
        shutil.copyfile(repo / "monogame/packaging/icons/YipeIII.icns", resources / "YipeIII.icns")
        for name in ["README.md", "SAVE_FORMAT.md", "FEATURE_DISPOSITIONS.md"]:
            shutil.copyfile(repo / "monogame" / name, resources / name)
        with (contents / "Info.plist").open("wb") as file:
            plistlib.dump({"CFBundleName": "Yipe III MonoGame", "CFBundleDisplayName": "Yipe III MonoGame",
                          "CFBundleExecutable": "YipeIII", "CFBundleIdentifier": "org.yipe.rebuild.monogame",
                          "CFBundlePackageType": "APPL", "CFBundleVersion": "0.1.0",
                          "CFBundleIconFile": "YipeIII.icns",
                          "CFBundleShortVersionString": "0.1.0", "LSMinimumSystemVersion": "26.0",
                          "YipePreviewVersion": args.version,
                          "NSHighResolutionCapable": True,
                          "NSHumanReadableCopyright": "Original Yipe! III: Kevin Kinell, 2002-03. icosium labs, 2026."}, file)
        launcher = contents / "MacOS/YipeIII"
        launcher.parent.mkdir()
        launcher.write_text('#!/bin/sh\nexec "$(dirname "$0")/../Resources/Game/Yipe.Desktop" "$@"\n')
        launcher.chmod(0o755)
        (root / "README.txt").write_text(
            "Yipe! III MonoGame desktop preview " + args.version + "\n\n"
            "Apple Silicon Mac, macOS 26 or newer. No .NET installation required.\n"
            "Extract the archive and open Yipe III MonoGame.app, or move it to Applications.\n"
            "This app is ad-hoc signed, not Developer ID signed or notarized.\n"
            "If macOS blocks it, use System Settings > Privacy & Security > Open Anyway.\n"
            "The app is separate from the C++/SDL preview and uses separate save/profile files.\n"
            "Escape opens pause; Enter opens Actions; M opens the map.\n"
            "The title screen offers New adventure, Load game, Options and Quit.\n"
            "Pause > Adventure contains Save game, Load game and New adventure.\n"
            "Pause > Return to title lets you choose a different adventure. Save first to keep progress.\n"
            "Options contains Display, Atmosphere, Sound, Battle pacing, Edit controls and Help & credits.\n"
            "Adjust Sound sliders with Left/Right. Menus retain their standard controls.\n"
            "Default saves: ~/Library/Application Support/YipeIII-MonoGame/\n"
            "Copy both .y3m and .map.json when moving an adventure.\n"
            "New adventures receive separate slots under Saved Adventures. Existing saves are preserved.\n"
            "Full controls, save policy and dependency notices are inside Contents/Resources.\n"
            "Physical controllers, other operating systems and older macOS versions remain unverified.\n")
        for library in sorted(binary.glob("*.dylib")):
            subprocess.run(["codesign", "--force", "--sign", "-", str(library)], check=True, capture_output=True)
        subprocess.run(["codesign", "--force", "--sign", "-", str(binary / "Yipe.Desktop")], check=True)
        subprocess.run(["codesign", "--force", "--sign", "-", str(contents.parent)], check=True)
        subprocess.run(["codesign", "--verify", "--deep", "--strict", str(contents.parent)], check=True)
        files = {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                 for path in sorted(root.rglob("*")) if path.is_file()}
        sources = {path.relative_to(repo).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                   for path in sorted((repo / "monogame").rglob("*"))
                   if path.is_file() and not {"bin", "obj"}.intersection(path.relative_to(repo).parts)
                   and (path.suffix in {".cs", ".csproj", ".props"} or path.name in
                        {"package_macos.py", "verify_macos_package.py", "packages.lock.json", "packages.osx-arm64.lock.json"})}
        revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
        sdk = subprocess.check_output(["dotnet", "--version"], cwd=repo, text=True).strip()
        (root / "manifest.json").write_text(json.dumps({"version": args.version, "runtime": "osx-arm64",
                                                      "dotnet": "10.0.8", "sdk": sdk, "base_revision": revision,
                                                      "source_files": sources, "files": files}, indent=2) + "\n")
        with tarfile.open(archive, "w:gz") as tar:
            for path in sorted(root.iterdir()):
                tar.add(path, arcname=path.name)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix(archive.suffix + ".sha256").write_text(f"{digest}  {archive.name}\n")
    print(f"Created {archive}\nSHA-256: {digest}")


if __name__ == "__main__":
    main()
