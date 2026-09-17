#pragma once

#include "yipe/player.hpp"

#include <array>
#include <cstddef>
#include <optional>
#include <string_view>

namespace yipe {

struct SoundEffect {
    int id;
    std::string_view asset_name;
};

// Embedded WAVE resources selected by the original dispatcher at 0x004491b0.
[[nodiscard]] const std::array<SoundEffect, 37>& sound_effects();
[[nodiscard]] std::optional<std::string_view> sound_asset_name(int sound_id);

// The original external monster-death loader aliases these type IDs before
// looking for "<type>.wav" and falls back to embedded sound 12 when absent.
[[nodiscard]] int monster_sound_file_id(int monster_type);

// Sound IDs written by the recovered spell and ability implementations.
[[nodiscard]] int spell_sound_id(std::size_t spell);
[[nodiscard]] int ability_sound_id(SpecialAbility ability);

} // namespace yipe
