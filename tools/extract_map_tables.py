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
EVENT_HELPERS = {
    "0041cf40": "message",
    "0041cf70": "overlay",
    "0041cff0": "princess",
    "0041d080": "depth",
    "0041d0b0": "depth",
    "0041d180": "linked_first",
    "0041d1e0": "linked_second",
}
EXPECTED_EVENT_CALLS = {
    "message": 20,
    "overlay": 10,
    "princess": 20,
    "depth": 26,
    "linked_first": 8,
    "linked_second": 6,
}


def parse_number(value: str) -> int:
    return int(value, 0)


def parse_signed_number(value: str) -> int:
    parsed = parse_number(value)
    return parsed - (1 << 32) if parsed >= (1 << 31) else parsed


def split_arguments(arguments: str) -> list[str]:
    return [argument.strip() for argument in arguments.split(",")]


def parse_events(path: Path) -> dict[int, dict[str, object]]:
    text = path.read_text(encoding="utf-8")
    matches = list(
        re.finditer(r"^\s*case\s+(0x[0-9a-fA-F]+|\d+):", text, re.MULTILINE)
    )
    events: dict[int, dict[str, object]] = {}
    counts = {name: 0 for name in EXPECTED_EVENT_CALLS}
    pending_map_ids: list[int] = []
    helper_pattern = "|".join(EVENT_HELPERS)
    for index, match in enumerate(matches):
        pending_map_ids.append(parse_number(match.group(1)))
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        body = text[match.end() : end]
        calls = re.findall(rf"FUN_({helper_pattern})\((.*?)\);", body, re.DOTALL)
        if not body.strip():
            continue
        for map_id in pending_map_ids:
            map_events = events.setdefault(
                map_id,
                {
                    "message": None,
                    "overlay": None,
                    "depth": [],
                    "linked": [None, None],
                    "princess": None,
                },
            )
            for helper, raw_arguments in calls:
                arguments = split_arguments(raw_arguments)
                kind = EVENT_HELPERS[helper]
                expected_arguments = 5 if kind == "princess" else 4
                if len(arguments) != expected_arguments or arguments[0] != "param_5":
                    raise ValueError(f"unexpected {helper} event call: {raw_arguments}")
                event = {
                    "x": parse_signed_number(arguments[1]),
                    "y": parse_signed_number(arguments[2]),
                    "value": parse_signed_number(arguments[3]),
                }
                if kind == "princess":
                    event["rescue_index"] = parse_signed_number(arguments[4])
                counts[kind] += 1
                if kind == "depth":
                    map_events["depth"].append(event)
                elif kind == "linked_first":
                    map_events["linked"][0] = event
                elif kind == "linked_second":
                    map_events["linked"][1] = event
                else:
                    map_events[kind] = event
        pending_map_ids = []

    if counts != EXPECTED_EVENT_CALLS:
        raise ValueError(f"unexpected event call counts: {counts}")
    for map_id, map_events in events.items():
        if len(map_events["depth"]) > 2:
            raise ValueError(f"map {map_id:#x} has more than two depth events")
        for event in [map_events["message"], map_events["overlay"],
                      *map_events["depth"], *map_events["linked"]]:
            if event is None:
                continue
            if not (-1 <= event["x"] < WIDTH and -1 <= event["y"] < HEIGHT):
                raise ValueError(f"map {map_id:#x} has invalid event position {event}")
        princess = map_events["princess"]
        if princess is not None:
            if not (0 <= princess["x"] < WIDTH and 0 <= princess["y"] < HEIGHT):
                raise ValueError(f"map {map_id:#x} has invalid princess position {princess}")
            if not (0 <= princess["rescue_index"] < 30):
                raise ValueError(f"map {map_id:#x} has invalid princess index {princess}")
    return events


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
    events = parse_events(functions / "0041ec40.c")
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
                "events": events.get(
                    map_id,
                    {
                        "message": None,
                        "overlay": None,
                        "depth": [],
                        "linked": [None, None],
                        "princess": None,
                    },
                ),
            }
        )

    return {
        "format": "yipe3-original-map-tables-v3",
        "source_dispatch": "0041e110",
        "source_events": "0041ec40",
        "dimensions": {
            "width": WIDTH,
            "height": HEIGHT,
            "storage": "column-major",
        },
        "maps": maps,
    }


def render_cpp(result: dict[str, object]) -> str:
    maps = result["maps"]
    lines = [
        '// Generated by tools/extract_map_tables.py. Do not edit by hand.',
        '#include "yipe/map.hpp"',
        "",
        "#include <array>",
        "#include <optional>",
        "",
        "namespace yipe {",
        "namespace {",
        "",
        "struct OriginalMapRecord {",
        "    int id;",
        "    int environment;",
        "    Map::Tiles tiles;",
        "    MapEvents events;",
        "};",
        "",
        f"constexpr std::array<OriginalMapRecord, {len(maps)}> original_maps{{{{",
    ]
    for map_record in maps:
        lines.append(
            f"    {{{map_record['id']}, {map_record['environment']}, Map::Tiles{{"
        )
        for column in map_record["tiles_by_x"]:
            values = ", ".join(str(value) for value in column)
            lines.append(f"        {values},")
        def event_literal(event: dict[str, int] | None) -> str:
            if event is None:
                return "{{-1, -1}, -1}"
            return f"{{{{{event['x']}, {event['y']}}}, {event['value']}}}"

        def princess_literal(event: dict[str, int] | None) -> str:
            if event is None:
                return "{{-1, -1}, 0, 0}"
            return (f"{{{{{event['x']}, {event['y']}}}, {event['value']}, "
                    f"{event['rescue_index']}}}")

        events = map_record["events"]
        depth = list(events["depth"])
        depth.extend([None] * (2 - len(depth)))
        linked = events["linked"]
        lines.append(
            "    }, MapEvents{"
            f"{event_literal(events['message'])}, "
            f"{event_literal(events['overlay'])}, "
            f"{{{{{event_literal(depth[0])}, {event_literal(depth[1])}}}}}, "
            f"{{{{{event_literal(linked[0])}, {event_literal(linked[1])}}}}}, "
            f"{princess_literal(events['princess'])}"
            "}},"
        )
    lines.extend(
        [
            "}};",
            "",
            "} // namespace",
            "",
            "std::optional<Map> original_map(int id) {",
            "    for (const OriginalMapRecord& record : original_maps) {",
            "        if (record.id == id) {",
            "            return Map(record.id, record.environment, record.tiles, record.events);",
            "        }",
            "    }",
            "    return std::nullopt;",
            "}",
            "",
            "} // namespace yipe",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--decompiled-dir",
        type=Path,
        required=True,
        help="directory containing the exported functions/ directory",
    )
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--cpp-output",
        type=Path,
        help="optional generated C++ catalog for the core library",
    )
    arguments = parser.parse_args()

    result = extract(arguments.decompiled_dir)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    if arguments.cpp_output is not None:
        arguments.cpp_output.parent.mkdir(parents=True, exist_ok=True)
        arguments.cpp_output.write_text(render_cpp(result), encoding="utf-8")


if __name__ == "__main__":
    main()
