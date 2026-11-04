#!/usr/bin/env python3
"""Drive the unmodified APK through an Android emulator's virtual USB controller.

Requires `adb root` on a disposable emulator. Does not modify game state or saves.
Input: a, b, start, map, up/down/left/right, stick_up/down/left/right [seconds].
EOF or `quit` disconnects the controller. This is synthetic input, not hardware validation.
Protocol: https://android.googlesource.com/platform/frameworks/base/+/main/cmds/uinput/
"""

import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default="emulator-5554")
    parser.add_argument("--adb", default=shutil.which("adb") or str(Path.home() / "Library/Android/sdk/platform-tools/adb"))
    args = parser.parse_args()
    adb = [args.adb, "-s", args.serial]
    if subprocess.check_output(adb + ["shell", "getprop", "ro.boot.qemu"], text=True).strip() != "1":
        parser.error("Use a disposable emulator, not a physical device.")
    if subprocess.check_output(adb + ["shell", "id", "-u"], text=True).strip() != "0":
        parser.error(f"Run adb -s {args.serial} root first.")
    axes = [0, 1, 3, 4, 16, 17]
    keys = {
        "a": (1, 304, 1), "b": (1, 305, 1), "start": (1, 315, 1), "map": (1, 314, 1),
        "up": (3, 17, -1), "down": (3, 17, 1), "left": (3, 16, -1), "right": (3, 16, 1),
        "stick_up": (3, 1, -32768), "stick_down": (3, 1, 32767),
        "stick_left": (3, 0, -32768), "stick_right": (3, 0, 32767),
    }
    with subprocess.Popen(adb + ["shell", "uinput", "-"], stdin=subprocess.PIPE, text=True) as device:
        def send(command, **values):
            device.stdin.write(json.dumps(dict(id=1, command=command, **values)) + "\n")
            device.stdin.flush()

        try:
            # Linux evdev codes and the standard Android Xbox 360 key layout.
            send("register", name="Yipe Virtual Controller", vid=1118, pid=654, bus="usb",
                 configuration=[{"type": 100, "data": [1, 3]},
                                {"type": 101, "data": [304, 305, 307, 308, 310, 311, 314, 315, 317, 318]},
                                {"type": 103, "data": axes}],
                 abs_info=[{"code": axis, "info": {"value": 0, "minimum": -1 if axis >= 16 else -32768,
                            "maximum": 1 if axis >= 16 else 32767, "fuzz": 0, "flat": 0, "resolution": 0}}
                           for axis in axes])
            time.sleep(2)  # Android must enumerate the device before accepting input.
            print("Virtual controller ready. First press may be suppressed by the game's reconnect guard.", flush=True)
            for line in sys.stdin:
                parts = line.split()
                if not parts:
                    continue
                if parts[0] == "quit":
                    break
                if parts[0] not in keys or len(parts) > 2:
                    print("Expected: " + ", ".join(keys) + " [seconds], or quit", flush=True)
                    continue
                try:
                    duration = float(parts[1]) if len(parts) == 2 else .12
                    if not .05 <= duration <= 10:
                        raise ValueError()
                except ValueError:
                    print("Duration must be 0.05–10 seconds.", flush=True)
                    continue
                event_type, code, value = keys[parts[0]]
                send("updateTimeBase")
                send("inject", events=[event_type, code, value, 0, 0, 0])
                time.sleep(duration)
                send("updateTimeBase")
                send("inject", events=[event_type, code, 0, 0, 0, 0])
                time.sleep(.2)
                print(f"Sent {parts[0]} for {duration}s", flush=True)
        finally:
            device.stdin.close()
            device.wait(timeout=5)


if __name__ == "__main__":
    main()
