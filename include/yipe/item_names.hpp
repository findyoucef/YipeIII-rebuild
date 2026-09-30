#pragma once

#include <array>
#include <filesystem>
#include <string>

namespace yipe {

struct ItemNames {
    static constexpr std::size_t armor_count = 30;
    static constexpr std::size_t weapon_count = 38;
    static constexpr std::size_t maximum_name_length = 22;

    std::array<std::string, armor_count> armor;
    std::array<std::string, weapon_count> weapons;
    bool used_embedded_fallback = false;
};

// Reconstructs the parser at 0x00417e40.
[[nodiscard]] ItemNames load_item_names(const std::filesystem::path& path);
[[nodiscard]] ItemNames embedded_item_names(bool silly_mode = false);

} // namespace yipe
