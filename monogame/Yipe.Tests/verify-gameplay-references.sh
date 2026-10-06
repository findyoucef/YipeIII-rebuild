#!/usr/bin/env bash
# Run from any directory. Large traces stay in the ignored build tree.
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
trace_dir=build/monogame-reference
mkdir -p "$trace_dir"
common=(src/encounter.cpp src/spell.cpp src/shop.cpp src/map.cpp src/player.cpp src/random.cpp src/generated/original_maps.cpp src/generated/original_monsters.cpp src/generated/original_monster_definitions.cpp src/generated/original_weapon_damage.cpp)
for suite in services exploration combat quest encounters scores items; do
  case "$suite" in
    items) sources=(src/item_names.cpp) ;;
    scores) sources=(src/auxiliary_data.cpp src/player.cpp src/random.cpp src/audio.cpp) ;;
    services) sources=(src/shop.cpp src/spell.cpp src/casino.cpp src/player.cpp src/random.cpp) ;;
    exploration) sources=(src/map.cpp src/generated/original_maps.cpp src/exploration.cpp src/player.cpp src/random.cpp) ;;
    quest) sources=(src/quest.cpp "${common[@]}") ;;
    *) sources=("${common[@]}") ;;
  esac
  c++ -std=c++20 -Wall -Wextra -Werror -fsanitize=undefined -fno-sanitize-recover=all -I include \
    "monogame/Yipe.Tests/${suite}_reference.cpp" "${sources[@]}" -o "$trace_dir/$suite-reference"
  "$trace_dir/$suite-reference" > "$trace_dir/$suite-reference.txt"
done
dotnet build monogame/Yipe.slnx -c Release --no-restore -p:CheckForOverflowUnderflow=true
for suite in services exploration combat quest encounters scores items; do
  dotnet run --project monogame/Yipe.Tests -c Release --no-build -- "--$suite-trace" "$trace_dir/$suite-csharp.txt"
  cmp "$trace_dir/$suite-reference.txt" "$trace_dir/$suite-csharp.txt"
done
