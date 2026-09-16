#!/usr/bin/env python3
"""Recover Yipe! III's static monster placements from the Ghidra C export."""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path


EXPECTED_PLACEMENTS = 186


def parse_number(value: str) -> int:
    parsed = int(value, 0)
    return parsed - (1 << 32) if parsed >= (1 << 31) else parsed


def split_arguments(arguments: str) -> list[str]:
    result: list[str] = []
    start = 0
    depth = 0
    for index, character in enumerate(arguments):
        if character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
        elif character == "," and depth == 0:
            result.append(arguments[start:index].strip())
            start = index + 1
    result.append(arguments[start:].strip())
    return result


def case_blocks(text: str) -> list[tuple[list[int], str]]:
    matches = list(re.finditer(r"^\s*case\s+(0x[0-9a-fA-F]+|\d+):", text, re.MULTILINE))
    blocks: list[tuple[list[int], str]] = []
    pending: list[int] = []
    for index, match in enumerate(matches):
        pending.append(parse_number(match.group(1)))
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        body = text[match.end() : end]
        if body.strip():
            blocks.append((pending, body))
            pending = []
    return blocks


@dataclass(frozen=True)
class Placement:
    map_id: int
    type_expression: str
    x: int
    y: int
    slot: int
    size_expression: str
    flag_36c: bool
    flag_371: bool
    companion_type_1_expression: str | None = None
    companion_type_2_expression: str | None = None


def parse_constant(expression: str, field: str) -> int:
    if not re.fullmatch(r"(?:0x[0-9a-fA-F]+|\d+)", expression):
        raise ValueError(f"unsupported {field} expression: {expression}")
    return parse_number(expression)


def parse_placements(path: Path) -> list[Placement]:
    text = path.read_text(encoding="utf-8")
    placements: list[Placement] = []
    for map_ids, body in case_blocks(text):
        for helper, raw_arguments in re.findall(
            r"FUN_(0041d7f0|0041d780)\((.*?)\);", body, re.DOTALL
        ):
            arguments = split_arguments(raw_arguments)
            expected = 7 if helper == "0041d7f0" else 9
            if len(arguments) != expected or arguments[0] != "param_5":
                raise ValueError(f"unexpected {helper} call: {raw_arguments}")
            for map_id in map_ids:
                type_expression = arguments[1]
                if type_expression == "param_2 + -0x56":
                    type_expression = str(map_id - 0x56)
                placements.append(
                    Placement(
                        map_id=map_id,
                        type_expression=type_expression,
                        x=parse_constant(arguments[2], "x"),
                        y=parse_constant(arguments[3], "y"),
                        slot=parse_constant(arguments[4], "slot"),
                        size_expression=arguments[5],
                        flag_36c=parse_constant(arguments[6], "flag_36c") != 0,
                        flag_371=helper == "0041d780",
                        companion_type_1_expression=(
                            arguments[7] if helper == "0041d780" else None
                        ),
                        companion_type_2_expression=(
                            arguments[8] if helper == "0041d780" else None
                        ),
                    )
                )

    if len(placements) != EXPECTED_PLACEMENTS:
        raise ValueError(
            f"expected {EXPECTED_PLACEMENTS} placements, found {len(placements)}"
        )
    for placement in placements:
        if not 0 <= placement.slot < 5:
            raise ValueError(f"map {placement.map_id:#x} has invalid slot {placement.slot}")
        if not 0 <= placement.x < 13 or not 0 <= placement.y < 12:
            raise ValueError(
                f"map {placement.map_id:#x} has invalid position "
                f"({placement.x}, {placement.y})"
            )
    return placements


def value_source(expression: str) -> tuple[str, int]:
    if re.fullmatch(r"(?:0x[0-9a-fA-F]+|\d+)", expression):
        return "fixed", parse_number(expression)
    if expression == "param_3":
        return "difficulty", 0
    if expression == "param_3 + 0x23":
        return "difficulty", 0x23
    if expression == "param_3 + 0x24":
        return "difficulty", 0x24
    if expression == "uVar1":
        return "map_39_level", 0
    if expression == "param_4":
        return "map_75_level", 0
    if expression == "(-(uint)(param_3 != 3) & 0xfffffffc) + 0x14":
        return "difficulty_three_20_else_16", 0
    if expression == "(param_3 != 3) - 1 & 0xc":
        return "difficulty_three_12_else_zero", 0
    raise ValueError(f"unsupported dynamic placement expression: {expression}")


def render_json(placements: list[Placement]) -> dict[str, object]:
    return {
        "format": "yipe3-original-monster-placements-v2",
        "source": "0041ec40",
        "placements": [
            {
                "map_id": placement.map_id,
                "map_id_hex": f"0x{placement.map_id:02x}",
                "slot": placement.slot,
                "type_expression": placement.type_expression,
                "x": placement.x,
                "y": placement.y,
                "encounter_size_expression": placement.size_expression,
                "flag_36c": placement.flag_36c,
                "flag_371": placement.flag_371,
                "companion_type_1_expression": placement.companion_type_1_expression,
                "companion_type_2_expression": placement.companion_type_2_expression,
            }
            for placement in placements
        ],
    }


def render_value(expression: str) -> str:
    source, value = value_source(expression)
    names = {
        "fixed": "ValueSource::fixed",
        "difficulty": "ValueSource::difficulty",
        "map_39_level": "ValueSource::map_39_level",
        "map_75_level": "ValueSource::map_75_level",
        "difficulty_three_20_else_16": "ValueSource::difficulty_three_20_else_16",
        "difficulty_three_12_else_zero": "ValueSource::difficulty_three_12_else_zero",
    }
    return f"{{{names[source]}, {value}}}"


def render_cpp(placements: list[Placement]) -> str:
    lines = [
        "// Generated by tools/extract_monster_placements.py. Do not edit by hand.",
        '#include "yipe/encounter.hpp"',
        "",
        "#include <array>",
        "#include <stdexcept>",
        "#include <vector>",
        "",
        "namespace yipe {",
        "namespace {",
        "",
        "enum class ValueSource {",
        "    fixed,",
        "    difficulty,",
        "    map_39_level,",
        "    map_75_level,",
        "    difficulty_three_20_else_16,",
        "    difficulty_three_12_else_zero,",
        "};",
        "",
        "struct PlacementValue {",
        "    ValueSource source;",
        "    int value;",
        "};",
        "",
        "struct OriginalMonsterPlacementRecord {",
        "    int map_id;",
        "    PlacementValue type_id;",
        "    MapPosition position;",
        "    int slot;",
        "    PlacementValue encounter_size;",
        "    PlacementValue companion_type_1;",
        "    PlacementValue companion_type_2;",
        "    bool flag_36c;",
        "    bool flag_371;",
        "};",
        "",
        f"constexpr std::array<OriginalMonsterPlacementRecord, {len(placements)}> placements{{{{",
    ]
    for placement in placements:
        lines.append(
            "    {"
            f"{placement.map_id}, {render_value(placement.type_expression)}, "
            f"{{{placement.x}, {placement.y}}}, {placement.slot}, "
            f"{render_value(placement.size_expression)}, "
            f"{render_value(placement.companion_type_1_expression or '0')}, "
            f"{render_value(placement.companion_type_2_expression or '0')}, "
            f"{'true' if placement.flag_36c else 'false'}, "
            f"{'true' if placement.flag_371 else 'false'}"
            "},"
        )
    lines.extend(
        [
            "}};",
            "",
            "int resolve_value(PlacementValue value, int difficulty, int player_level) {",
            "    switch (value.source) {",
            "    case ValueSource::fixed:",
            "        return value.value;",
            "    case ValueSource::difficulty:",
            "        return difficulty + value.value;",
            "    case ValueSource::map_39_level:",
            "        return player_level < 7 ? 1 : 5;",
            "    case ValueSource::map_75_level:",
            "        if (player_level >= 21) {",
            "            return 20;",
            "        }",
            "        if (player_level == 17 || player_level == 19) {",
            "            return 18;",
            "        }",
            "        return player_level < 3 ? 3 : player_level;",
            "    case ValueSource::difficulty_three_20_else_16:",
            "        return difficulty == 3 ? 20 : 16;",
            "    case ValueSource::difficulty_three_12_else_zero:",
            "        return difficulty == 3 ? 12 : 0;",
            "    }",
            '    throw std::logic_error("unknown placement value source");',
            "}",
            "",
            "} // namespace",
            "",
            "std::vector<MonsterPlacement> original_monsters_for_map(",
            "    int map_id, int difficulty, int player_level) {",
            "    std::vector<MonsterPlacement> result;",
            "    for (const OriginalMonsterPlacementRecord& placement : placements) {",
            "        if (placement.map_id == map_id) {",
            "            result.push_back({resolve_value(placement.type_id, difficulty, player_level),",
            "                              placement.position, placement.slot,",
            "                              resolve_value(placement.encounter_size, difficulty,",
            "                                            player_level),",
            "                              resolve_value(placement.companion_type_1, difficulty,",
            "                                            player_level),",
            "                              resolve_value(placement.companion_type_2, difficulty,",
            "                                            player_level),",
            "                              placement.flag_36c, placement.flag_371});",
            "        }",
            "    }",
            "    return result;",
            "}",
            "",
            "} // namespace yipe",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cpp-output", type=Path, required=True)
    arguments = parser.parse_args()

    placements = parse_placements(arguments.source)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        json.dumps(render_json(placements), indent=2) + "\n", encoding="utf-8"
    )
    arguments.cpp_output.parent.mkdir(parents=True, exist_ok=True)
    arguments.cpp_output.write_text(render_cpp(placements), encoding="utf-8")


if __name__ == "__main__":
    main()
