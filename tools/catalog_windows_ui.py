#!/usr/bin/env python3
"""Decode Windows dialog, menu, and accelerator resource templates."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import struct


DS_SETFONT = 0x00000040
MF_POPUP = 0x0010
MF_END = 0x0080

CONTROL_CLASSES = {
    0x80: "BUTTON",
    0x81: "EDIT",
    0x82: "STATIC",
    0x83: "LISTBOX",
    0x84: "SCROLLBAR",
    0x85: "COMBOBOX",
}


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    def require(self, count: int) -> None:
        if self.offset + count > len(self.data):
            raise ValueError("resource template is truncated")

    def align(self, boundary: int) -> None:
        self.offset = (self.offset + boundary - 1) & ~(boundary - 1)
        self.require(0)

    def u8(self) -> int:
        self.require(1)
        value = self.data[self.offset]
        self.offset += 1
        return value

    def u16(self) -> int:
        self.require(2)
        value = struct.unpack_from("<H", self.data, self.offset)[0]
        self.offset += 2
        return value

    def i16(self) -> int:
        self.require(2)
        value = struct.unpack_from("<h", self.data, self.offset)[0]
        self.offset += 2
        return value

    def u32(self) -> int:
        self.require(4)
        value = struct.unpack_from("<I", self.data, self.offset)[0]
        self.offset += 4
        return value

    def bytes(self, count: int) -> bytes:
        self.require(count)
        value = self.data[self.offset : self.offset + count]
        self.offset += count
        return value

    def utf16(self, first: int | None = None) -> str:
        characters = []
        value = self.u16() if first is None else first
        while value != 0:
            characters.append(value)
            value = self.u16()
        return bytes(struct.pack(f"<{len(characters)}H", *characters)).decode(
            "utf-16le", errors="replace"
        ) if characters else ""

    def variable(self, class_name: bool = False) -> str | int | None:
        first = self.u16()
        if first == 0:
            return None
        if first == 0xFFFF:
            ordinal = self.u16()
            return CONTROL_CLASSES.get(ordinal, ordinal) if class_name else ordinal
        return self.utf16(first)


def font(reader: Reader, extended: bool) -> dict[str, int | str]:
    result: dict[str, int | str] = {"point_size": reader.u16()}
    if extended:
        result.update(
            weight=reader.u16(), italic=reader.u8(), charset=reader.u8()
        )
    result["face"] = reader.utf16()
    return result


def parse_dialog(data: bytes) -> dict[str, object]:
    reader = Reader(data)
    extended = len(data) >= 4 and struct.unpack_from("<HH", data) == (1, 0xFFFF)
    if extended:
        reader.u16()
        reader.u16()
        help_id = reader.u32()
        ex_style = reader.u32()
        style = reader.u32()
        count = reader.u16()
    else:
        help_id = 0
        style = reader.u32()
        ex_style = reader.u32()
        count = reader.u16()

    geometry = [reader.i16(), reader.i16(), reader.i16(), reader.i16()]
    result: dict[str, object] = {
        "extended": extended,
        "help_id": help_id,
        "style": f"0x{style:08x}",
        "extended_style": f"0x{ex_style:08x}",
        "geometry": geometry,
        "menu": reader.variable(),
        "class": reader.variable(class_name=True),
        "title": reader.variable(),
    }
    if style & DS_SETFONT:
        result["font"] = font(reader, extended)

    controls = []
    for _ in range(count):
        reader.align(4)
        if extended:
            control_help = reader.u32()
            control_ex_style = reader.u32()
            control_style = reader.u32()
            control_geometry = [reader.i16(), reader.i16(), reader.i16(), reader.i16()]
            control_id = reader.u32()
        else:
            control_style = reader.u32()
            control_ex_style = reader.u32()
            control_geometry = [reader.i16(), reader.i16(), reader.i16(), reader.i16()]
            control_id = reader.u16()
            control_help = 0
        control = {
            "id": control_id,
            "class": reader.variable(class_name=True),
            "title": reader.variable(),
            "geometry": control_geometry,
            "style": f"0x{control_style:08x}",
            "extended_style": f"0x{control_ex_style:08x}",
        }
        if control_help:
            control["help_id"] = control_help
        extra_count = reader.u16()
        if extra_count:
            control["extra_data_hex"] = reader.bytes(extra_count).hex()
        controls.append(control)
    result["controls"] = controls
    result["parsed_bytes"] = reader.offset
    result["payload_bytes"] = len(data)
    return result


def parse_menu_level(reader: Reader) -> list[dict[str, object]]:
    items = []
    while True:
        flags = reader.u16()
        popup = bool(flags & MF_POPUP)
        command_id = None if popup else reader.u16()
        item: dict[str, object] = {
            "flags": f"0x{flags:04x}",
            "title": reader.utf16(),
        }
        if command_id is not None:
            item["command_id"] = command_id
        if popup:
            item["children"] = parse_menu_level(reader)
        items.append(item)
        if flags & MF_END:
            return items


def parse_menu(data: bytes) -> dict[str, object]:
    reader = Reader(data)
    version = reader.u16()
    header_size = reader.u16()
    if version != 0:
        raise ValueError(f"unsupported extended menu version {version}")
    reader.offset += header_size
    return {"version": version, "items": parse_menu_level(reader)}


def key_name(key: int, virtual: bool) -> str:
    if not virtual:
        return chr(key) if 32 <= key < 127 else f"character:{key}"
    names = {0x08: "Backspace", 0x1B: "Escape", 0x2E: "Delete", 0x75: "F6"}
    return names.get(key, chr(key) if 32 <= key < 127 else f"virtual-key:0x{key:02x}")


def parse_accelerators(data: bytes) -> list[dict[str, object]]:
    if len(data) % 8:
        raise ValueError("accelerator table has a partial entry")
    result = []
    for offset in range(0, len(data), 8):
        flags, key, command, _padding = struct.unpack_from("<HHHH", data, offset)
        modifiers = []
        if flags & 0x08:
            modifiers.append("Ctrl")
        if flags & 0x04:
            modifiers.append("Shift")
        if flags & 0x10:
            modifiers.append("Alt")
        result.append(
            {
                "flags": f"0x{flags:04x}",
                "key": key_name(key, bool(flags & 0x01)),
                "modifiers": modifiers,
                "command_id": command,
                "last": bool(flags & 0x80),
            }
        )
    return result


def resource_id(path: Path) -> str:
    return path.stem.rsplit("-", 1)[0]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("raw_resource_directory", type=Path)
    parser.add_argument("output", type=Path, nargs="?")
    args = parser.parse_args()

    root = args.raw_resource_directory.resolve()
    output = args.output or root.parent / "ui-catalog.json"
    catalog: dict[str, object] = {"dialogs": {}, "menus": {}, "accelerators": {}}

    for path in sorted((root / "5").glob("*.bin")):
        catalog["dialogs"][resource_id(path)] = parse_dialog(path.read_bytes())
    for path in sorted((root / "4").glob("*.bin")):
        catalog["menus"][resource_id(path)] = parse_menu(path.read_bytes())
    for path in sorted((root / "9").glob("*.bin")):
        catalog["accelerators"][resource_id(path)] = parse_accelerators(path.read_bytes())

    output.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(
        f"Cataloged {len(catalog['dialogs'])} dialogs, {len(catalog['menus'])} menus, "
        f"and {len(catalog['accelerators'])} accelerator tables."
    )


if __name__ == "__main__":
    main()
