#!/bin/sh
set -eu

input_root=${1:-}
if [ -z "$input_root" ]; then
    echo "usage: $0 /path/to/original/Yipe-directory" >&2
    exit 2
fi

manifest=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)/docs/INPUT_MANIFEST.tsv
failed=0
while IFS="	" read -r expected_hash expected_size relative_path; do
    [ "$expected_hash" = "sha256" ] && continue
    [ -z "$expected_hash" ] && continue
    file="$input_root/$relative_path"
    if [ ! -f "$file" ]; then
        echo "missing: $relative_path" >&2
        failed=1
        continue
    fi
    actual_size=$(wc -c < "$file" | tr -d ' ')
    actual_hash=$(shasum -a 256 "$file" | cut -d ' ' -f 1)
    if [ "$actual_size" != "$expected_size" ] || [ "$actual_hash" != "$expected_hash" ]; then
        echo "mismatch: $relative_path" >&2
        failed=1
    else
        echo "ok: $relative_path"
    fi
done < "$manifest"

exit "$failed"
