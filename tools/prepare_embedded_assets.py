#!/usr/bin/env python3
"""Copy extracted resources and add BMP headers to Windows DIB bitmaps."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import wave


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def dib_metadata(data: bytes) -> tuple[int, int, int, int]:
    if len(data) < 40:
        raise ValueError("DIB is shorter than a BITMAPINFOHEADER")
    header_size, width, height, planes, bits_per_pixel, compression = struct.unpack_from(
        "<IiiHHI", data
    )
    if header_size < 40 or planes != 1:
        raise ValueError("unsupported DIB header")
    colors_used = struct.unpack_from("<I", data, 32)[0]
    palette_entries = colors_used or (1 << bits_per_pixel if bits_per_pixel <= 8 else 0)
    mask_bytes = 0
    if header_size == 40 and compression in (3, 6):
        mask_bytes = 16 if compression == 6 else 12
    pixel_offset = header_size + mask_bytes + palette_entries * 4
    if pixel_offset > len(data):
        raise ValueError("DIB palette extends beyond the payload")
    return width, abs(height), bits_per_pixel, pixel_offset


def make_bmp(data: bytes) -> tuple[bytes, str]:
    width, height, bits_per_pixel, pixel_offset = dib_metadata(data)
    file_header = struct.pack("<2sIHHI", b"BM", len(data) + 14, 0, 0, pixel_offset + 14)
    return file_header + data, f"{width}x{height}x{bits_per_pixel}"


def wave_metadata(path: Path) -> str:
    with wave.open(str(path), "rb") as sound:
        return (
            f"channels={sound.getnchannels()},rate={sound.getframerate()},"
            f"bits={sound.getsampwidth() * 8},frames={sound.getnframes()}"
        )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("resource_directory", type=Path)
    args = parser.parse_args()

    source = args.resource_directory.resolve()
    index = json.loads((source / "index.json").read_text(encoding="utf-8"))
    output = Path(__file__).resolve().parents[1] / "assets" / "embedded"
    raw_output = output / "raw"
    bitmap_output = output / "bitmaps"
    audio_output = output / "audio"
    for directory in (raw_output, bitmap_output, audio_output):
        directory.mkdir(parents=True, exist_ok=True)

    manifest = [
        "type\tidentifier\tlanguage\tsize\tsha256\traw_file\tprepared_file\tmetadata"
    ]
    converted_bitmaps = 0
    copied_audio = 0

    for resource in index:
        resource_type, identifier, language = resource["identifiers"]
        relative = Path(resource["file"])
        source_file = source / relative
        data = source_file.read_bytes()
        if len(data) != resource["size"] or sha256(data) != resource["sha256"]:
            raise ValueError(f"resource verification failed: {relative}")

        raw_file = raw_output / relative
        raw_file.parent.mkdir(parents=True, exist_ok=True)
        raw_file.write_bytes(data)

        prepared_file = ""
        metadata = ""
        if resource_type == 2:
            bitmap, metadata = make_bmp(data)
            destination = bitmap_output / relative.with_suffix(".bmp").name
            destination.write_bytes(bitmap)
            prepared_file = str(destination.relative_to(output))
            converted_bitmaps += 1
        elif resource_type == "WAVE":
            destination = audio_output / relative.name
            shutil.copyfile(source_file, destination)
            prepared_file = str(destination.relative_to(output))
            metadata = wave_metadata(destination)
            copied_audio += 1

        manifest.append(
            "\t".join(
                (
                    str(resource_type),
                    str(identifier),
                    str(language),
                    str(resource["size"]),
                    resource["sha256"],
                    str(Path("raw") / relative),
                    prepared_file,
                    metadata,
                )
            )
        )

    (output / "resource-manifest.tsv").write_text(
        "\n".join(manifest) + "\n", encoding="utf-8"
    )
    print(
        f"Prepared {len(index)} raw resources, {converted_bitmaps} BMP files, "
        f"and {copied_audio} embedded WAV files."
    )


if __name__ == "__main__":
    main()
