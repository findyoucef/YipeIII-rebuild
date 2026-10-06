#!/usr/bin/env python3
"""Input-only authoring client for the unchanged C++ full-game test driver.

This proposes ordinary inputs and reads STATE observations. It never loads a
checkpoint into play or writes character/map fields. Retained literal commands
can be replayed without this authoring client.
"""
from collections import deque
from pathlib import Path
import json
import os
import re
import shlex
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def build_reference():
    output = ROOT / "build/monogame-reference/campaign-reference"
    output.parent.mkdir(parents=True, exist_ok=True)
    sources = [ROOT / "tests/app_shortcut_tests.cpp", *sorted((ROOT / "src").glob("*.cpp")),
               *sorted((ROOT / "src/generated").glob("*.cpp"))]
    flags = shlex.split(subprocess.check_output(["pkg-config", "--cflags", "--libs", "sdl2"], text=True))
    subprocess.run(["c++", "-std=c++20", "-O1", "-I", str(ROOT / "include"),
                    '-DYIPE_TEST_EVIDENCE_DIR="' + str(ROOT / "evidence/original-runtime") + '"',
                    *map(str, sources), *flags, "-o", str(output)], check=True)
    return output


class Campaign:
    def __init__(self, output, executable=None, mode="--full-game"):
        self.output = Path(output)
        self.output.mkdir(parents=True, exist_ok=False)
        self.commands = (self.output / "commands.txt").open("w", buffering=1)
        self.trace = (self.output / "trace.txt").open("w", buffering=1)
        self.states = (self.output / "states.jsonl").open("w", buffering=1)
        self.process = subprocess.Popen([str(executable or build_reference()), mode], cwd=ROOT,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, bufsize=1, env={**os.environ, "SDL_VIDEODRIVER": "dummy", "SDL_AUDIODRIVER": "dummy"})
        self.directory = None
        self.choices = deque()
        self.dialog_handler = None
        self.encounters = 0
        self.observation_count = 0
        self.command_count = 0
        self.maps = {m["id"]: m for m in json.loads((ROOT / "assets/maps/original-map-tables.json").read_text())["maps"]}
        self.placement_cells = {(p["map_id"], p["x"] + dx, p["y"] + dy)
            for p in json.loads((ROOT / "assets/maps/original-monster-placements.json").read_text())["placements"]
            for dx in range(2 if p["occupies_two_by_two_tiles"] else 1)
            for dy in range(2 if p["occupies_two_by_two_tiles"] else 1)}
        self.state = self.observe()

    def send(self, line):
        self.command_count += 1
        self.commands.write(line + "\n")
        self.process.stdin.write(line + "\n")
        self.process.stdin.flush()

    def line(self):
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError(f"Reference exited {self.process.wait()}; inspect {self.output / 'trace.txt'}")
        self.trace.write(line)
        return line.rstrip("\n")

    def observe(self):
        while True:
            line = self.line()
            if line.startswith("FULL_GAME_DIRECTORY="):
                self.directory = Path(line.split("=", 1)[1].strip('"'))
            elif line.startswith("STATE "):
                self.states.write(line[6:] + "\n")
                self.observation_count += 1
                return json.loads(line[6:])
            elif line.startswith("ENCOUNTER"):
                self.encounters += 1
            elif line.startswith("DIALOG "):
                title = line[7:]
                body = []
                buttons = {}
                while True:
                    text = self.line()
                    if not text and buttons:
                        break
                    button = re.fullmatch(r"(-?\d+): (.*)", text)
                    if button:
                        buttons[int(button[1])] = button[2]
                    else:
                        body.append(text)
                if self.choices:
                    expected, choice = self.choices.popleft()
                    if expected is not None and title != expected:
                        raise RuntimeError(f"Expected {expected}, saw {title}: {body}")
                elif self.dialog_handler:
                    choice = self.dialog_handler(title, "\n".join(body), buttons)
                elif title == "Rescued Princess":
                    choice = 4
                else:
                    raise RuntimeError(f"Unplanned dialog {title}: {body}; buttons {buttons}")
                if choice not in buttons:
                    raise RuntimeError(f"Unavailable choice {choice} in {title}")
                self.send(f"choice {choice}")

    def command(self, line, choices=()):
        if self.choices:
            raise RuntimeError("Unconsumed choices before command")
        self.choices.extend(choices)
        self.send(line)
        self.state = self.observe()
        if self.choices:
            raise RuntimeError(f"Unconsumed choices after {line}: {self.choices}")
        return self.state

    def expect(self, map_id, x, y):
        if (self.state["map"], self.state["x"], self.state["y"]) != (map_id, x, y):
            raise RuntimeError(f"Route diverged; expected {map_id} ({x},{y}), got {self.state}")

    def replay(self, directory):
        """Reconstruct an earned prefix from literal inputs, checking every observation."""
        directory = Path(directory)
        lines = (directory / "commands.txt").read_text().splitlines()
        expected = [json.loads(line) for line in (directory / "states.jsonl").read_text().splitlines()]
        if self.state != expected[0]:
            raise RuntimeError("Replay opening state differs")
        index = 0
        observation = 1
        while index < len(lines):
            command = lines[index]
            index += 1
            choices = []
            while index < len(lines) and lines[index].startswith("choice "):
                choices.append((None, int(lines[index].split()[1])))
                index += 1
            self.command(command, choices)
            if self.state != expected[observation]:
                raise RuntimeError(f"Replay differs at observation {observation}: {command}")
            observation += 1
        if observation != len(expected):
            raise RuntimeError("Replay did not consume every state observation")
        print(f"Replayed {observation} identical native observations from earned inputs", flush=True)

    def route(self, target, interact=False):
        """Propose static-terrain moves; the native result is checked after each room.

        This planner cannot solve moving platforms, secret actions or generated Hell.
        Those need authored commands observed in the actual game.
        """
        start = (self.state["map"], self.state["x"], self.state["y"])
        queue = deque([start])
        previous = {start: None}
        directions = [('7', -1, -1), ('8', 0, -1), ('9', 1, -1), ('4', -1, 0),
                      ('6', 1, 0), ('1', -1, 1), ('2', 0, 1), ('3', 1, 1)]
        ordinary = {2, 8, 11, *range(0x16, 0x21), *range(0x27, 0x2b), 0x2c, 0x2d, 0x2f,
                    0x32, 0x33, 0x34, 0x35, 0x3c, 0x43, 0x44, 0x45, 0x53, *range(0x65, 0x6a),
                    0x26, 0x42, 0x46, 0x47, 0x48, 0x49, 0x5a, 0x5d, 0x5e, 0x5f,
                    0x36, 0x39, 0x3e}
        if self.state["rainbow"]: ordinary.add(0x3f)
        if self.state["underworld"]: ordinary.add(0x40)
        if self.state["spray"]: ordinary.update([0x58, 0x59])
        if self.state["shoes"]: ordinary.update([0x56, 0x57])
        while queue and target not in previous:
            node = queue.popleft()
            mid, px, py = node
            m = self.maps[mid]
            for key, dx, dy in directions:
                x, y = px + dx, py + dy
                if not (0 <= x < 13 and 0 <= y < 12): continue
                tile = m["tiles_by_x"][x][y]
                if mid == 117 and self.state["gatekeeper"] >= 1 and (x, y) == (12, 1):
                    tile = 0x27
                if mid == 1 and tile in [0x56, 0x57] and (mid, x, y) != target:
                    continue  # Enter the text puzzle only as an explicit destination.
                if interact and (mid, x, y) == target:
                    previous[target] = (node, key)
                    break
                if tile not in ordinary: continue
                # Hell uses the same coordinate grid with the original +98 offset.
                alternate = mid >= 117
                layer = 0 if alternate else (mid - 1) // 40
                base = mid - 98 if alternate else mid - layer * 40
                rx = (base - 17 + 4) % 6 - 4
                ry = (17 + rx - base) // 6
                next_layer = layer
                depth = next((d for d in m["events"]["depth"] if d and (d["x"], d["y"]) == (x, y)), None)
                if depth: next_layer += -1 if depth["value"] == 0x3a else 1
                elif tile in [0x36, 0x39]: next_layer += 1
                if x == 0: rx -= 1; x = 11
                elif x == 12: rx += 1; x = 1
                if y == 0: ry += 1; y = 10
                elif y == 11: ry -= 1; y = 1
                if rx == 2: rx = -4
                elif rx == -5: rx = 1
                if ry == 3: ry = -3
                elif ry == -4: ry = 2
                if next_layer not in range(3): dest = (17, 4, 5)
                else: dest = (17 + rx - ry * 6 + next_layer * 40 + (98 if alternate else 0), x, y)
                if dest[0] not in self.maps or dest in previous: continue
                previous[dest] = (node, key)
                queue.append(dest)
        if target not in previous: raise RuntimeError(f"No static route from {start} to {target}")
        steps = []
        node = target
        while previous[node] is not None:
            origin, key = previous[node]
            steps.append((key, node))
            node = origin
        return list(reversed(steps))

    def walk_to(self, map_id, x, y, interact=False):
        target = (map_id, x, y)
        steps = self.route(target, interact)
        # Batches end at a map change, so unexpected travel cannot silently accumulate.
        keys = ""
        current_map = self.state["map"]
        for index, (key, expected) in enumerate(steps):
            keys += key
            if len(keys) == 4 or expected in self.placement_cells or expected[0] != current_map or index == len(steps) - 1:
                if self.state["hp"][0] < self.state["hp"][1] * .85 or self.state["sp"][0] < self.state["sp"][1] * .75:
                    self.rest()
                if self.state["spells"][4]:
                    self.command("spell 4")
                self.command("walk " + keys)
                if not (interact and index == len(steps) - 1): self.expect(*expected)
                keys = ""
                current_map = self.state["map"]

    def rest(self):
        # Actual exploration ticks and learned spells, with original costs/RNG.
        self.command("wait 760")
        for _ in range(30):
            if self.state["hp"][0] == self.state["hp"][1]:
                break
            if self.state["sp"][0] < 8:
                self.command("wait 760")
            self.command("spell 1")
        else:
            raise RuntimeError("Resting did not recover health")
        if self.state["sp"][0] < self.state["sp"][1]:
            self.command(f"wait {76 * ((self.state['sp'][1] - self.state['sp'][0] + 3) // 4)}")

    def board(self, direction, target):
        """Try an adjacent water cell until the real platform reaches the landing."""
        start = (self.state["map"], self.state["x"], self.state["y"])
        for _ in range(4000):
            self.command("walk " + direction)
            position = (self.state["map"], self.state["x"], self.state["y"])
            if position == target:
                return
            if position != start:
                raise RuntimeError(f"Unexpected platform boarding position: {position}")
            self.command("wait 1")
        raise RuntimeError(f"Platform did not reach {target}")

    def ride_to(self, x, y):
        map_id = self.state["map"]
        for _ in range(8000):
            if self.state["map"] != map_id:
                raise RuntimeError("Left the map while riding a platform")
            if (self.state["x"], self.state["y"]) == (x, y):
                return
            self.command("wait 1")
        raise RuntimeError(f"Platform did not carry the rider to {x},{y}")

    def checkpoint(self, label):
        shutil.copy2(self.directory / "fresh-route.y3r", self.output / (label + ".y3r"))
        (self.output / (label + ".json")).write_text(json.dumps(self.state, indent=2) + "\n")
        with (self.output / "checkpoints.jsonl").open("a") as checkpoints:
            checkpoints.write(json.dumps({"label": label, "observation": self.observation_count - 1,
                "command": self.command_count}) + "\n")
        print(label, json.dumps(self.state), flush=True)

    def close(self):
        # Failure/authoring prefixes must never masquerade as a completed fixture.
        if self.process.poll() is None:
            self.process.stdin.close()
            self.process.wait(timeout=30)
        for stream in [self.commands, self.trace, self.states]:
            stream.close()

    def finish(self):
        # Every preceding input saves. The original test harness automatically
        # chooses Leave Without Saving at its final quit dialog.
        self.command("quit")
        remainder = self.process.stdout.read()
        self.trace.write(remainder)
        for line in remainder.splitlines():
            if line.startswith("STATE "):
                self.states.write(line[6:] + "\n")
                self.state = json.loads(line[6:])
                self.observation_count += 1
        if self.process.wait() != 0 or "connected full-game finale verified" not in remainder:
            raise RuntimeError("The native driver did not verify full completion")
        print("PASS native saved earned completion and explicit quit", flush=True)
        self.close()


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-only", action="store_true")
    args = parser.parse_args()
    if args.build_only:
        print(build_reference())
    else:
        parser.error("Import Campaign from an input-only authoring script, or use --build-only.")
