# Embedded assets

Run `tools/prepare_embedded_assets.py` against the analysis resource directory to reproduce this tree.

- `raw/` preserves every extracted PE resource payload byte for byte.
- `bitmaps/` adds the 14-byte BMP file header that Windows resource DIBs omit. The DIB header, palette, and pixels remain unchanged; SDL2 can load the resulting BMP files directly.
- `audio/` contains the 39 embedded WAVE resources without transformation.
- `resource-manifest.tsv` connects resource type/ID/language to raw and prepared paths, hashes, bitmap dimensions, and audio properties.

The resource IDs are evidence identifiers. Their gameplay meanings remain provisional until code references or original-runtime observations establish them.
