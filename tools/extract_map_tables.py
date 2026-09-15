#!/usr/bin/env python3
"""Recover Yipe! III's static map grids from the Ghidra C export."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


WIDTH = 13
HEIGHT = 12
TILE_COUNT = WIDTH * HEIGHT


def parse_number(value: str) -> int:
    return int(value, 0)


def parse_dispatch(path: Path) -> list[tuple[int, str, int]]:
    text = path.read_text(encoding="utf-8")
    blocks = re.finditer(
        r"case\s+(?P<map_id>0x[0-9a-fA-F]+|\d+):(?P<body>.*?)"
        r"(?=\n\s*case\s+(?:0x[0-9a-fA-F]+|\d+):|\n\s*}\n)",
        text,
        re.DOTALL,
    )

    dispatch: list[tuple[int, str, int]] = []
    for match in blocks:
        map_id = parse_number(match.group("map_id"))
        body = match.group("body")
        loader = re.search(r"FUN_(00[0-9a-fA-F]{6})\(param_2\);", body)
        if loader is None:
            raise ValueError(f"map {map_id:#x} has no static loader")

        explicit_return = re.search(r"return\s+(0x[0-9a-fA-F]+|\d+)\s*;", body)
        environment_assignment = re.search(
            r"uVar1\s*=\s*(0x[0-9a-fA-F]+|\d+)\s*;", body
        )
        if explicit_return is not None:
            environment = parse_number(explicit_return.group(1))
        elif environment_assignment is not None:
            environment = parse_number(environment_assignment.group(1))
        else:
            environment = 0 if map_id < 0x29 else -1
        dispatch.append((map_id, loader.group(1).lower(), environment))

    if not dispatch:
        raise ValueError(f"no map cases found in {path}")
    return dispatch


def parse_loader(path: Path) -> list[int]:
    text = path.read_text(encoding="utf-8")
    values: list[int | None] = [None] * TILE_COUNT

    low = re.search(r"local_138\._0_2_\s*=\s*(0x[0-9a-fA-F]+|\d+)\s*;", text)
    high = re.search(r"local_138\._2_2_\s*=\s*(0x[0-9a-fA-F]+|\d+)\s*;", text)
    if low is not None:
        values[0] = parse_number(low.group(1))
    if high is not None:
        values[1] = parse_number(high.group(1))

    whole = re.search(r"(?<!\.)\blocal_138\s*=\s*(0x[0-9a-fA-F]+|\d+)\s*;", text)
    if whole is not None:
        packed = parse_number(whole.group(1))
        values[0] = packed & 0xFFFF
        values[1] = packed >> 16

    for match in re.finditer(
        r"\blocal_([0-9a-fA-F]+)\s*=\s*(0x[0-9a-fA-F]+|\d+)\s*;", text
    ):
        offset = int(match.group(1), 16)
        if offset >= 0x138 or (0x138 - offset) % 2 != 0:
            continue
        index = (0x138 - offset) // 2
        if index < TILE_COUNT:
            values[index] = parse_number(match.group(2))

    missing = [index for index, value in enumerate(values) if value is None]
    if missing:
        raise ValueError(f"{path.name}: missing tile indexes {missing}")
    return [int(value) for value in values]


def extract(decompiled_directory: Path) -> dict[str, object]:
    functions = decompiled_directory / "functions"
    dispatch = parse_dispatch(functions / "0041e110.c")
    maps = []
    for map_id, loader, environment in dispatch:
        flat_tiles = parse_loader(functions / f"{loader}.c")
        columns = [
            flat_tiles[x * HEIGHT : (x + 1) * HEIGHT] for x in range(WIDTH)
        ]
        maps.append(
            {
                "id": map_id,
                "id_hex": f"0x{map_id:02x}",
                "loader": loader,
                "environment": environment,
                "tiles_by_x": columns,
            }
        )

    return {
        "format": "yipe3-original-map-tables-v1",
        "source_dispatch": "0041e110",
        "dimensions": {
            "width": WIDTH,
            "height": HEIGHT,
            "storage": "column-major",
        },
        "maps": maps,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--decompiled-dir",
        type=Path,
        required=True,
        help="directory containing the exported functions/ directory",
    )
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()

    result = extract(arguments.decompiled_dir)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
