#!/usr/bin/env python3
"""Fresh native replay of retained completion inputs, including full save checkpoints."""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess

from campaign_driver import ROOT, build_reference

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("fixture", type=Path)
parser.add_argument("output", type=Path)
parser.add_argument("--executable", type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
executable = args.executable or build_reference()
checkpoints = [json.loads(line) for line in (args.fixture / "checkpoints.jsonl").read_text().splitlines()]
environment = {**os.environ, "SDL_VIDEODRIVER": "dummy", "SDL_AUDIODRIVER": "dummy",
    "YIPE_CHECKPOINT_COMMANDS": " ".join(str(c["command"]) for c in checkpoints)}
trace_path = args.output / "trace.txt"
with (args.fixture / "commands.txt").open() as commands, trace_path.open("w") as trace:
    subprocess.run([str(executable.resolve()), "--full-game"], cwd=ROOT, stdin=commands,
        stdout=trace, stderr=subprocess.STDOUT, env=environment, check=True)
trace = trace_path.read_text()
if "PASS: unchanged opening checkpoints and connected full-game finale verified." not in trace:
    raise RuntimeError("The native driver did not verify the earned finale")
actual = [json.loads(line[6:]) for line in trace.splitlines() if line.startswith("STATE ")]
state_path = args.fixture / "states.jsonl"
expected_text = state_path.read_text() if state_path.exists() else gzip.decompress(Path(str(state_path) + ".gz").read_bytes()).decode()
expected = [json.loads(line) for line in expected_text.splitlines()]
if actual != expected:
    mismatch = next((i for i, pair in enumerate(zip(actual, expected)) if pair[0] != pair[1]), min(len(actual), len(expected)))
    raise RuntimeError(f"Native replay differs at observation {mismatch}; counts {len(actual)} / {len(expected)}")
directory = Path(next(line.split("=", 1)[1].strip('"') for line in trace.splitlines() if line.startswith("FULL_GAME_DIRECTORY=")))
if (directory / "commands.txt").read_bytes() != (args.fixture / "commands.txt").read_bytes():
    raise RuntimeError("Native replay consumed a different input stream")
for checkpoint in checkpoints:
    expected_save = args.fixture / (checkpoint["label"] + ".y3r")
    actual_save = directory / ("checkpoint-" + str(checkpoint["command"]) + ".y3r")
    if expected_save.read_bytes() != actual_save.read_bytes():
        raise RuntimeError("Native full save differs: " + checkpoint["label"])
report = {"observations": len(actual), "full_saves": len(checkpoints), "native_exit": 0,
    "final_state": actual[-1], "executable_sha256": hashlib.sha256(executable.read_bytes()).hexdigest()}
(args.output / "verification.json").write_text(json.dumps(report, indent=2) + "\n")
print(f"PASS independent fresh native completion: {len(actual)} state/RNG observations and {len(checkpoints)} complete saves", flush=True)
