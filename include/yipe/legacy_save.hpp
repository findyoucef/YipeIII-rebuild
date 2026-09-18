#pragma once

#include "yipe/save.hpp"

#include <array>
#include <cstdint>
#include <filesystem>

namespace yipe {

struct LegacyFlagValue {
    bool flag_0148 = false;
    std::uint16_t value_0080 = 0;
};

struct LegacyInventoryEntry {
    std::uint16_t item_id_0094 = 0;
    std::uint16_t value_00d0 = 0;
    bool flag_012a = false;
};

struct LegacyExtendedEntry {
    std::uint16_t value_065e = 0;
    std::int32_t value_0584 = 0;
    bool flag_0552 = false;
};

// Import-only representation of the headerless CArchive payload written by
// Yipe! III 1.1.6.0. Offset-based names preserve fields whose meanings remain
// unknown; game contains the subset already understood by the rebuild.
struct LegacySave {
    GameState game;
    std::int32_t stored_validation = 0;
    bool validation_passed = false;

    std::int32_t unknown_005c = 0;
    std::uint16_t unknown_007e = 0;
    std::uint16_t unknown_007c = 0;
    std::int32_t unknown_0074 = 0;
    std::int32_t unknown_0078 = 0;
    bool unknown_006c = false;
    bool unknown_0060 = false;
    bool unknown_0061 = false;
    bool unknown_0063 = false;
    bool unknown_64c = false;
    std::array<bool, 512> flags_0152{};
    std::array<bool, 512> flags_0352{};
    std::array<std::uint16_t, 15> values_010c{};
    std::array<LegacyFlagValue, 10> flag_values{};
    std::array<LegacyInventoryEntry, 30> inventory_entries{};
    std::array<LegacyExtendedEntry, 50> extended_entries{};
    bool trailing_healing_flag = false;
};

[[nodiscard]] LegacySave load_legacy_save(const std::filesystem::path& path);

} // namespace yipe
