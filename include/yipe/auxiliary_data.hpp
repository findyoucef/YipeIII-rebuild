#pragma once

#include <array>
#include <cstdint>
#include <filesystem>
#include <string>

namespace yipe {

struct HighScoreEntry {
    std::string name;
    std::int32_t value = 0;
};

struct OriginalAuxiliaryData {
    std::array<HighScoreEntry, 45> high_scores{};

    // Offset-based groups preserve the original serializer and in-memory order.
    std::array<std::uint16_t, 8> keys_0174_to_0182{};
    std::int32_t value_0190 = 0;
    std::int32_t value_0170 = 0;
    std::array<std::uint16_t, 5> keys_0184_to_018c{};
    std::int32_t value_016c = 0;
};

// Reads the original game's headerless CArchive payload used by Yipe3.dat.
[[nodiscard]] OriginalAuxiliaryData
load_original_auxiliary_data(const std::filesystem::path& path);

} // namespace yipe
