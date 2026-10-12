#!/usr/bin/env python3
"""Verify extracted MonoGame assets, native dependencies, signatures and startup."""
import argparse
import hashlib
import json
import os
import platform
import plistlib
import shutil
import subprocess
import tarfile
import tempfile
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--output", type=Path, default=Path("build/monogame-package-check"))
    parser.add_argument("--tests", type=Path, help="Built Yipe.Tests directory; runs fresh-process save checks against packaged assemblies")
    parser.add_argument("--graphics", type=Path, help="Built Yipe.GraphicsChecks directory; runs the normal game-loop startup check against packaged assemblies")
    args = parser.parse_args()
    if platform.system() != "Darwin" or platform.machine() != "arm64":
        parser.error("Verify this package on an Apple Silicon Mac with an active graphical desktop.")
    archive = args.archive.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    env = {key: value for key, value in os.environ.items() if not key.startswith(("DOTNET_ROOT", "DYLD_"))}
    env.update(PATH="/usr/bin:/bin:/usr/sbin:/sbin", DOTNET_MULTILEVEL_LOOKUP="0")
    report = {"archive": archive.name, "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
              "os": platform.mac_ver()[0], "architecture": platform.machine(), "save_checks": False}
    with tempfile.TemporaryDirectory(prefix="yipe-monogame-verify-") as temporary:
        root = Path(temporary)
        extracted = root / "extracted"
        with tarfile.open(archive) as tar:
            tar.extractall(extracted, filter="data")
        manifest = json.loads((extracted / "manifest.json").read_text())
        actual = {path.relative_to(extracted).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                  for path in extracted.rglob("*") if path.is_file() and path != extracted / "manifest.json"}
        if actual != manifest["files"]:
            raise RuntimeError("Extracted files differ from the package manifest.")
        app = extracted / "Yipe III MonoGame.app"
        binary = app / "Contents/Resources/Game"
        plist = plistlib.loads((app / "Contents/Info.plist").read_bytes())
        if plist["CFBundleIdentifier"] != "org.yipe.rebuild.monogame":
            raise RuntimeError("Unexpected app bundle identity.")
        if plist.get("YipePreviewVersion") != manifest["version"]:
            raise RuntimeError("The app preview version differs from its manifest.")
        if plist.get("CFBundleIconFile") != "YipeIII.icns":
            raise RuntimeError("The app does not declare its game icon.")
        iconset = root / "YipeIII.iconset"
        subprocess.run(["iconutil", "-c", "iconset", str(app / "Contents/Resources/YipeIII.icns"),
                        "-o", str(iconset)], check=True, capture_output=True)
        for size in [16, 32, 128, 256, 512]:
            for scale in [1, 2]:
                name = f"icon_{size}x{size}" + ("@2x" if scale == 2 else "") + ".png"
                dimensions = subprocess.check_output(["sips", "-g", "pixelWidth", "-g", "pixelHeight",
                                                       "-g", "hasAlpha", str(iconset / name)], text=True)
                if any(value not in dimensions for value in [f"pixelWidth: {size * scale}",
                                                              f"pixelHeight: {size * scale}", "hasAlpha: yes"]):
                    raise RuntimeError("Invalid app icon representation: " + name)
        report["app_icon"] = "Ten alpha-preserving representations, 16–1024 pixels"
        subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
        libraries = sorted(binary.glob("*.dylib"))
        dependencies = {}
        for native in [binary / "Yipe.Desktop", *libraries]:
            subprocess.run(["codesign", "--verify", "--strict", str(native)], check=True, capture_output=True)
            # LLVM lipo consumes all following values as architectures; put the input first.
            subprocess.run(["lipo", str(native), "-verify_arch", "arm64"], check=True)
            lines = subprocess.check_output(["otool", "-arch", "arm64", "-L", str(native)], text=True).splitlines()[1:]
            links = [line.strip().split(" (")[0] for line in lines]
            if native.suffix == ".dylib":
                links = links[1:]  # The first entry is this dylib's own install name.
            for link in links:
                if link.startswith(("/usr/lib/", "/System/Library/")):
                    continue
                if link.startswith(("@rpath/", "@loader_path/", "@executable_path/")) and (binary / link.split("/", 1)[1]).is_file():
                    continue
                raise RuntimeError(f"Unbundled native dependency: {native.name}: {link}")
            dependencies[native.name] = links
        content = binary / "Content"
        for relative in ["maps.json", "original-help.json", "Yipe3Items.txt", "Branding/icosium-labs.png"]:
            if not (content / relative).is_file():
                raise RuntimeError("Missing packaged content: " + relative)
        guide = (content / "original-help.json").read_text()
        if len(json.loads(guide)) != 21 or any(word in guide.lower() for word in ["shareware", "registration", "registered", "payment"]):
            raise RuntimeError("The packaged gameplay guide is incomplete or contains obsolete commercial language.")
        report.update(files=len(actual), content_files=sum(p.is_file() for p in content.rglob("*")),
                      native_libraries=len(libraries), dependencies=dependencies)
        empty = root / "empty-working-directory"
        empty.mkdir()
        source = root / "position.json"
        source.write_text('{"Version":1,"MapId":17,"X":5,"Y":5}')
        source_bytes = source.read_bytes()
        save = root / "isolated.y3m"
        executable = str(app / "Contents/MacOS/YipeIII")

        def run(arguments, name, loader_trace=False):
            environment = dict(env)
            if loader_trace:
                environment["DYLD_PRINT_LIBRARIES"] = "1"
            result = subprocess.run(arguments, cwd=empty, env=environment, text=True, capture_output=True, timeout=60)
            (output / name).write_text(result.stdout + result.stderr)
            if result.returncode != 0:
                raise RuntimeError(f"{name} failed ({result.returncode}): {result.stdout[-1500:]} {result.stderr[-1500:]}")
            return result

        run([executable, "--import-save", str(source), str(save)], "import.log")
        if source.read_bytes() != source_bytes or not save.is_file():
            raise RuntimeError("The package import did not preserve its source and create a native save.")
        saved_bytes = save.read_bytes()
        smoke = run([executable, "--smoke-test", str(output / "startup.png"), "--load-save", str(save)], "startup.log")
        if "MonoGame graphics smoke test passed." not in smoke.stdout or save.read_bytes() != saved_bytes:
            raise RuntimeError("Selected-save graphics startup failed or changed the save.")
        # macOS strips DYLD variables from /bin/sh; trace the game apphost directly as well as its launcher.
        traced = run([str(binary / "Yipe.Desktop"), "--smoke-test", "--load-save", str(save)], "loader.log", True)
        if any(path in traced.stderr for path in ["/opt/homebrew", "/usr/local/", "/Cellar/"]):
            raise RuntimeError("Extracted startup loaded a development dependency.")
        for name in ["libcoreclr.dylib", "libSDL2-2.0.0.dylib", "libopenal.dylib"]:
            if str(binary / name) not in traced.stderr:
                raise RuntimeError("Loader trace does not confirm the bundled dependency: " + name)
        launch_capture = output / "launchservices.png"
        launch_capture.unlink(missing_ok=True)
        run(["/usr/bin/open", "-n", "-W", str(app), "--args", "--smoke-test", str(launch_capture), "--load-save", str(save)], "launchservices.log")
        if not launch_capture.is_file() or save.read_bytes() != saved_bytes:
            raise RuntimeError("LaunchServices startup did not render or preserve the selected save.")
        if args.tests:
            # Reuse the existing persistence runner, with the package's actual assemblies and bundled runtime.
            # Keep the signed extracted app untouched; the disposable runner lives in a separate directory.
            tests = args.tests.resolve()
            harness = root / "persistence-runner"
            shutil.copytree(binary, harness)
            for name in ["Yipe.Tests", "Yipe.Tests.dll"]:
                shutil.copyfile(tests / name, harness / name)
            (harness / "Yipe.Tests").chmod(0o755)
            shutil.copyfile(binary / "Yipe.Desktop.runtimeconfig.json", harness / "Yipe.Tests.runtimeconfig.json")
            deps = json.loads((binary / "Yipe.Desktop.deps.json").read_text())
            target = deps["targets"][deps["runtimeTarget"]["name"]]
            target["Yipe.Tests/1.0.0"] = {"runtime": {"Yipe.Tests.dll": {}}}
            deps["libraries"]["Yipe.Tests/1.0.0"] = {"type": "project", "serviceable": False, "sha512": ""}
            (harness / "Yipe.Tests.deps.json").write_text(json.dumps(deps))
            fixture = root / "restart.y3m"
            for mode in ["--write-save-fixture", "--read-save-fixture"]:
                run([str(harness / "Yipe.Tests"), mode, str(fixture)], mode[2:] + ".log")
            shutil.copytree(tests / "Content/Persistence", harness / "Content/Persistence")
            imports = root / "import-restarts"
            for mode in ["--write-import-fixtures", "--read-import-fixtures"]:
                run([str(harness / "Yipe.Tests"), mode, str(imports)], mode[2:] + ".log")
            rooms = root / "room-restarts"
            rooms.mkdir()
            for mode in ["--write-room-fixtures", "--read-room-fixtures"]:
                run([str(harness / "Yipe.Tests"), mode, str(rooms)], mode[2:] + ".log")
            report["save_checks"] = "Packaged assemblies/runtime: full-state/RNG restart, 24 imports/96 cycles, eight special rooms/640 continuation ticks"
        if args.graphics:
            harness = root / "graphics-runner"
            shutil.copytree(binary, harness)
            for name in ["Yipe.GraphicsChecks", "Yipe.GraphicsChecks.dll"]:
                shutil.copyfile(args.graphics / name, harness / name)
            (harness / "Yipe.GraphicsChecks").chmod(0o755)
            shutil.copyfile(binary / "Yipe.Desktop.runtimeconfig.json", harness / "Yipe.GraphicsChecks.runtimeconfig.json")
            deps = json.loads((binary / "Yipe.Desktop.deps.json").read_text())
            target = deps["targets"][deps["runtimeTarget"]["name"]]
            target["Yipe.GraphicsChecks/1.0.0"] = {"runtime": {"Yipe.GraphicsChecks.dll": {}}}
            deps["libraries"]["Yipe.GraphicsChecks/1.0.0"] = {"type": "project", "serviceable": False, "sha512": ""}
            (harness / "Yipe.GraphicsChecks.deps.json").write_text(json.dumps(deps))
            run([str(harness / "Yipe.GraphicsChecks"), "--startup-check", str(output / "normal-startup")], "normal-startup.log")
            report["normal_startup"] = "Packaged assemblies/runtime: normal Game.Run, ident/title, bounded presentation, resize/fullscreen/window, save preservation"
        report["result"] = "passed"
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"Package verified: {archive.name}; {report['files']} files, {report['content_files']} content files, {report['native_libraries']} native libraries.\nEvidence: {output}")


if __name__ == "__main__":
    main()
