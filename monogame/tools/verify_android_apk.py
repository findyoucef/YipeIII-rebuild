#!/usr/bin/env python3
"""Check the APK's shared assets and ARM64 native payload; device tests are separate."""

import argparse
import json
from pathlib import Path
import subprocess
from zipfile import ZipFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1] / "Yipe.Android/Yipe.Android.csproj"
    items = json.loads(subprocess.check_output(
        ["dotnet", "msbuild", str(project), "-getItem:AndroidAsset"], text=True))["Items"]["AndroidAsset"]
    with ZipFile(args.apk) as apk:
        if "AndroidManifest.xml" not in apk.namelist() or "classes.dex" not in apk.namelist():
            raise RuntimeError("APK is missing its Android entry point.")
        for item in items:
            if apk.read("assets/" + item["Link"]) != Path(item["FullPath"]).read_bytes():
                raise RuntimeError("APK content differs from source: " + item["Link"])
        libraries = [name for name in apk.namelist() if name.startswith("lib/") and name.endswith(".so")]
        if {name.split("/")[1] for name in libraries} != {"arm64-v8a"}:
            raise RuntimeError("Expected only ARM64 native libraries.")
        if not any("openal" in name.lower() for name in libraries):
            raise RuntimeError("Missing the native audio backend.")
    print(f"PASS: {len(items)} APK assets match source; Android entry point and ARM64 audio/runtime are present.")


if __name__ == "__main__":
    main()
